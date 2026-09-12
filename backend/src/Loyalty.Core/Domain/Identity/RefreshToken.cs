namespace Loyalty.Core.Domain.Identity;

/// <summary>
/// Token de refresco persistido (solo su HASH SHA-256, nunca el valor en claro).
/// POR QUÉ guardar el hash y no el token: si la BD se compromete, los tokens de
/// sesión reutilizables siguen siendo inutilizables. La rotación en cada refresh
/// (familia + reemplazo) detecta replay/robo de sesión.
/// </summary>
public sealed class RefreshToken
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid UserId { get; private set; }
    /// <summary>Familia de sesión: todos los refrescos emitidos por una misma sesión original comparten family.</summary>
    public Guid FamilyId { get; private set; }
    public string TokenHash { get; private set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; private set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset ExpiresAtUtc { get; private set; }
    public DateTimeOffset? RevokedAtUtc { get; private set; }
    /// <summary>Hash del token que sustituyó a este (rotación). Nullable: al emitir reemplazo se setea.</summary>
    public string? ReplacedByTokenHash { get; private set; }

    private RefreshToken() { } // EF Core

    public static RefreshToken Issue(Guid userId, Guid familyId, string tokenHash, DateTimeOffset expiresAtUtc)
    {
        if (string.IsNullOrWhiteSpace(tokenHash)) throw new ArgumentException("tokenHash obligatorio.", nameof(tokenHash));
        if (expiresAtUtc <= DateTimeOffset.UtcNow) throw new ArgumentException("Expiración en el pasado.", nameof(expiresAtUtc));
        return new RefreshToken { UserId = userId, FamilyId = familyId, TokenHash = tokenHash, ExpiresAtUtc = expiresAtUtc };
    }

    public bool IsActive(DateTimeOffset now) => !RevokedAtUtc.HasValue && ExpiresAtUtc > now;

    public bool WasReplaced => ReplacedByTokenHash != null;

    /// <summary>Revoca este token (logout o rotación) marcando quién lo reemplazó si aplica.</summary>
    public void Revoke(DateTimeOffset now, string? replacedByHash = null)
    {
        RevokedAtUtc = now;
        ReplacedByTokenHash = replacedByHash;
    }
}