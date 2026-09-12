using System.Security.Claims;
using Loyalty.Application.Auth;
using Loyalty.Application.Coupons;
using Loyalty.Contracts.Coupons;
using Loyalty.Core.Domain.Members;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Loyalty.Api.Controllers;

/// <summary>
/// Panel de cajero: canje atómico de cupones. El operador presenta el código (escrito o escaneado)
/// y el servicio ejecuta el UPDATE condicional en una transacción ACID serializable.
/// Requiere Cajero o TenantAdmin (no SuperAdmin) y un tenant asignado.
/// </summary>
[ApiController]
[Route("api/pos")]
[Authorize(Roles = "Cajero,TenantAdmin")]
public class CouponPosController : ControllerBase
{
    private readonly ICouponService _coupons;
    private readonly ICurrentUser _currentUser;

    public CouponPosController(ICouponService coupons, ICurrentUser currentUser)
    {
        _coupons = coupons;
        _currentUser = currentUser;
    }

    /// <summary>Canjea un cupón presentado en caja. Código (o QR content) + tienda. Retorna 409 si ya fue usado.</summary>
    [HttpPost("coupons/redeem")]
    [ProducesResponseType(typeof(CouponRedeemResultDto), 200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(409)]
    public async Task<ActionResult<CouponRedeemResultDto>> RedeemCoupon(
        [FromBody] RedeemCouponRequestDto request, CancellationToken ct)
    {
        Guid? tenantIdNullable = _currentUser.TenantId;
        if (tenantIdNullable is null) throw new UnauthorizedAccessException("Operador sin tenant asignado.");
        Guid tenantId = tenantIdNullable.Value;

        var byUserId = GetCurrentUserId();
        var storeId = _currentUser.StoreId ?? request.StoreId;
        if (storeId == Guid.Empty) throw new DomainException("Asigne un local (storeId) para registrar el canje.");

        var result = await _coupons.RedeemAsync(tenantId, byUserId, storeId, request.CouponCode, ct);
        return Ok(result);
    }

    /// <summary>Historial de canjes recientes del tenant (auditoría para el panel).</summary>
    [HttpGet("coupons/redeemed")]
    [ProducesResponseType(typeof(IReadOnlyList<RedeemedCouponDto>), 200)]
    [ProducesResponseType(401)]
    public async Task<ActionResult<IReadOnlyList<RedeemedCouponDto>>> RecentRedeemed(int take = 25, CancellationToken ct = default)
    {
        Guid? tenantIdNullable = _currentUser.TenantId;
        if (tenantIdNullable is null) throw new UnauthorizedAccessException("Operador sin tenant asignado.");
        return Ok(await _coupons.ListRecentRedeemedAsync(tenantIdNullable.Value, take, ct));
    }

    /// <summary>Tiendas activas del tenant (selector).</summary>
    [HttpGet("stores")]
    [ProducesResponseType(typeof(IReadOnlyList<StoreOptionDto>), 200)]
    [ProducesResponseType(401)]
    public async Task<ActionResult<IReadOnlyList<StoreOptionDto>>> Stores(CancellationToken ct)
    {
        Guid? tenantIdNullable = _currentUser.TenantId;
        if (tenantIdNullable is null) throw new UnauthorizedAccessException("Operador sin tenant asignado.");
        return Ok(await _coupons.ListStoresAsync(tenantIdNullable.Value, ct));
    }

    private Guid GetCurrentUserId()
    {
        var sub = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("sub")?.Value;
        return sub != null ? Guid.Parse(sub) : Guid.Empty;
    }
}