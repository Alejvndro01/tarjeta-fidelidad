namespace Loyalty.Core.Domain.Identity;

/// <summary>
/// Usuario de la plataforma (staff): Super Admin (global), Tenant Admin (de una marca)
/// o Cajero (operador de un local/panel de escaneo). El rol determina el alcance de los
/// JWT en Auth  (Fase 3) y lo separamos del Actor 'Member' (consumidor final).
/// </summary>
public sealed class AppUser
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid? TenantId { get; private set; }        // null para Super Admin (cross-tenant)
    public Guid? StoreId { get; private set; }          // local asignado para cajeros
    public string Email { get; private set; } = string.Empty;
    public string PasswordHash { get; private set; } = string.Empty;

    /// <summary>Hash de salt fijo por seguridad; en Fase 3 usamos BCrypt/Argon2.
    /// Mantenemos el campo para trazabilidad no funcional sin exponer el secreto.</summary>
    public string PasswordSalt { get; private set; } = string.Empty;

    public UserRole Role { get; private set; }
    public bool IsActive { get; private set; } = true;
    public DateTimeOffset CreatedAtUtc { get; private set; } = DateTimeOffset.UtcNow;

    private AppUser() { }

    public static AppUser Create(Guid? tenantId, Guid? storeId, string email, string passwordHash,
        string passwordSalt, UserRole role)
    {
        if (string.IsNullOrWhiteSpace(email)) throw new ArgumentException("Email obligatorio.", nameof(email));
        if (role == UserRole.Cajero && tenantId is null) throw new ArgumentException("Un cajero requiere tenant.", nameof(tenantId));
        return new AppUser
        {
            TenantId = tenantId,
            StoreId = storeId,
            Email = email.Trim().ToLowerInvariant(),
            PasswordHash = passwordHash,
            PasswordSalt = passwordSalt,
            Role = role
        };
    }
}

public enum UserRole { SuperAdmin = 1, TenantAdmin = 2, Cajero = 3 }