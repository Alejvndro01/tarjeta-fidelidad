namespace Loyalty.Core.Domain.Security;

/// <summary>
/// Nombres de claims JWT propios del dominio. Centralizados para evitar typos y documentar
/// el contrato entre el emisor (TokenService) y los consumidores (CurrentUserClaims,
/// TenantContextMiddleware, member auth). Distinguimos el actor: "staff" (AppUser) vs "member".
/// </summary>
public static class ClaimNames
{
    public const string TenantId = "tenant_id";
    public const string StoreId = "store_id";
    /// <summary>Tipo de actor autenticado: "staff" (AppUser) o "member" (consumidor).</summary>
    public const string Actor = "actor";
    /// <summary>Rol del staff (RBAC). No presente en tokens de miembro.</summary>
    public const string Role = "role";
}