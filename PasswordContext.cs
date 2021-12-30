using Microsoft.EntityFrameworkCore;


public class PasswordContext : DbContext
{
    public DbSet<Password> Passwords { get; set; }

    public string DbPath { get; }

    public PasswordContext()
    {
        var folder = Environment.SpecialFolder.LocalApplicationData;
        var path = Environment.GetFolderPath(folder);
        Console.WriteLine(path);
        DbPath = System.IO.Path.Join(path, "data.db");
    }

    // The following configures EF to create a Sqlite database file in the
    // special "local" folder for your platform.
    protected override void OnConfiguring(DbContextOptionsBuilder options)
        => options.UseSqlite($"Data Source={DbPath}");
}
