namespace Loyalty.Core.Domain.Loyalty;

/// <summary>
/// Contador de sellos por miembro y producto (SKU). El modelo híbrido exige tracking
/// independiente del saldo de puntos: cada unidad comprada de un producto regido por
/// StampRule acumula un sello. Se actualiza atómicamente con lock de fila en la misma
/// transacción que otorga puntos, garantizando consistencia under concurrent POS scans.
/// </summary>
public sealed class MemberStamp : IRequiresTenant
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid TenantId { get; private set; }
    public Guid MemberId { get; private set; }
    public Guid LoyaltyProgramId { get; private set; }
    public string ProductSku { get; private set; } = string.Empty;
    public int Count { get; private set; }

    private MemberStamp() { } // EF Core

    public static MemberStamp Create(Guid tenantId, Guid memberId, Guid loyaltyProgramId, string productSku)
    {
        if (string.IsNullOrWhiteSpace(productSku)) throw new ArgumentException("SKU obligatorio.", nameof(productSku));
        return new MemberStamp
        {
            TenantId = tenantId,
            MemberId = memberId,
            LoyaltyProgramId = loyaltyProgramId,
            ProductSku = productSku,
            Count = 0
        };
    }

    /// <summary>Incremento atómico del contador de sellos (checked evita overflow silencioso).</summary>
    public void Add(int quantity)
    {
        if (quantity <= 0) throw new ArgumentOutOfRangeException(nameof(quantity), "Cantidad debe ser positiva.");
        checked { Count += quantity; }
    }
}