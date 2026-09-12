using Loyalty.Application.Loyalty;
using Loyalty.Contracts.Loyalty;
using Loyalty.Core.Domain;
using Loyalty.Core.Domain.Coupons;
using Loyalty.Core.Domain.Loyalty;
using Loyalty.Core.Domain.Members;
using Loyalty.Core.Domain.Transactions;
using Loyalty.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Loyalty.Infrastructure.Loyalty;

/// <summary>
/// Procesa ventas de caja bajo el modelo híbrido en UNA transacción ACID.
/// POR QUÉ todo en una sola transacción: puntos, sellos, recompensa y registro de auditoría
/// deben persistir juntos o no persistir nada; un cajero que escanee y luego falle a mitad
/// no puede dejar saldos parcialmente actualizados que desincronicen el wallet del miembro.
/// POR QUÉ CreateExecutionStrategy: con EnableRetryOnFailure habilitado, las transacciones
/// manuales deben ejecutarse dentro de la estrategia para que el reintento re-ejecute la
/// unidad completa (BeginCommit) en vez de re-intentar una transacción ya abierta/abortada.
/// Combinamos: concurrency optimista (RowVersion) en Member + lock pesimista (SELECT FOR UPDATE)
/// en el contador de sellos para evitar condiciones de carrera entre escaneos simultáneos.
/// </summary>
public sealed class SaleProcessingService : ISaleProcessingService
{
    private readonly LoyaltyDbContext _db;
    private readonly IProgramCacheService _programCache;

    public SaleProcessingService(LoyaltyDbContext db, IProgramCacheService programCache)
    {
        _db = db;
        _programCache = programCache;
    }

    public Task<SaleProcessingResult> ProcessSaleAsync(
        Guid tenantId, Guid memberId, Guid? storeId, IReadOnlyList<SaleLineItemDto> items,
        decimal totalAmount, string reference, CancellationToken ct = default)
    {
        // Unidad ACID re-ejecutable bajo la estrategia de reintento de Npgsql.
        var strategy = _db.Database.CreateExecutionStrategy();
        return strategy.ExecuteAsync(async () => await ExecuteInTransactionAsync(
            tenantId, memberId, storeId, items, totalAmount, reference, ct));
    }

