namespace Loyalty.Core.Domain.Members;

/// <summary>
/// Miembro de fidelización de un tenant. El 'QR hash' se genera en el registro
/// y se usa para validaciones instantáneas en caja a través de Redis (sin tocar la BD).
/// Los sellos quedan atómicamente calculados en una transacción EF Core con lock de fila.
/// </summary>
public sealed class Member : IRequiresTenant
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid TenantId { get; private set; }
    public Guid LoyaltyProgramId { get; private set; }

    /// <summary>Identificador desnormalizado para alta concurrencia: único por tenant. Índice (tenant_id, qr_hash).</summary>
    public string QrHash { get; private set; } = string.Empty;
    public string QrSecret { get; private set; } = string.Empty;  // claim HMAC firmado; nunca en logs

    public string PhoneNumber { get; private set; } = string.Empty;
    public string FullName { get; private set; } = string.Empty;
    public string? Email { get; private set; }                    // opcional en registro

    /// <summary>Saldo de puntos; incrementos/decrementos solo mediante el agregado para invariantes.</summary>
    public int PointsBalance { get; private set; }

    /// <summary>Nombre completo del tenant; la búsqueda por caja usa PhoneNumber o QrHash.</summary>
    public string? MembershipId { get; private set; }

    public DateTimeOffset JoinedAtUtc { get; private set; } = DateTimeOffset.UtcNow;

    private Member() { } // EF Core

    /// <summary>Fábrica: valida entradas obligatorias y normaliza contacto. Los QR hash/secret los emite el dominio de emisión QR.</summary>
    public static Member Create(Guid tenantId, Guid loyaltyProgramId, string phoneNumber, string fullName, string? email,
        string qrHash, string qrSecret, string? membershipId = null)
    {
        if (Guid.Empty == tenantId) throw new ArgumentException("Tenant obligatorio.", nameof(tenantId));
        if (string.IsNullOrWhiteSpace(phoneNumber)) throw new ArgumentException("Número de celular obligatorio.", nameof(phoneNumber));
        if (string.IsNullOrWhiteSpace(fullName)) throw new ArgumentException("Nombre obligatorio.", nameof(fullName));

        return new Member
        {
            TenantId = tenantId,
            LoyaltyProgramId = loyaltyProgramId,
            PhoneNumber = phoneNumber.Trim(),
            FullName = fullName.Trim(),
            Email = email?.Trim().ToLowerInvariant(),
            QrHash = qrHash,
            QrSecret = qrSecret,
            MembershipId = membershipId,
            PointsBalance = 0
        };
    }

    /// <summary>Única vía de mutación del saldo. La concurrency de EF Core (rowversion) evita sobrescrituras en picos.</summary>
    public void AwardPoints(int points)
    {
        if (points < 0) throw new ArgumentOutOfRangeException(nameof(points));
        checked { PointsBalance += points; }
    }

    public void RedeemPoints(int points)
    {
        if (points < 0) throw new ArgumentOutOfRangeException(nameof(points));
        if (PointsBalance < points)
            throw new DomainException($"Saldo insuficiente: tiene {PointsBalance}, requiere {points}.");
        PointsBalance -= points;
    }
}

/// <summary>Excepción de dominio; el middleware la traduce a 4xx con mensaje seguro al cliente.</summary>
public sealed class DomainException : Exception
{
    public DomainException(string message) : base(message) { }
}