using Loyalty.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Loyalty.Infrastructure.Persistence;

/// <summary>
/// Permite a `dotnet ef migrations add` instanciar el DbContext sin el pipeline HTTP completo.
/// Precedencia: env LOYALTY_DB_CONNECTION en runtime local (docker-compose) → default de desarrollo.
/// </summary>
public class LoyaltyDbContextFactory : IDesignTimeDbContextFactory<LoyaltyDbContext>
{
    public LoyaltyDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<LoyaltyDbContext>();

        var conn = Environment.GetEnvironmentVariable("LOYALTY_DB_CONNECTION")
                   ?? "Host=localhost;Port=5432;Database=loyalty;Username=loyalty;Password=loyalty_dev";

        optionsBuilder.UseNpgsql(conn, npgsql =>
        {
            npgsql.EnableRetryOnFailure(3);
        });

        return new LoyaltyDbContext(optionsBuilder.Options);
    }
}