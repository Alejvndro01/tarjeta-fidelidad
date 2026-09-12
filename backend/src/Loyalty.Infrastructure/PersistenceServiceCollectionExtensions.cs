using Loyalty.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Loyalty.Infrastructure;

/// <summary>Registra infraestructura de persistencia (PostgreSQL/EF Core).</summary>
public static class PersistenceServiceCollectionExtensions
{
    public static IServiceCollection AddPersistence(this IServiceCollection services, IConfiguration config)
    {
        var connectionString = config.GetConnectionString("LoyaltyDb")
            ?? throw new InvalidOperationException("Cadena 'LoyaltyDb' no configurada.");

        services.AddDbContext<LoyaltyDbContext>(o => o.UseNpgsql(connectionString));
        // POR QUÉ SIN EnableRetryOnFailure aquí: con la estrategia de reintento activa, EF/Npgsql 8
        // rechaza transacciones iniciadas por el usuario (BeginTransactionAsync) incluso dentro de
        // CreateExecutionStrategy().ExecuteAsync (recursión del strategy en cada query). El servicio
        // SaleProcessingService ya ejecuta su unidad ACID bajo CreateExecutionStrategy(), aportando
        // reintento resiliente; habilitarlo además a nivel DbContext rompe el hot path transaccional.
        // Nota: la factory design-time sí lo conserva (no maneja transacciones manuales).

        services.AddScoped<ITenantContext, TenantContext>();
        return services;
    }
}