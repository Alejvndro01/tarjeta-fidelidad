namespace Loyalty.Core.Domain;

/// <summary>
/// Marcador para entidades que deben cumplir el aislamiento por tenant.
/// No se aplica a Tenant ni a entidades globales cross-tenant (p.ej. AppUser-SuperAdmin).
/// El filtro global de EF Core lo consulta para aplicar la restricción por TenantId.
/// </summary>
public interface IRequiresTenant { }