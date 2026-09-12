using Loyalty.Application.Auth;
using Loyalty.Contracts.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Loyalty.Api.Controllers;

/// <summary>
/// Autenticación del STAFF (Cajero/TenantAdmin/SuperAdmin). Login/refresh/logout/me.
/// POR QUÉ refresh tokens: el access token es corto (60 min) y firmado; el refresh inserta
/// rotación/familia para revocar sesiones sin esperar expiración ni exponer credenciales.
/// El throttling anti-fuerza-bruta es por email+IP y devuelve 429.
/// </summary>
[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _auth;
    private readonly ILoginThrottleService _throttle;

    public AuthController(IAuthService auth, ILoginThrottleService throttle)
    {
        _auth = auth;
        _throttle = throttle;
    }

    /// <summary>Inicio de sesión de staff. Devuelve access + refresh.</summary>
    [HttpPost("login")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(AuthResponseDto), 200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(429)]
    public async Task<ActionResult<AuthResponseDto>> Login([FromBody] LoginRequestDto request, CancellationToken ct)
    {
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        if (_throttle.IsBlocked(request.Email, ip, out var retryAfter))
        {
            Response.Headers["Retry-After"] = retryAfter.ToString();
            return StatusCode(429, new { error = "rate_limited", message = $"Demasiados intentos. Reintente en {retryAfter}s." });
        }

        var result = await _auth.LoginAsync(request.Email, request.Password, ip, ct);
        return Ok(result);
    }

    /// <summary>Rota la sesión: access nuevo + refresh nuevo (revoca el anterior).</summary>
    [HttpPost("refresh")]
    [AllowAnonymous]
    public async Task<ActionResult<AuthResponseDto>> Refresh([FromBody] RefreshTokenRequestDto request, CancellationToken ct)
        => Ok(await _auth.RefreshAsync(request.RefreshToken, ct));

    /// <summary>Revoca el refresh token y termina la sesión.</summary>
    [HttpPost("logout")]
    [AllowAnonymous]
    public async Task<IActionResult> Logout([FromBody] LogoutRequestDto request, CancellationToken ct)
    {
        await _auth.LogoutAsync(request.RefreshToken, ct);
        return NoContent();
    }

    /// <summary>Devuelve la identidad/rol/tenant del token actual (para el frontend).</summary>
    [HttpGet("me")]
    [Authorize]
    public async Task<ActionResult<MeResponseDto>> Me(CancellationToken ct)
    {
        var userId = Guid.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "");
        return Ok(await _auth.GetMeAsync(userId, ct));
    }
}