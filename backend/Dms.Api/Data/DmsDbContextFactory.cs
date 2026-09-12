using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Dms.Api.Data;

/// <summary>
/// Lets `dotnet ef migrations add/remove` construct DmsDbContext without
/// running the full app host. Only used by design-time tooling — the app
/// itself registers DmsDbContext via DI in Program.cs using the real
/// configured connection string.
/// </summary>
public class DmsDbContextFactory : IDesignTimeDbContextFactory<DmsDbContext>
{
    public DmsDbContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .Build();

        var connectionString = configuration.GetConnectionString("Default")
            ?? "Host=localhost;Database=awakedrive;Username=postgres;Password=postgres";

        var optionsBuilder = new DbContextOptionsBuilder<DmsDbContext>();
        optionsBuilder.UseNpgsql(connectionString);

        return new DmsDbContext(optionsBuilder.Options);
    }
}
