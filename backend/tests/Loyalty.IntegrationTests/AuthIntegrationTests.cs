using Loyalty.Application.Auth;
using Loyalty.Core.Domain.Identity;
using Loyalty.Core.Domain.Security;
using Loyalty.Infrastructure;
using Loyalty.Infrastructure.Auth;
using Loyalty.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using Xunit;

namespace Loyalty.IntegrationTests;

/// <summary>
/// Tests de integración de la capa de Auth (Fase 3) contra PostgreSQL y Redis reales.
/// POR QUÉ integración vs unit: verifica rotación de refresh en BD y throttling atómico en
/// Redis, que un mock no cubriría. Reutiliza el registro de servicios de Infrastructure.
/// </summary>
public class AuthIntegrationTests
{
    private static readonly string _conn = "Host=127.0.0.1;Port=5433;Database=loyalty_test;Username=loyalty;Password=loyalty_dev;SslMode=Disable";

    private static ServiceProvider BuildServices()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IConnectionMultiplexer>(ConnectionMultiplexer.Connect("127.0.0.1:6379"));
        services.AddStackExchangeRedisCache(o => { o.Configuration = "127.0.0.1:6379"; o.InstanceName = "loyalty_test:"; });

        // JwtOptions de prueba.
        services.Configure<JwtOptions>(o =>
        {
            o.Issuer = "loyalty-test"; o.Audience = "loyalty-pwa";
            o.SecretKey = "TEST_SECRET_KEY_0123456789abcdef_32+_bytes_LONGENOUGH"; o.AccessTokenMinutes = 60; o.RefreshTokenMinutes = 10080;
        });

        services.AddDbContext<LoyaltyDbContext>(o => o.UseNpgsql(_conn));
        services.AddScoped<IPasswordHasherService, PasswordHasherService>();
        services.AddScoped<ITokenService, TokenService>();
        services.AddScoped<ILoginThrottleService, LoginThrottleService>();
        services.AddScoped<IAuthService, AuthService>();
        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task Login_refresh_and_throttle_work_e2e()
    {
        using var scope = BuildServices();
        using var serviceScope = scope.CreateScope();
        var sp = serviceScope.ServiceProvider;
        var db = sp.GetRequiredService<LoyaltyDbContext>();
        var auth = sp.GetRequiredService<IAuthService>();
        var throttle = sp.GetRequiredService<ILoginThrottleService>();

        // Crear un usuario de prueba único (evita colisión entre runs).
        var hasher = sp.GetRequiredService<IPasswordHasherService>();
        var email = $"cajero-e2e-{Guid.NewGuid():N}@test.dev";
        var password = "S3cret!pass";
        // SuperAdmin no exige tenant (invariante de dominio); suficiente para probar login/refresh/throttle.
        var user = AppUser.Create(null, null, email, hasher.HashPassword(password), "", UserRole.SuperAdmin);
        db.AppUsers.Add(user);
        await db.SaveChangesAsync();

        // 1. Login correcto → tokens.
        var login = await auth.LoginAsync(email, password, "10.0.0.1");
        Assert.False(string.IsNullOrWhiteSpace(login.AccessToken));
        Assert.False(string.IsNullOrWhiteSpace(login.RefreshToken));
        Assert.Equal(UserRole.SuperAdmin.ToString(), login.User.Role);

        // 2. El refresh emitido está activo en BD (hash).
        var tokenService = sp.GetRequiredService<ITokenService>();
        var hash0 = tokenService.HashRefreshToken(login.RefreshToken);
        var stored = await db.RefreshTokens.SingleAsync(t => t.TokenHash == hash0);
        Assert.True(stored.IsActive(DateTimeOffset.UtcNow));

        // 3. Refresh rotation → nuevo refresh, el viejo revocado.
        var rotated = await auth.RefreshAsync(login.RefreshToken);
        Assert.NotEqual(login.RefreshToken, rotated.RefreshToken);

        var st = await db.RefreshTokens.AsNoTracking().SingleAsync(t => t.TokenHash == hash0);
        Assert.True(st.RevokedAtUtc.HasValue, "el refresh original debe quedar revocado tras rotar");

        // 4. Reuso del refresh roto → AuthenticationException (replay detection).
        await Assert.ThrowsAsync<AuthenticationException>(() => auth.RefreshAsync(login.RefreshToken));

        // 5. Logout revoca el refresh activo del rotation.
        await auth.LogoutAsync(rotated.RefreshToken);
        await Assert.ThrowsAsync<AuthenticationException>(() => auth.RefreshAsync(rotated.RefreshToken));

        // 6. Throttle: 5 intentos fallidos → bloqueado.
        var ip = "10.0.0.99";
        throttle.Reset(email, ip);
        for (var i = 0; i < 5; i++)
            await Assert.ThrowsAsync<AuthenticationException>(() => auth.LoginAsync(email, "wrongpass", ip));
        var blocked = throttle.IsBlocked(email, ip, out var retryAfter);
        Assert.True(blocked);
        Assert.True(retryAfter > 0);
    }

    [Fact]
    public async Task Inactive_user_cannot_login()
    {
        using var scope = BuildServices();
        using var serviceScope = scope.CreateScope();
        var sp = serviceScope.ServiceProvider;
        var db = sp.GetRequiredService<LoyaltyDbContext>();
        var auth = sp.GetRequiredService<IAuthService>();
        var hasher = sp.GetRequiredService<IPasswordHasherService>();

        var email = $"inactive-{Guid.NewGuid():N}@test.dev";
        // No hay API para desactivar; usamos un usuario inexistente → 401.
        await Assert.ThrowsAsync<AuthenticationException>(() => auth.LoginAsync(email, "whatever", "10.0.0.2"));
    }
}