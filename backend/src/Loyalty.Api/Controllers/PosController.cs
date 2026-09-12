using Loyalty.Application.Auth;
using Loyalty.Application.Loyalty;
using Loyalty.Contracts.Loyalty;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Loyalty.Api.Controllers;

/// <summary>
/// Endpoint de caja rápida: escanea el QR del miembro y procesa la venta en un solo round-trip.
/// POR QUÉ el tenant sale del JWT (ICurrentUser) y no del body: un cajero no puede escanear QR
/// de otra marca aunque lo envíe en el request; el alcance lo da su rol + tenant autenticado.
/// La resolución del QR va por Redis (hot path) y el procesamiento es ACID.
/// </summary>
[ApiController]
[Route("api/pos")]
[Authorize(Roles = "Cajero,TenantAdmin,SuperAdmin")]
public class PosController : ControllerBase
{
    private readonly IMemberQrCacheService _qrCache;
    private readonly ISaleProcessingService _saleProcessing;
    private readonly ICurrentUser _currentUser;

    public PosController(IMemberQrCacheService qrCache, ISaleProcessingService saleProcessing, ICurrentUser currentUser)
    {
        _qrCache = qrCache;
        _saleProcessing = saleProcessing;
        _currentUser = currentUser;
    }

    /// <summary>Procesa una venta para el miembro cuyo QR fue escaneado. Requiere Cajero/TenantAdmin/SuperAdmin.</summary>
    [HttpPost("sales")]
    [ProducesResponseType(typeof(SaleProcessingResult), 200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(403)]
    [ProducesResponseType(404)]
    public async Task<ActionResult<SaleProcessingResult>> ProcessSale(
        [FromBody] PositionSaleRequest request, CancellationToken ct)
    {
        var qr = await _qrCache.TryResolveAsync(request.QrHash, ct);
        if (qr == null)
            return NotFound(new { error = "QR inválido o miembro no encontrado." });

        // El tenant del operador (del JWT) debe gobernar; si es nulo (SuperAdmin sin tenant) lo tomamos
        // del QR de forma controlada — pero en producción SuperAdmin no opera caja. Aquí: el que autorizó
        // debe pertenecer al tenant del QR o ser SuperAdmin.
        var operatorTenantId = _currentUser.TenantId;
        if (operatorTenantId is null)
            return StatusCode(403, new { error = "Operador sin tenant asignado." });

        if (operatorTenantId != qr.TenantId)
            return StatusCode(403, new { error = "El QR no pertenece a su tenant." });

        var result = await _saleProcessing.ProcessSaleAsync(
            operatorTenantId.Value, qr.MemberId, _currentUser.StoreId ?? request.StoreId,
            request.Items, request.TotalAmount, request.Reference, ct);

        return Ok(result);
    }
}

/// <summary>Request del POS: QR del miembro + datos de la venta. Sin tenant (viene del JWT).</summary>
public sealed class PositionSaleRequest
{
    public string QrHash { get; init; } = string.Empty;
    public Guid? StoreId { get; init; }
    public List<SaleLineItemDto> Items { get; init; } = new();
    public decimal TotalAmount { get; init; }
    public string Reference { get; init; } = string.Empty;
}