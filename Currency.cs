public class Currency{
    public string Name { get; set; } = "Bitcoin";
    public string Pair { get; set; } = "BTCUSDT";
    public string Url { get; set; } = $"https://api.binance.com/api/v3/avgPrice?symbol=BTCUSDT";
    public string BuyValue { get; set; }
    public string SellValue { get; set; }
}