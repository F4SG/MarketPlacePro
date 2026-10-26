using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace MarketPlacePro.Data;

public sealed class MarketPlaceProContextFactory : IDesignTimeDbContextFactory<MarketPlaceProContext>
{
    public MarketPlaceProContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder().SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false).AddEnvironmentVariables().Build();
        var connection = configuration.GetConnectionString("MarketPlacePro")
            ?? throw new InvalidOperationException("Configure ConnectionStrings:MarketPlacePro.");
        return new MarketPlaceProContext(new DbContextOptionsBuilder<MarketPlaceProContext>().UseSqlServer(connection).Options);
    }
}
