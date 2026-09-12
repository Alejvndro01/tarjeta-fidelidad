using Loyalty.Core.Domain.Identity;

namespace Loyalty.Application.Auth;

/// <summary>
/// Genera y valida tokens JWT de acceso (firma HMAC-SHA256) y el valor aleatorio de refresh tokens.
/// Desacoplado de la persistencia para poder unit-testear claims y expiración aisladamente.
/// </summary>
public interface ITokenService
{
    /// <summary>Crea un access token con los claims de identidad, rol y tenant.</summary>
    string CreateAccessToken(AppUser user);

    /// <summary>Emisión genérica de access token por claims (staff o miembro consumidor).</summary>
    string CreateToken(IEnumerable<System.Security.Claims.Claim> claims);

    /// <summary>Relama claims tipo AccessToken; lanza si la firma/expiración es inválida.</summary>
    System.Security.Claims.ClaimsPrincipal? ValidateAccessToken(string accessToken);

    /// <summary>Valor aleatorio criptográfico del refresh token (devuelto al cliente).</summary>
    string GenerateRefreshToken();

    /// <summary>Devuelve el hash SHA-256 del refresh token para almacenar en BD (nunca el valor en claro).</summary>
    string HashRefreshToken(string refreshToken);

    int AccessTokenExpiresInSeconds { get; }
    int RefreshTokenExpiresInSeconds { get; }
}