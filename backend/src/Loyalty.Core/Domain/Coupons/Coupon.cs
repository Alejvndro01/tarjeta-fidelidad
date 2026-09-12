using Loyalty.Core.Domain.Wallets;

namespace Loyalty.Core.Domain.Coupons;

/// <summary>
/// Cupón digital emitido al wallet del miembro tras canjear puntos/sellos.
/// La transición a Used/Expired ocurre de forma atómica al escanearse en el panel
/// del cajero (single-row UPDATE condicional), garantizando que un QR no se
/// reutilice bajo concurrencia.
/// </summary>
public sealed class Coupon : IRequiresTenant
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid TenantId { get; private set; }
    public Guid MemberId { get; private set; }
    public Guid LoyaltyProgramId { get; private set; }

    public string Title { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public string CouponCode { get; private set; } = string.Empty;  // código de un solo uso (canje atómico)
    public CouponStatus Status { get; private set; } = CouponStatus.Issued;

    public DateTimeOffset IssuedUtc { get; private set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ExpiresUtc { get; private set; }

    /// <summary>Pase/navegador desde el que fue emitido (Apple/Google) o null si fue web.</summary>
    public Guid? SourcePassId { get; private set; }
    public WalletProvider? SourceProvider { get; private set; }

    /// <summary>Quién/usuario del cajero que lo canjeó (auditoría).</summary>
    public Guid? RedeemedByUserId { get; private set; }
    public Guid? RedeemedAtStoreId { get; private set; }
    public DateTimeOffset? RedeemedUtc { get; private set; }

    private Coupon() { }

    public static Coupon Create(Guid tenantId, Guid memberId, Guid loyaltyProgramId,
        string title, string? description, string couponCode, DateTimeOffset? expiresUtc,
        Guid? sourcePassId, WalletProvider? sourceProvider)
    {
        if (string.IsNullOrWhiteSpace(couponCode)) throw new ArgumentException("Código de cupón obligatorio.", nameof(couponCode));
        return new Coupon
        {
            TenantId = tenantId,
            MemberId = memberId,
            LoyaltyProgramId = loyaltyProgramId,
            Title = title,
            Description = description,
            CouponCode = couponCode,
            ExpiresUtc = expiresUtc,
            SourcePassId = sourcePassId,
            SourceProvider = sourceProvider
        };
    }

    /// <summary>
    /// Canje atómico: solo tiene efecto si el cupón sigue en estado Issued.
    /// El UPDATE condicional (WHERE Status = Issued) en infraestructura impide doble canje
    /// incluso con lecturas concurrentes del mismo QR. Retorna false si ya fue usado/expirado.
    /// </summary>
    public bool TryRedeem(Guid byUserId, Guid atStoreId)
    {
        if (Status != CouponStatus.Issued) return false;
        if (ExpiresUtc.HasValue && ExpiresUtc.Value < DateTimeOffset.UtcNow)
        {
            Status = CouponStatus.Expired;
            return false;
        }

        Status = CouponStatus.Used;
        RedeemedByUserId = byUserId;
        RedeemedAtStoreId = atStoreId;
        RedeemedUtc = DateTimeOffset.UtcNow;
        return true;
    }
}

public enum CouponStatus { Issued = 1, Used = 2, Expired = 3, Revoked = 4 }