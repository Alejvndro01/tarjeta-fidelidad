using System.Security.Claims;
using Loyalty.Application.Auth;
using Loyalty.Infrastructure.Persistence;

namespace Loyalty.Api.Infrastructure;

/// <summary>
/// Implementación de ICurrentUser sobre el ClaimsPrincipal del request (JWT ya validado por
/// el schema Bearer). POR QUÉ como tipo separado: la capa de API publica el principal y la
/// aplicación lo lee tipado, aislando a los servicios de ASP.NET. Nula/throw si no autenticado.
/// </summary>
public sealed class CurrentUserClaims : ICurrentUser
{
    private readonly ClaimsPrincipal? _principal;

    public CurrentUserClaims(IHttpContextAccessor httpContextAccessor)
        => _principal = httpContextAccessor.HttpContext?.User;

    public bool IsAuthenticated => _principal?.Identity?.IsAuthenticated ?? false;

    public Guid UserId
    {
        get
        {
            var sub = _principal?.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? _principal?.FindFirstValue("sub");
            return Guid.TryParse(sub, out var id) ? id : Guid.Empty;
        }
    }

    public string? Email => _principal?.FindFirstValue(ClaimTypes.Email);

    public string Role => _principal?.FindFirstValue(ClaimTypes.Role) ?? string.Empty;

    public Guid? TenantId
    {
        get
        {
            var v = _principal?.FindFirstValue("tenant_id");
            return Guid.TryParse(v, out var id) ? id : null;
        }
    }

    public Guid? StoreId
    {
        get
        {
            var v = _principal?.FindFirstValue("store_id");
            return Guid.TryParse(v, out var id) ? id : null;
        }
    }
}