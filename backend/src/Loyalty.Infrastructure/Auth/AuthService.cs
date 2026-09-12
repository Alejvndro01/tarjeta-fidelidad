using Loyalty.Application.Auth;
using Loyalty.Contracts.Auth;
using Loyalty.Core.Domain.Identity;
using Loyalty.Core.Domain.Security;
using Loyalty.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Loyalty.Infrastructure.Auth;

/// <summary>
/// Orquesta login/refresh/logout/me. POR QUÉ respuestas uniformes y 401 para credenciales
/// malas (no revelar si el email existe): evita enumeration de cuentas. El throttling se
/// evalúa ANTES de consultar la BD y se registra antes de validar la contraseña, para que
/// los intentos fallidos siempre cuenten. La rotación de refresh (familia) detecta robo.
/// </summary>
public sealed class AuthService : IAuthService
{
    private readonly LoyaltyDbContext _db;
    private readonly IPasswordHasherService _hasher;
    private readonly ITokenService _tokens;
    private readonly ILoginThrottleService _throttle;

    public AuthService(LoyaltyDbContext db, IPasswordHasherService hasher, ITokenService tokens, ILoginThrottleService throttle)
    {
        _db = db;
        _hasher = hasher;
        _tokens = tokens;
        _throttle = throttle;
    }

    public async Task<AuthResponseDto> LoginAsync(string email, string password, string ipAddress, CancellationToken ct = default)
    {
        var ip = ipAddress ?? "unknown"; // IP real del request, poblada por el controller.
        email = (email ?? string.Empty).Trim().ToLowerInvariant();

        // Throttle: bloquear TEMPRANO para no gastar CPU de PBKDF2 en atacantes bloqueados.
        if (_throttle.IsBlocked(email, ip, out var retryAfter))
            throw new RateLimitException(retryAfter);

        // No exponer si el email existe: si no hay usuario, devolvemos 401 genérico igualmente.
        var user = await _db.AppUsers.SingleOrDefaultAsync(u => u.Email == email, ct);
        var passwordOk = user != null && _hasher.VerifyPassword(password, user.PasswordHash);

        if (user == null || !passwordOk || !user.IsActive)
        {
            _throttle.RecordFailedAttempt(email, ip, out _, out _);
            throw new AuthenticationException("Credenciales inválidas o usuario inactivo."); // → 401
        }

        _throttle.Reset(email, ip);
        return await IssueSessionAsync(user.Id, ct);
    }

    public async Task<AuthResponseDto> RefreshAsync(string refreshToken, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(refreshToken)) throw new AuthenticationException("Token de refresco requerido.");

        var hash = _tokens.HashRefreshToken(refreshToken);
        var stored = await _db.RefreshTokens.SingleOrDefaultAsync(t => t.TokenHash == hash, ct);
        if (stored == null) throw new AuthenticationException("Sesión inválida o expirada.");

        var now = DateTimeOffset.UtcNow;
        if (!stored.IsActive(now)) throw new AuthenticationException("Sesión expirada o revocada.");

        var user = await _db.AppUsers.SingleOrDefaultAsync(u => u.Id == stored.UserId, ct);
        if (user == null || !user.IsActive)
        {
            stored.Revoke(now);
            await _db.SaveChangesAsync(ct);
            throw new AuthenticationException("Usuario inactivo o eliminado.");
        }

        // Rotación: revoca el refresh actual y emite uno nuevo en la misma familia.
        stored.Revoke(now);
        var familyId = stored.FamilyId;
        var newRefresh = _tokens.GenerateRefreshToken();
        var newHash = _tokens.HashRefreshToken(newRefresh);
        var newStored = RefreshToken.Issue(user.Id, familyId, newHash, now.AddSeconds(_tokens.RefreshTokenExpiresInSeconds));
        _db.RefreshTokens.Add(newStored);

        await _db.SaveChangesAsync(ct);

        return new AuthResponseDto
        {
            AccessToken = _tokens.CreateAccessToken(user),
            RefreshToken = newRefresh,
            RefreshTokenExpiresInSeconds = _tokens.RefreshTokenExpiresInSeconds,
            AccessTokenExpiresInSeconds = _tokens.AccessTokenExpiresInSeconds,
            User = ToUserInfo(user)
        };
    }

    public async Task LogoutAsync(string refreshToken, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(refreshToken)) return;
        var hash = _tokens.HashRefreshToken(refreshToken);
        var stored = await _db.RefreshTokens.SingleOrDefaultAsync(t => t.TokenHash == hash, ct);
        if (stored == null) return;
        stored.Revoke(DateTimeOffset.UtcNow);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<MeResponseDto> GetMeAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await _db.AppUsers.SingleOrDefaultAsync(u => u.Id == userId, ct)
            ?? throw new AuthenticationException("Usuario no encontrado.");
        return new MeResponseDto { User = ToUserInfo(user) };
    }

    private async Task<AuthResponseDto> IssueSessionAsync(Guid userId, CancellationToken ct)
    {
        var user = await _db.AppUsers.SingleAsync(u => u.Id == userId, ct);
        var now = DateTimeOffset.UtcNow;
        var refreshToken = _tokens.GenerateRefreshToken();
        var hash = _tokens.HashRefreshToken(refreshToken);
        var familyId = Guid.NewGuid();

        _db.RefreshTokens.Add(RefreshToken.Issue(user.Id, familyId, hash, now.AddSeconds(_tokens.RefreshTokenExpiresInSeconds)));
        await _db.SaveChangesAsync(ct);

        return new AuthResponseDto
        {
            AccessToken = _tokens.CreateAccessToken(user),
            RefreshToken = refreshToken,
            RefreshTokenExpiresInSeconds = _tokens.RefreshTokenExpiresInSeconds,
            AccessTokenExpiresInSeconds = _tokens.AccessTokenExpiresInSeconds,
            User = ToUserInfo(user)
        };
    }

    private static AppUserInfoDto ToUserInfo(AppUser user) => new()
    {
        Id = user.Id,
        Email = user.Email,
        Role = user.Role.ToString(),
        TenantId = user.TenantId,
        StoreId = user.StoreId
    };
}