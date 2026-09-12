using Loyalty.Contracts.Auth;

namespace Loyalty.Application.Auth;

/// <summary>
/// Servicio de autenticación de staff: verifica credenciales, emite JWT + refresh, rota y revoca.
/// Encapsula el orquestador; TokenService (firma/claims) y PasswordHasherService quedan desacoplados.
/// </summary>
public interface IAuthService
{
    Task<AuthResponseDto> LoginAsync(string email, string password, string ipAddress, CancellationToken ct = default);

    Task<AuthResponseDto> RefreshAsync(string refreshToken, CancellationToken ct = default);
    Task LogoutAsync(string refreshToken, CancellationToken ct = default);
    Task<MeResponseDto> GetMeAsync(Guid userId, CancellationToken ct = default);
}