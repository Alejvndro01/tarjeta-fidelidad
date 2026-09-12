namespace Loyalty.Application.Auth;

/// <summary>
/// Acceso tipado al usuario autenticado del request (claims JWT) sin acoplar la capa de
/// aplicación a ASP.NET. El controller publica el ClaimsPrincipal; los servicios leen
/// identidad/rol/tenant desde aquí para, p.ej., POS aislado por tenant (nunca confiar en
/// un tenant del body).
/// </summary>
public interface ICurrentUser
{
    Guid UserId { get; }
    string? Email { get; }
    string Role { get; }
    Guid? TenantId { get; }
    Guid? StoreId { get; }
    bool IsAuthenticated { get; }
}