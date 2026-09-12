using Loyalty.Application.Coupons;
using Loyalty.Contracts.Coupons;
using Loyalty.Core.Domain.Coupons;
using Loyalty.Core.Domain.Members;
using Loyalty.Core.Domain.Transactions;
using Loyalty.Core.Domain.Security;
using Loyalty.Core.Domain.Stores;
using Loyalty.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Loyalty.Infrastructure.Coupons;

/// <summary>
/// Canje ACID de cupones en caja. Un cupón se puede canjear UNA sola vez; el UPDATE condicional
/// (WHERE Status = 1) en SaveChanges + registro inmutable RewardTransaction tipo CouponRedeemed
/// garantizan que dos escaneos simultáneos del mismo cupón no duplican el descuento.
/// POR QUÉ la misma estrategia de reintento que SaleProcessing: si el retry del servidor de base
/// de datos dispara el segundo intento, la condición de carrera se detecta porque el coupon ya
/// tiene Status != Issued y se rechaza con 409. Sin row-version pero con transacción aislada
/// serializable, el primer transacción commit y el segundo se aborta limpiamente.
/// </summary>
public sealed class CouponService : ICouponService
{
    private readonly LoyaltyDbContext _db;

    public CouponService(LoyaltyDbContext db) => _db = db;

    public Task<CouponRedeemResultDto> RedeemAsync(Guid operatorTenantId, Guid byUserId, Guid storeId, string couponCode, CancellationToken ct = default)
    {
        var strategy = _db.Database.CreateExecutionStrategy();
        return strategy.ExecuteAsync(async () => await RedeemInTransactionAsync(operatorTenantId, byUserId, storeId, couponCode, ct));
    }

    private async Task<CouponRedeemResultDto> RedeemInTransactionAsync(Guid operatorTenantId, Guid byUserId, Guid storeId, string couponCode, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(couponCode))
            throw new DomainException("El código del cupón es obligatorio.");

        // Lookup con lock pesimista: SELECT ... FOR UPDATE para que dos requests concurrentes
        // no lean el mismo Status=1 y ambos pasen el filtro de TryRedeem.
        var coupon = await _db.Coupons
            .FromSqlInterpolated($@"SELECT * FROM coupons
                WHERE ""CouponCode"" = {couponCode.Trim()}
                FOR UPDATE")
            .AsTracking()
            .FirstOrDefaultAsync(ct)
            ?? throw new DomainException("Cupón no encontrado.");

        // Validación de aislamiento: el cupón del miembro debe pertenecer al mismo tenant que el operador.
        if (coupon.TenantId != operatorTenantId)
            throw new DomainException("El cupón no pertenece a este local.");

        if (coupon.Status != CouponStatus.Issued)
        {
            throw new DomainException(coupon.Status == CouponStatus.Used
                ? "El cupón ya fue canjeado."
                : "El cupón ya expiró o fue revocado.");
        }

        // TryRedeem: muta el objeto si Status==Issued (true si ejecutó).
        var redeemed = coupon.TryRedeem(byUserId, storeId);
        if (!redeemed)
            throw new DomainException("No se pudo canjear el cupón (estado no válido).");

        // Auditoría en la misma transacción.
        _db.RewardTransactions.Add(RewardTransaction.Create(
            tenantId: coupon.TenantId,
            memberId: coupon.MemberId,
            type: RewardTransactionType.CouponRedeemed,
            pointsDelta: 0,
            stampsDelta: 0,
            amount: null,
            reference: $"coupon-{coupon.Id}",
            storeId: storeId,
            couponId: coupon.Id));

        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new DomainException("Conflicto de concurrencia al canjear el cupón. Reintente.");
        }

        return new CouponRedeemResultDto
        {
            CouponId = coupon.Id,
            CouponCode = coupon.CouponCode,
            Title = coupon.Title,
            Redeemed = true,
            RedeemedUtc = coupon.RedeemedUtc!.Value,
            RedeemedByUserId = byUserId,
            StoreId = storeId,
            Message = $"Cupón '{coupon.Title}' canjeado exitosamente."
        };
    }

    public async Task<IReadOnlyList<MemberCouponDto>> ListMemberActiveCouponsAsync(Guid memberId, CancellationToken ct = default)
    {
        return await _db.Coupons
            .AsNoTracking()
            .Where(c => c.MemberId == memberId)
            .Where(c => c.Status == CouponStatus.Issued)
            .Where(c => !c.ExpiresUtc.HasValue || c.ExpiresUtc > DateTimeOffset.UtcNow)
            .OrderByDescending(c => c.IssuedUtc)
            .Select(c => new MemberCouponDto
            {
                CouponId = c.Id,
                CouponCode = c.CouponCode,
                Title = c.Title,
                Description = c.Description,
                IssuedUtc = c.IssuedUtc,
                ExpiresUtc = c.ExpiresUtc,
                Status = c.Status.ToString(),
                SourcePassId = c.SourcePassId,
                SourceProvider = c.SourceProvider != null ? c.SourceProvider.Value.ToString() : null
            })
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<RedeemedCouponDto>> ListRecentRedeemedAsync(Guid tenantId, int take, CancellationToken ct)
    {
        if (take > 100) take = 100;
        return await _db.Coupons
            .AsNoTracking()
            .Where(c => c.TenantId == tenantId)
            .Where(c => c.Status == CouponStatus.Used && c.RedeemedUtc.HasValue)
            .OrderByDescending(c => c.RedeemedUtc)
            .Take(take)
            .Select(c => new RedeemedCouponDto
            {
                CouponId = c.Id,
                CouponCode = c.CouponCode,
                Title = c.Title,
                RedeemedUtc = c.RedeemedUtc!.Value,
                RedeemedByUserId = c.RedeemedByUserId,
                RedeemedByEmail = _db.AppUsers.Where(u => u.Id == c.RedeemedByUserId).Select(u => u.Email).FirstOrDefault(),
                StoreId = c.RedeemedAtStoreId,
                StoreName = _db.Stores.Where(s => s.Id == c.RedeemedAtStoreId).Select(s => s.Name).FirstOrDefault()
            })
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<StoreOptionDto>> ListStoresAsync(Guid tenantId, CancellationToken ct)
        => await _db.Stores
            .AsNoTracking()
            .Where(s => s.TenantId == tenantId && s.IsActive)
            .OrderBy(s => s.Name)
            .Select(s => new StoreOptionDto { StoreId = s.Id, Name = s.Name })
            .ToListAsync(ct);
}