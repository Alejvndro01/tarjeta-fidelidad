using Loyalty.Application.Auth;
using Loyalty.Core.Domain.Identity;
using Loyalty.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Loyalty.Infrastructure.Auth;

/// <summary>
/// Siembra los usuarios STAFF iniciales (SuperAdmin global + un TenantAdmin y un Cajero de demo)
/// si no existen. POR QUÉ fuera del dominio: es bootstrap de infraestructura/dev, y usa el hasher
/// para eliminar la creación de usuarios fuera del registro seguro. Idempotente por email.
/// En producción el password NO se hardcodea: se provee por config/env (Seed:DemoPassword).
/// Si el entorno no es Development y no se proveyó password, el seeder se salta (gestión manual
/// de usuarios iniciales — nunca sembrar credenciales por defecto en producción).
/// </summary>
public static class AuthSeeder
{
    public static async Task SeedAsync(IServiceScope scope, CancellationToken ct = default)
    {
        var db = scope.ServiceProvider.GetRequiredService<LoyaltyDbContext>();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasherService>();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("AuthSeeder");
        var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();

        // Password de demo: SOLO entra por config/env (Seed:DemoPassword). En dev hay un default;
        // en producción si no se provee, abortamos (nunca sembrar una password fija conocida).
        var demoPassword = config["Seed:DemoPassword"];
        var env = config["ASPNETCORE_ENVIRONMENT"] ?? "Production";
        var isDevelopment = string.Equals(env, "Development", StringComparison.OrdinalIgnoreCase);

        if (string.IsNullOrWhiteSpace(demoPassword) && isDevelopment)
            demoPassword = "DemoPass!123"; // [Solo dev] default local; no existe en producción.

        if (string.IsNullOrWhiteSpace(demoPassword))
        {
            logger.LogWarning("AuthSeeder: sin Seed:DemoPassword en {Env} — se omite el seed de usuarios demo.", env);
            return;
        }

        var seedUsers = new List<(string Email, UserRole Role, string Password, Guid? TenantId)>
        {
            ("admin@loyalty.dev", UserRole.SuperAdmin, demoPassword, null),
            ("tenantadmin@burger.dev", UserRole.TenantAdmin, demoPassword, null), // tenant se asigna abajo
            ("cajero@burger.dev", UserRole.Cajero, demoPassword, null)
        };

        // El TenantAdmin/Cajero deben pertenecer al tenant de la marca demo (si existe).
        var demoTenant = await db.Tenants.FirstOrDefaultAsync(t => t.Slug == "burger-demo", ct);
        var demoTenantId = demoTenant?.Id;

        foreach (var (email, role, password, _) in seedUsers)
        {
            var exists = await db.AppUsers.AnyAsync(u => u.Email == email, ct);
            if (exists) continue;

            var tenant = role == UserRole.SuperAdmin ? null : demoTenantId;
            var user = AppUser.Create(tenant, null, email, hasher.HashPassword(password), "", role);
            db.AppUsers.Add(user);
            logger.LogInformation("Seed: creó usuario {Email} rol {Role}", email, role);
        }

        await db.SaveChangesAsync(ct);
    }
}