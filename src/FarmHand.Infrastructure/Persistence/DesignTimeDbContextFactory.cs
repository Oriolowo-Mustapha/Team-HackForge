using FarmHand.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using System.IO;

namespace FarmHand.Infrastructure.Persistence;

public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<FarmHandDbContext>
{
    public FarmHandDbContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? "Host=localhost;Port=5432;Database=FarmHandDb;Username=postgres;Password=postgres";

        var optionsBuilder = new DbContextOptionsBuilder<FarmHandDbContext>();
        optionsBuilder.UseNpgsql(connectionString);

        return new FarmHandDbContext(optionsBuilder.Options);
    }
}
