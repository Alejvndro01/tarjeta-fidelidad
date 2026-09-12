using Loyalty.Contracts.Loyalty;

namespace Loyalty.Application.Loyalty;

/// <summary>
/// Procesa una venta de caja bajo el modelo híbrido (puntos por monto + sellos por SKU).
/// El contrato vive en Application; la implementación transaccional (ACID) en Infrastructure
/// porque necesita EF Core. Así la capa de aplicación no se acopla a la persistencia.
/// </summary>
public interface ISaleProcessingService
{
    /// <param name="tenantId">Tenant (marca) autenticado del request.</param>
    /// <param name="storeId">Local donde ocurre la venta (null para autoservicio sin local).</param>
    /// <param name="reference">Idempotencia: dedupe de retries de caja por el mismo reference.</param>
    Task<SaleProcessingResult> ProcessSaleAsync(
        Guid tenantId,
        Guid memberId,
        Guid? storeId,
        IReadOnlyList<SaleLineItemDto> items,
        decimal totalAmount,
        string reference,
        CancellationToken ct = default);
}

/// <summary>Valida/mantiene la caché de QR para escaneo instantáneo de caja.</summary>
public interface IMemberQrCacheService
{
    /// <summary>Devuelve el TenantId y MemberId de un QR hash sin tocar la BD (0 a BD en hot path).</summary>
    Task<QrCacheEntry?> TryResolveAsync(string qrHash, CancellationToken ct = default);

    /// <summary>Refresca/inserta la entrada del miembro en Redis tras cambios de saldo.</summary>
    Task SeedAsync(Guid tenantId, Guid memberId, string qrHash, CancellationToken ct = default);
}

public sealed record QrCacheEntry(Guid TenantId, Guid MemberId);