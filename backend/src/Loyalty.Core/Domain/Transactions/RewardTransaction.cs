namespace Loyalty.Core.Domain.Transactions;

/// <summary>
/// Registro inmutable de una operación de fidelización (acumulación de puntos,
/// sello, canje o emisión de cupón). Sirve de auditoría y fuente para reportes.
/// Se inserta en la misma transacción ACID que muta los saldos.
/// </summary>
public sealed class RewardTransaction : IRequiresTenant
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid TenantId { get; private set; }
    public Guid MemberId { get; private set; }
    public Guid? StoreId { get; private set; }
    public Guid? CouponId { get; private set; }

    public RewardTransactionType Type { get; private set; }
    public int PointsDelta { get; private set; }   // +ganado / -canjeado
    public int StampsDelta { get; private set; }
    public decimal? Amount { get; private set; }   // monto de la compra si aplica
    public string Reference { get; private set; } = string.Empty;  // idempotencia / caja
    public DateTimeOffset CreatedUtc { get; private set; } = DateTimeOffset.UtcNow;

    private RewardTransaction() { }

    public static RewardTransaction Create(Guid tenantId, Guid memberId, RewardTransactionType type,
        int pointsDelta, int stampsDelta, decimal? amount, string reference, Guid? storeId = null, Guid? couponId = null)
    {
        return new RewardTransaction
        {
            TenantId = tenantId,
            MemberId = memberId,
            Type = type,
            PointsDelta = pointsDelta,
            StampsDelta = stampsDelta,
            Amount = amount,
            Reference = reference,
            StoreId = storeId,
            CouponId = couponId
        };
    }
}

public enum RewardTransactionType
{
    PointsEarned = 1,
    StampEarned = 2,
    PointsRedeemed = 3,
    CouponIssued = 4,
    CouponRedeemed = 5
}