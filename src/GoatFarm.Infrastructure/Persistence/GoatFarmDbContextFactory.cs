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
            .Build();

        var optionsBuilder = new DbContextOptionsBuilder<GoatFarmDbContext>();
        optionsBuilder.UseSqlServer(configuration.GetConnectionString("DefaultConnection"));

        return new GoatFarmDbContext(optionsBuilder.Options);
    }
}
