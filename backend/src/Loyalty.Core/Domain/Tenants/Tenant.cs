namespace Loyalty.Core.Domain.Tenants;

/// <summary>
/// Raíz multi-tenant. Todo agregado del sistema lleva un TenantId para aislar
/// datos por marca. Cualquier consulta cross-tenant se considera un defecto
/// de seguridad y se bloquea en el filtro global de EF Core.
/// </summary>
public sealed class Tenant
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public string Name { get; private set; } = string.Empty;
    public string Slug { get; private set; } = string.Empty;      // identificador URL seguro
    public string? LogoUrl { get; private set; }
    public string? PrimaryColor { get; private set; }            // branding del frontend móvil
    public TenantPlan Plan { get; private set; }                // determina capacidades de fidelización
    public bool IsActive { get; private set; } = true;
    public DateTimeOffset CreatedAtUtc { get; private set; } = DateTimeOffset.UtcNow;

    private Tenant() { } // EF Core

    /// <summary>Creación vía fábrica para mantener el invariante: slug único y plan requerido.</summary>
    public static Tenant Create(string name, string slug, TenantPlan plan, string? logoUrl = null, string? primaryColor = null)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("El nombre del tenant es obligatorio.", nameof(name));
        if (string.IsNullOrWhiteSpace(slug)) throw new ArgumentException("El slug es obligatorio.", nameof(slug));

        return new Tenant
        {
            Name = name.Trim(),
            Slug = slug.Trim().ToLowerInvariant(),
            Plan = plan,
            LogoUrl = logoUrl,
            PrimaryColor = primaryColor
        };
    }
}

/// <summary>
/// Planes de capacidad por tenant. Determina qué módulos de fidelización están
/// activos y límites de concurrencia/cuota. Se mapea a enum en la BD (int).
/// </summary>
public enum TenantPlan
{
    Starter = 1,   // puntos por monto
    Growth = 2,    // puntos + sellos
    Enterprise = 3 // puntos + sellos + wallet push + analytics
}