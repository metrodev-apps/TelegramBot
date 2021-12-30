using Microsoft.Extensions.Configuration;
using HtmlAgilityPack;
using ScrapySharp.Network;
using QRCoder;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Extensions.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

// Load configuration from appsettings.json
var config = LoadConfiguration();
var botClient = config.GetSection("TelegramBotClient");

if (botClient == null)
{
    Console.WriteLine("No se encontro el BotClientId en la configuracion");
    return;
}
var bot = new TelegramBotClient(botClient.Value);

using var cts = new CancellationTokenSource();

// StartReceiving does not block the caller thread. Receiving is done on the ThreadPool.
var receiverOptions = new ReceiverOptions
{
    AllowedUpdates = { } // receive all update types
};
bot.StartReceiving(
    HandleUpdateAsync,
    HandleErrorAsync,
    receiverOptions,
    cancellationToken: cts.Token);

var me = await bot.GetMeAsync();

Console.WriteLine($"Start listening for @{me.Username}");
Console.ReadLine();

// Send cancellation request to stop bot
cts.Cancel();

async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
{
    // Only process Message updates: https://core.telegram.org/bots/api#message
    if (update.Type != UpdateType.Message)
        return;
    // Only process text messages
    if (update.Message!.Type != MessageType.Text)
        return;

    var chatId = update.Message.Chat.Id;
    string messageText = update.Message.Text != null? update.Message.Text.ToLower() : string.Empty;

    // End process when send empty messages
    if (string.IsNullOrEmpty(messageText)){
        return;
    }

    Console.WriteLine($"Received a '{messageText}' message in chat {chatId}.");
    
    if(messageText.Contains("price"))
    {
        Currency currency = new Currency();
        if(ContainsOne(messageText, new string[]{"bitcoin","btc", "bit coin"})){
            currency = GetPairFromBinance("BTCUSDT", "Bitcoin");
        }
        if(ContainsOne(messageText, new string[]{"ether","ethereum", "ethers", "eth"})){
            currency = GetPairFromBinance("ETHUSDT", "Ethereum");
        }
        if(ContainsOne(messageText, new string[]{"dolar blue","blue", "dolarblue", "dolar paralelo"})){
            currency = GetDolarHoyValues();
        }
        
        messageText = $"{currency.Name} price is: {currency.SellValue}";
    }
    else if(messageText.Contains("password")){
        
        if (messageText.Contains("new")){
            NewPassword(messageText, messageText);

            messageText = $"{messageText} guardado correctamente";
            Message sentMessage = await botClient.SendTextMessageAsync(
            chatId: chatId,
            text: messageText,
            cancellationToken: cancellationToken);
        }
        else{
            // get password from database.
            var password = GetPassword(messageText);
            await GenerateQR(bot,chatId, password);
        }
    }
    else 
    {
        // Echo received message text
        messageText = $"You said: {messageText}";
        Message sentMessage = await botClient.SendTextMessageAsync(
        chatId: chatId,
        text: messageText,
        cancellationToken: cancellationToken);
    }


}

Task HandleErrorAsync(ITelegramBotClient botClient, Exception exception, CancellationToken cancellationToken)
{
    var ErrorMessage = exception switch
    {
        ApiRequestException apiRequestException
            => $"Telegram API Error:\n[{apiRequestException.ErrorCode}]\n{apiRequestException.Message}",
        _ => exception.ToString()
    };

    Console.WriteLine(ErrorMessage);
    return Task.CompletedTask;
}

Boolean ContainsOne(string message, string[] words)
{
    return (message.Split(" ").Any(x=>words.Contains(x)));
}

// Binance pair values
Currency GetPairFromBinance(string pair, string currencyName)
{
    var currency = new Currency();
    currency.Name = currencyName;
    currency.Pair = pair;
    currency.Url = $"https://api.binance.com/api/v3/avgPrice?symbol={currency.Pair}";     

    var client = new HttpClient();
    var msg = client.GetStringAsync(currency.Url).Result;

    var obj = Newtonsoft.Json.JsonConvert.DeserializeObject<BinancePrice>(msg);

    if (obj != null)
    {
        currency.SellValue = obj.Price != null ? obj.Price : "No disponible";
    }

    return currency;
}

// Get Dolar blue values from dolarhoy.com
Currency GetDolarHoyValues()
{
    var url = "https://www.dolarhoy.com/";
    var values = new List<Currency>();
    var html = GetHtml(url);
    var dolarNode = html.SelectNodes("//div[@class='tile dolar']");

    var name = dolarNode[0].SelectSingleNode("./div/div/a");
    var buyValue = dolarNode[0].SelectSingleNode("./div/div/div/div[1]/div[2]");
    var sellValue = dolarNode[0].SelectSingleNode("./div/div/div/div[2]/div[2]");

    var dolarObj = new Currency();
    dolarObj.Name = name != null ? name.InnerText.Trim() : string.Empty;
    dolarObj.BuyValue = buyValue != null ? buyValue.InnerText.Trim() : string.Empty;
    dolarObj.SellValue = sellValue != null ? sellValue.InnerText.Trim() : string.Empty;

    return dolarObj;
}

// Get entire html from website for scrapping
static HtmlNode GetHtml(string url)
{
    ScrapingBrowser scraping = new ScrapingBrowser();
    WebPage webpage = scraping.NavigateToPage(new Uri(url));
    return webpage.Html;
}

// Get configuration from appsettings.json
static IConfiguration LoadConfiguration()
{
    var builder = new ConfigurationBuilder()
        .SetBasePath(Directory.GetCurrentDirectory())
        .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false);
    return builder.Build();
}

// Print QR Code on telegram bot
static async Task GenerateQR(TelegramBotClient bot, long chatId, string text) {  
    QRCodeGenerator qrGenerator = new QRCodeGenerator();
    QRCodeData qrCodeData = qrGenerator.CreateQrCode(text, QRCodeGenerator.ECCLevel.Q);
    PngByteQRCode qrCode = new PngByteQRCode(qrCodeData);
    byte[] qrCodeAsPngByteArr = qrCode.GetGraphic(20);

    using (MemoryStream ms = new MemoryStream(qrCodeAsPngByteArr))
    {
        // This is important: otherwise anything reading the stream
        // will start at the point AFTER the written image.
        ms.Position = 0;
        Message message = await bot.SendPhotoAsync(
                chatId: chatId,
                photo: ms,
                caption: "Escaneame"
            );
    }
}  

// Get password from database
static string GetPassword(string text)
{
    string resultPass = string.Empty;
    using (var db = new PasswordContext())
    {
        Console.WriteLine("Querying for a password");
        var pass = db.Passwords
            .OrderBy(b => b.Name.ToLower() == text)
            .FirstOrDefault();

        if (pass != null) resultPass = pass.Pwd;
    }
    return resultPass;
}

// Create a new password record on database
static void NewPassword(string name, string pass)
{
    using (var db = new PasswordContext())
    {
        Password passObj = new Password(name, pass);
        Console.WriteLine("Inserting a new password");
        db.Add(passObj);
        db.SaveChanges();
    }
}
