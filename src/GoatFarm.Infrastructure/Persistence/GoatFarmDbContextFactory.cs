using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace GoatFarm.Infrastructure.Persistence;

public class GoatFarmDbContextFactory : IDesignTimeDbContextFactory<GoatFarmDbContext>
{
    public GoatFarmDbContext CreateDbContext(string[] args)
    {
        var basePath = Path.Combine(Directory.GetCurrentDirectory(), "..", "GoatFarm.Web");
        var configuration = new ConfigurationBuilder()
            .SetBasePath(basePath)
            .AddJsonFile("appsettings.json", optional: false)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .Build();

        var connectionString = Configuration.ConnectionStringResolver.Resolve(configuration);
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException("Connection string 'DefaultConnection' is not configured.");

        var optionsBuilder = new DbContextOptionsBuilder<GoatFarmDbContext>();
        optionsBuilder.UseNpgsql(connectionString);

        return new GoatFarmDbContext(optionsBuilder.Options);
    }
}
