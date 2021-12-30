
public class Password
{
    public int Id { get; set; }
    public string Name { get; set; }
    public string Pwd { get; set; }
    public string Url { get; set; }
    public string Description { get; set; }
    public DateTime Created { get; set; } = DateTime.UtcNow;

    public Password(string name, string pwd)
    {
        this.Name = name;
        this.Pwd = pwd;
        this.Url = string.Empty;
        this.Description = string.Empty;
    }
}