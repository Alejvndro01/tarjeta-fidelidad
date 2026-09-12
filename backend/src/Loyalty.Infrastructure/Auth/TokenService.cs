using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Loyalty.Application.Auth;
using Loyalty.Core.Domain.Identity;
using Loyalty.Core.Domain.Security;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Loyalty.Infrastructure.Auth;

/// <summary>
/// Emisión y validación de JWT (access, corto) + refresh (opaco, aleatorio). POR QUÉ separar:
/// el access lived ~60 min y el refresh ~7 días; cada uno tiene ciclo de vida y validación
/// distintos. Los claims incluyen sub (userId), role, tenant (nullable) y store para RBAC
/// y para el TenantContextMiddleware (que lee el tenant del token y lo fija en AsyncLocal).
/// </summary>
public sealed class TokenService : ITokenService
{
    private readonly JwtOptions _options;
    private readonly SigningCredentials _signingCredentials;

    public TokenService(IOptions<JwtOptions> options)
    {
        _options = options.Value;

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SecretKey));
        _signingCredentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
    }

    public int AccessTokenExpiresInSeconds => _options.AccessTokenMinutes * 60;
    public int RefreshTokenExpiresInSeconds => _options.RefreshTokenMinutes * 60;

    public string CreateAccessToken(AppUser user)
    {
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new(ClaimTypes.Role, user.Role.ToString()),
            new(ClaimNames.Actor, "staff"),
        };

        if (user.TenantId.HasValue)
            claims.Add(new Claim(ClaimNames.TenantId, user.TenantId.Value.ToString()));
        if (user.StoreId.HasValue)
            claims.Add(new Claim(ClaimNames.StoreId, user.StoreId.Value.ToString()));

        return CreateToken(claims);
    }

    /// <summary>Emisión genérica de access token a partir de claims (staff o miembro).</summary>
    public string CreateToken(IEnumerable<Claim> claims)
    {
        var now = DateTime.UtcNow;
        var token = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims: claims,
            notBefore: now,
            expires: now.AddMinutes(_options.AccessTokenMinutes),
            signingCredentials: _signingCredentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public ClaimsPrincipal? ValidateAccessToken(string accessToken)
    {
        var handler = new JwtSecurityTokenHandler();
        var parameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = _options.Issuer,
            ValidateAudience = true,
            ValidAudience = _options.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = _signingCredentials.Key,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30)
        };

        try { return handler.ValidateToken(accessToken, parameters, out _); }
        catch { return null; }
    }

    public string GenerateRefreshToken()
    {
        // 256 bits aleatorios → 32 bytes Base64URL (no subjetos a adivinar/fuerza bruta).
        return Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    public string HashRefreshToken(string refreshToken)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(refreshToken));
        return Convert.ToBase64String(bytes);
    }
}