using Loyalty.Contracts.Coupons;

namespace Loyalty.Application.Coupons;

/// <summary>
/// Servicio de canje y consulta de cupones.
/// - El canje es ACID con UPDATE condicional (solo si Status == Issued) y registro de auditoría.
/// - La lista es para el miembro autenticado (PWA).
/// </summary>
public interface ICouponService
{
    Task<CouponRedeemResultDto> RedeemAsync(Guid operatorTenantId, Guid byUserId, Guid storeId, string couponCode, CancellationToken ct = default);
    Task<IReadOnlyList<MemberCouponDto>> ListMemberActiveCouponsAsync(Guid memberId, CancellationToken ct = default);
    Task<IReadOnlyList<RedeemedCouponDto>> ListRecentRedeemedAsync(Guid tenantId, int take = 25, CancellationToken ct = default);
    Task<IReadOnlyList<StoreOptionDto>> ListStoresAsync(Guid tenantId, CancellationToken ct = default);
}