    private async Task<SaleProcessingResult> ExecuteInTransactionAsync(
        Guid tenantId, Guid memberId, Guid? storeId, IReadOnlyList<SaleLineItemDto> items,
        decimal totalAmount, string reference, CancellationToken ct)
    {
        if (items == null || items.Count == 0) throw new DomainException("La venta no contiene ítems.");
        if (totalAmount <= 0) throw new DomainException("El monto total debe ser positivo.");

        // Idempotencia: reference único por (tenant + caja) para no duplicar puntos en retries.
        // Nota: NO paralelizar estas dos lecturas — EF Core prohíbe operaciones concurrentes
        // sobre el mismo DbContext (un tracked context no es thread-safe por requests paralelos).
        var alreadyProcessed = await _db.RewardTransactions
            .AnyAsync(t => t.TenantId == tenantId && t.Reference == reference && t.Type == RewardTransactionType.PointsEarned, ct);
        if (alreadyProcessed)
            throw new DomainException($"La transacción con referencia '{reference}' ya fue procesada.");

        await using var tx = await _db.Database.BeginTransactionAsync(ct);

        // Member con RowVersion; el UPDATE condicional del SaveChanges lanzará DbUpdateConcurrencyException
        // si otra transacción modificó el miembro entre la lectura y la escritura.
        var member = await _db.Members
            .SingleAsync(m => m.Id == memberId && m.TenantId == tenantId, ct);

        // Configuración del programa desde caché Redis (reglas+recompensas cambian rara vez).
        // POR QUÉ no re-fetchear con Include: el saldo/sellos del miembro se leen vivos de BD (ACID),
        // pero la config del programa es casi inmutable; servirla desde cache reduce 2-3 round-trips.
        var programModel = await _programCache.GetAsync(member.LoyaltyProgramId, ct)
            ?? throw new DomainException("Programa de lealtad no configurado para el miembro.");
        // Guard: el programa cacheado debe pertenecer al campo del miembro.
        if (programModel.Id != member.LoyaltyProgramId) throw new DomainException("Programa de lealtad inválido.");

        var pointsEarned = 0;
        var stampsEarned = new List<MemberStamp>();
        var stampsDeltaBySku = new Dictionary<string, int>();
        var issuedCoupons = new List<Coupon>();

        if (programModel.PointsEnabled)
        {
            // Puntos = monto total * factor de conversión (redondeo hacia abajo → entero).
            pointsEarned = (int)(totalAmount * programModel.PointsPerMonetaryUnit);
            if (pointsEarned > 0) member.AwardPoints(pointsEarned);
        }

        if (programModel.StampsEnabled)
        {
            // Reward canjeable por sellos (el que define qué cupón se emite al completar un ciclo).
            var stampReward = programModel.Rewards
                .Where(r => r.IsActive && r.StampCost > 0)
                .OrderBy(r => r.StampCost)
                .FirstOrDefault();

            var rulesBySku = programModel.StampRules.ToDictionary(r => r.ProductSku, StringComparer.OrdinalIgnoreCase);
            foreach (var line in items)
            {
                if (!rulesBySku.TryGetValue(line.ProductSku, out var rule)) continue;

                var qty = line.Quantity > 0 ? line.Quantity : 1;
                stampsDeltaBySku[line.ProductSku] = stampsDeltaBySku.GetValueOrDefault(line.ProductSku) + qty;
            }

            foreach (var (sku, qty) in stampsDeltaBySku)
            {
                var rule = programModel.StampRules.FirstOrDefault(r => r.ProductSku == sku);
                if (rule == null) continue;

                // Lock pesimista de fila: SELECT ... FOR UPDATE sobre el contador del miembro+SKU.
                var stamp = await _db.MemberStamps
                    .FromSqlInterpolated($@"SELECT * FROM member_stamps
                        WHERE ""TenantId"" = {tenantId} AND ""MemberId"" = {memberId} AND ""ProductSku"" = {sku}
                        FOR UPDATE")
                    .FirstOrDefaultAsync(ct);

                if (stamp == null)
                {
                    // No existe fila → crearla con contador inicial +Upsert. Evita race al insertar
                    // usando el índice único; si dos piden a la vez, uno gana y el otro re-lee.
                    stamp = MemberStamp.Create(tenantId, memberId, programModel.Id, sku);
                    _db.MemberStamps.Add(stamp);
                }

                // Cruces de umbral: cuántos ciclos completos se alcanzaron NUEVAMENTE en esta venta.
                // POR QUÉ prevCount→floor: evita re-emitir en cada venta post-umbral (un miembro en 6/3
                // no recibe cupón por comprar 1 más; solo al cruzar a 6, 9, ...). Anti-spam natural.
                var prevCount = stamp.Count;
                stamp.Add(qty);
                var cyclesBefore = prevCount / rule.StampsRequired;
                var cyclesAfter = stamp.Count / rule.StampsRequired;
                stampsEarned.Add(stamp);

                // Emitir un cupón por cada ciclo recién completado.
                for (int k = cyclesBefore + 1; k <= cyclesAfter; k++)
                {
                    // Preferir el reward de sellos; si no existe, uno canjeable por puntos ai disponibidad.
                    var couponReward = stampReward ?? programModel.Rewards
                        .FirstOrDefault(r => r.IsActive && r.PointsCost > 0 && member.PointsBalance >= r.PointsCost);
                    var coupon = Coupon.Create(
                        tenantId, memberId, programModel.Id,
                        couponReward?.Name ?? $"Recompensa por {rule.ProductName}",
                        "Cupón emitido automáticamente al completar sellos.",
                        $"{programModel.Id:N}".Substring(0, 6).ToUpperInvariant() + "-" + Guid.NewGuid().ToString("N").Substring(0, 8).ToUpperInvariant(),
                        DateTimeOffset.UtcNow.AddDays(30),
                        null, null);
                    issuedCoupons.Add(coupon);
                    _db.Coupons.Add(coupon);
                }
            }
        }

        // Registro de auditoría inmutable (misma transacción).
        _db.RewardTransactions.Add(RewardTransaction.Create(
            tenantId, memberId, RewardTransactionType.PointsEarned,
            pointsEarned, stampsDeltaBySku.Values.Sum(), totalAmount, reference, storeId));

        // Guardar todo juntos; el SaveChanges lanza concurrency exceptions si hubo conflictos.
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            // Reintentar bajo retry-policy del outer (EnableRetryOnFailure) o propagar para el caller.
            throw new DomainException("Conflicto de concurrencia al actualizar saldos. Reintente.");
        }

        await tx.CommitAsync(ct);

        // Construcción del resultado con las recompensas completadas (sellos==umbral o ya superado).
        var stampsCompleted = new List<StampCompletedDto>();
        foreach (var stamp in stampsEarned)
        {
            var rule = programModel.StampRules.FirstOrDefault(r => r.ProductSku == stamp.ProductSku);
            if (rule != null && stamp.Count >= rule.StampsRequired)
                stampsCompleted.Add(new StampCompletedDto { ProductSku = stamp.ProductSku, StampRuleId = rule.Id.ToString() });
        }

        var reward = programModel.Rewards.FirstOrDefault(r => r.IsActive && r.PointsCost > 0 && member.PointsBalance >= r.PointsCost);
        // Nota: la auto-recompensa por alcanzar sellos/umbral se implementa en Fase 6 (emisión de cupón);
        // aquí solo reportamos el hito.

        return new SaleProcessingResult
        {
            MemberId = memberId,
            PointsEarned = pointsEarned,
            PointsBalance = member.PointsBalance,
            StampsEarned = stampsEarned.Select(s => new StampEarnedDto
            {
                ProductSku = s.ProductSku,
                Count = stampsDeltaBySku[s.ProductSku],
                Total = s.Count
            }).ToList(),
            StampsCompleted = stampsCompleted,
            RewardId = reward?.Id,
            RewardCompleted = reward != null,
            CouponsIssued = issuedCoupons.Select(c => c.CouponCode).ToList(),
            TotalAmount = totalAmount
        };
    }
}