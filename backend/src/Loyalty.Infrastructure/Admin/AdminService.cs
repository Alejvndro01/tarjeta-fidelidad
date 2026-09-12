using Loyalty.Application.Admin;
using Loyalty.Application.Loyalty;
using Loyalty.Contracts.Admin;
using Loyalty.Core.Domain.Coupons;
using Loyalty.Core.Domain.Loyalty;
using Loyalty.Core.Domain.Members;
using Loyalty.Core.Domain.Transactions;
using Loyalty.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Loyalty.Infrastructure.Admin;

/// <summary>
/// Panel de administración multi-tenant. TODOS los queries/materializaciones reciben el tenantId
/// del operador (JWT), que además coincide con el filtro global AsyncLocal — un admin con tenant
/// definido solo ve su marca; nunca cruza a otra (el filtro global lo replica).
/// </summary>
public sealed class AdminService : IAdminService
{
    private readonly LoyaltyDbContext _db;
    private readonly IProgramCacheService _programCache;

    public AdminService(LoyaltyDbContext db, IProgramCacheService programCache)
    {
        _db = db;
        _programCache = programCache;
    }

    public async Task<AdminDashboardDto> GetDashboardAsync(Guid tenantId, CancellationToken ct = default)
    {
        var memberCount = await _db.Members.CountAsync(m => m.TenantId == tenantId, ct);
        var activeCoupons = await _db.Coupons.CountAsync(c => c.TenantId == tenantId && c.Status == CouponStatus.Issued && (!c.ExpiresUtc.HasValue || c.ExpiresUtc > DateTimeOffset.UtcNow), ct);
        var redeemedCoupons = await _db.Coupons.CountAsync(c => c.TenantId == tenantId && c.Status == CouponStatus.Used, ct);
        // Puntos emitidos (Type=1) vs canjeados (Type=3) en transacciones.
        var pointsIssued = await _db.RewardTransactions.Where(t => t.TenantId == tenantId && t.Type == RewardTransactionType.PointsEarned).SumAsync(t => (long)t.PointsDelta, ct);
        var pointsRedeemed = await _db.RewardTransactions.Where(t => t.TenantId == tenantId && t.Type == RewardTransactionType.PointsRedeemed).SumAsync(t => (long)t.PointsDelta, ct);

        return new AdminDashboardDto
        {
            MemberCount = memberCount,
            ActiveCoupons = activeCoupons,
            RedeemedCoupons = redeemedCoupons,
            TotalPointsIssued = pointsIssued,
            TotalPointsRedeemed = Math.Abs(pointsRedeemed),
        };
    }

    public async Task<(IReadOnlyList<AdminMemberDto> Items, int Total)> ListMembersAsync(Guid tenantId, int page, int pageSize, string? search, CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var q = _db.Members.AsNoTracking().Where(m => m.TenantId == tenantId);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            q = q.Where(m => m.FullName.Contains(s) || m.PhoneNumber.Contains(s) || m.Email!.Contains(s) || m.QrHash.Contains(s));
        }
        var total = await q.CountAsync(ct);
        var items = await q.OrderByDescending(m => m.JoinedAtUtc)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(m => new AdminMemberDto
            {
                MemberId = m.Id,
                QrHash = m.QrHash,
                PhoneNumber = m.PhoneNumber,
                FullName = m.FullName,
                Email = m.Email,
                PointsBalance = m.PointsBalance,
                JoinedAtUtc = m.JoinedAtUtc
            })
            .ToListAsync(ct);
        return (items, total);
    }

    public async Task<(IReadOnlyList<AdminCouponDto> Items, int Total)> ListCouponsAsync(Guid tenantId, int page, int pageSize, CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var q = _db.Coupons.AsNoTracking().Where(c => c.TenantId == tenantId);
        var total = await q.CountAsync(ct);
        var items = await q.OrderByDescending(c => c.IssuedUtc)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(c => new AdminCouponDto
            {
                CouponId = c.Id,
                CouponCode = c.CouponCode,
                Title = c.Title,
                Status = c.Status.ToString(),
                MemberId = c.MemberId,
                MemberName = _db.Members.Where(m => m.Id == c.MemberId).Select(m => m.FullName).FirstOrDefault(),
                MemberPhone = _db.Members.Where(m => m.Id == c.MemberId).Select(m => m.PhoneNumber).FirstOrDefault(),
                IssuedUtc = c.IssuedUtc,
                ExpiresUtc = c.ExpiresUtc,
                RedeemedUtc = c.RedeemedUtc,
                StoreName = c.RedeemedAtStoreId != null ? _db.Stores.Where(s => s.Id == c.RedeemedAtStoreId).Select(s => s.Name).FirstOrDefault() : null
            })
            .ToListAsync(ct);
        return (items, total);
    }

    public async Task<AdminProgramDto> GetProgramAsync(Guid tenantId, CancellationToken ct = default)
    {
        return await _db.LoyaltyPrograms.AsNoTracking()
            .Where(p => p.TenantId == tenantId)
            .Select(p => new AdminProgramDto
            {
                ProgramId = p.Id,
                Name = p.Name,
                PointsEnabled = p.PointsEnabled,
                StampsEnabled = p.StampsEnabled,
                PointsPerMonetaryUnit = p.PointsPerMonetaryUnit,
                CurrencyCode = p.CurrencyCode,
                StampRules = p.StampRules.Select(r => new AdminStampRuleDto
                {
                    RuleId = r.Id,
                    ProductSku = r.ProductSku,
                    ProductName = r.ProductName,
                    StampsRequired = r.StampsRequired
                }).ToList(),
                Rewards = p.Rewards.Select(r => new AdminRewardDto
                {
                    RewardId = r.Id,
                    Name = r.Name,
                    Description = r.Description,
                    PointsCost = r.PointsCost,
                    StampCost = r.StampCost,
                    IsActive = r.IsActive
                }).ToList()
            })
            .FirstOrDefaultAsync(ct)
            ?? throw new DomainException("El tenant no tiene un programa de lealtad configurado.");
    }

    public Task UpdateProgramPointsAsync(Guid tenantId, UpdateProgramPointsRequest request, CancellationToken ct = default)
        => MutateProgramAsync(tenantId, ct, async (p) =>
        {
            p.UpdatePointsConfig(request.PointsPerMonetaryUnit, request.PointsEnabled, request.StampsEnabled);
        });

    public Task AddStampRuleAsync(Guid tenantId, AddStampRuleRequest request, CancellationToken ct = default)
        => MutateProgramAsync(tenantId, ct, async (p) =>
        {
            p.AddStampRule(tenantId, request.ProductSku, request.ProductName, request.StampsRequired);
            // Forzar estado Added: la colección del agregado (private IReadOnlyCollection) no siempre
            // se marca como Added por EF en fixup; registrar el hijo en el DbSet garantiza el INSERT.
            var added = p.StampRules.First(r => r.ProductSku == request.ProductSku && r.StampsRequired == request.StampsRequired);
            _db.StampRules.Add(added);
        });

    public Task AddRewardAsync(Guid tenantId, AddRewardRequest request, CancellationToken ct = default)
        => MutateProgramAsync(tenantId, ct, async (p) =>
        {
            p.AddReward(tenantId, request.Name, request.Description, request.PointsCost, request.StampCost);
            var added = p.Rewards.First(r => r.Name == request.Name);
            _db.RedemptionRewards.Add(added);
        });

    /// <summary>Crea un cupón manual (campaña) para un miembro del tenant; retorna el código emitido.</summary>
    public async Task<CreateCouponResultDto> CreateCouponAsync(Guid tenantId, CreateCouponRequest request, CancellationToken ct = default)
    {
        var member = await _db.Members.FirstOrDefaultAsync(m => m.Id == request.MemberId && m.TenantId == tenantId, ct)
            ?? throw new DomainException("Miembro no encontrado.");

        var programId = member.LoyaltyProgramId;
        var couponCode = $"{programId:N}".Substring(0, 6).ToUpperInvariant() + "-" + Guid.NewGuid().ToString("N").Substring(0, 8).ToUpperInvariant();

        var coupon = Coupon.Create(
            tenantId, member.Id, programId,
            request.Title,
            request.Description ?? "Cupón de campaña otorgado por la tienda.",
            couponCode,
            request.ExpiresUtc ?? DateTimeOffset.UtcNow.AddDays(30),
            null, null);

        _db.Coupons.Add(coupon);
        _db.RewardTransactions.Add(RewardTransaction.Create(
            tenantId, member.Id, RewardTransactionType.CouponIssued, 0, 0, null, $"coupon-{coupon.Id}", couponId: coupon.Id));

        try { await _db.SaveChangesAsync(ct); }
        catch (DbUpdateException)
        {
            throw new DomainException("No se pudo crear el cupón (posible código duplicado). Reintente.");
        }

        return new CreateCouponResultDto
        {
            CouponId = coupon.Id,
            CouponCode = coupon.CouponCode,
            Title = coupon.Title,
            ExpiresUtc = coupon.ExpiresUtc
        };
    }

    public async Task<(IReadOnlyList<AdminTransactionDto> Items, int Total)> ListTransactionsAsync(Guid tenantId, int page, int pageSize, CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var q = _db.RewardTransactions.AsNoTracking().Where(t => t.TenantId == tenantId);
        var total = await q.CountAsync(ct);
        var items = await q.OrderByDescending(t => t.CreatedUtc)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(t => new AdminTransactionDto
            {
                TransactionId = t.Id,
                MemberId = t.MemberId,
                MemberName = _db.Members.Where(m => m.Id == t.MemberId).Select(m => m.FullName).FirstOrDefault(),
                Type = t.Type.ToString(),
                PointsDelta = t.PointsDelta,
                StampsDelta = t.StampsDelta,
                Amount = t.Amount,
                Reference = t.Reference,
                StoreName = t.StoreId != null ? _db.Stores.Where(s => s.Id == t.StoreId).Select(s => s.Name).FirstOrDefault() : null,
                CreatedUtc = t.CreatedUtc
            })
            .ToListAsync(ct);
        return (items, total);
    }

    /// <summary>
    /// Reporte del rango. POR QUÉ agrego en SQL (GroupBy/Sum) y no en memoria: el volumen de
    /// reward_transactions por tenant crece con cada venta; traer las filas al servidor para sumar
    /// en C# no escala. Los 3 bloques (insights, por tienda, por día) salen en consultas agregadas.
    /// </summary>
    public async Task<AdminReportDto> GetReportAsync(Guid tenantId, DateTimeOffset from, DateTimeOffset to, Guid? storeId, CancellationToken ct = default)
    {
        var txQ = _db.RewardTransactions.AsNoTracking()
            .Where(t => t.TenantId == tenantId && t.CreatedUtc >= from && t.CreatedUtc < to);

        // Compras con puntos (PointsEarned) = ventas en caja.
        var salesQ = txQ.Where(t => t.Type == RewardTransactionType.PointsEarned);
        if (storeId.HasValue) salesQ = salesQ.Where(t => t.StoreId == storeId.Value);

        // Insights globales.
        var totalSales = await salesQ.CountAsync(ct);
        var pointsEarned = await salesQ.SumAsync(t => (long)t.PointsDelta, ct);
        var totalAmount = await salesQ.SumAsync(t => (decimal?)t.Amount ?? 0m, ct);

        // Emisiones y canjes en el rango (cupones por CreatedUtc / RedeemedUtc según el caso).
        var couponsIssued = await _db.Coupons.AsNoTracking()
            .Where(c => c.TenantId == tenantId && c.IssuedUtc >= from && c.IssuedUtc < to)
            .CountAsync(ct);
        var couponsRedeemed = await _db.Coupons.AsNoTracking()
            .Where(c => c.TenantId == tenantId && c.RedeemedUtc != null && c.RedeemedUtc >= from && c.RedeemedUtc < to)
            .CountAsync(ct);
        var newMembers = await _db.Members.AsNoTracking()
            .Where(m => m.TenantId == tenantId && m.JoinedAtUtc >= from && m.JoinedAtUtc < to)
            .CountAsync(ct);

        var gross = couponsIssued + couponsRedeemed;
        var insights = new AdminInsightsDto
        {
            NewMembers = newMembers,
            TotalNewMembers = newMembers,
            ActiveRedemptions = couponsRedeemed,
            TotalSales = totalSales,
            TotalCouponsIssued = couponsIssued,
            TotalCouponsRedeemed = couponsRedeemed,
            GrossRedemptionRate = gross == 0 ? 0m : Math.Round((decimal)couponsRedeemed / gross, 4),
            AvgPointsPerSale = totalSales == 0 ? 0 : Math.Round((double)pointsEarned / totalSales, 2),
        };

        // Por tienda: agrupar ventas (puntos) por StoreId y unir nombre + canjes.
        var byStoreRaw = await salesQ
            .GroupBy(t => t.StoreId)
            .Select(g => new { StoreId = g.Key, Transactions = g.Count(), Points = g.Sum(x => (long)x.PointsDelta), Amount = g.Sum(x => (decimal?)x.Amount) ?? 0m })
            .ToListAsync(ct);

        var storeIds = byStoreRaw.Where(x => x.StoreId != null).Select(x => x.StoreId!.Value).ToList();
        var storeNames = await _db.Stores.AsNoTracking()
            .Where(s => storeIds.Contains(s.Id))
            .Select(s => new { s.Id, s.Name })
            .ToDictionaryAsync(x => x.Id, x => x.Name, ct);

        // Canjes por tienda en el rango (RedeemedAtStoreId).
        var redeemedByStore = await _db.Coupons.AsNoTracking()
            .Where(c => c.TenantId == tenantId && c.RedeemedUtc != null && c.RedeemedUtc >= from && c.RedeemedUtc < to && c.RedeemedAtStoreId != null)
            .GroupBy(c => c.RedeemedAtStoreId)
            .Select(g => new { StoreId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.StoreId!.Value, x => x.Count, ct);

        var byStore = byStoreRaw
            .Select(x => new AdminStoreReportDto
            {
                StoreId = x.StoreId ?? Guid.Empty,
                StoreName = x.StoreId != null && storeNames.TryGetValue(x.StoreId.Value, out var n) ? n : "Sin tienda",
                Transactions = x.Transactions,
                PointsEarned = x.Points,
                Amount = x.Amount,
                CouponsIssued = 0, // emisión no lleva tienda (se emite por miembro); el canje sí.
                CouponsRedeemed = x.StoreId != null && redeemedByStore.TryGetValue(x.StoreId.Value, out var c) ? c : 0
            })
            .OrderByDescending(x => x.Transactions)
            .ToList();

        // Por día.
        var byDayRaw = await salesQ
            .GroupBy(t => t.CreatedUtc.Date)
            .Select(g => new { Day = g.Key, Transactions = g.Count(), Amount = g.Sum(x => (decimal?)x.Amount) ?? 0m, Points = g.Sum(x => (long)x.PointsDelta) })
            .OrderBy(x => x.Day)
            .ToListAsync(ct);

        var redeemedByDay = await _db.Coupons.AsNoTracking()
            .Where(c => c.TenantId == tenantId && c.RedeemedUtc != null && c.RedeemedUtc >= from && c.RedeemedUtc < to)
            .GroupBy(c => c.RedeemedUtc!.Value.Date)
            .Select(g => new { Day = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Day, x => x.Count, ct);

        var byDay = byDayRaw
            .Select(x => new AdminDailyReportDto
            {
                Day = new DateTimeOffset(x.Day, TimeSpan.Zero),
                Transactions = x.Transactions,
                Amount = x.Amount,
                PointsEarned = x.Points,
                CouponsRedeemed = redeemedByDay.TryGetValue(x.Day, out var c) ? c : 0
            })
            .ToList();

        return new AdminReportDto { Insights = insights, ByStore = byStore, ByDay = byDay };
    }

    /// <summary>CSV del reporte para descargar/abrir en Excel (separador ; por locale es-CL).</summary>
    public async Task<string> ExportReportCsvAsync(Guid tenantId, DateTimeOffset from, DateTimeOffset to, Guid? storeId, CancellationToken ct = default)
    {
        var report = await GetReportAsync(tenantId, from, to, storeId, ct);
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("seccion;clave;transacciones;monto;puntos;cupones_canjeados");
        sb.AppendLine($"insights;total_sales;{report.Insights.TotalSales};0;0;{report.Insights.TotalCouponsRedeemed}");
        sb.AppendLine($"insights;new_members;{report.Insights.NewMembers};0;0;0");
        sb.AppendLine($"insights;coupons_issued;{report.Insights.TotalCouponsIssued};0;0;0");
        foreach (var s in report.ByStore)
            sb.AppendLine($"tienda;{Escape(s.StoreName)};{s.Transactions};{s.Amount};{s.PointsEarned};{s.CouponsRedeemed}");
        foreach (var d in report.ByDay)
            sb.AppendLine($"dia;{d.Day:yyyy-MM-dd};{d.Transactions};{d.Amount};{d.PointsEarned};{d.CouponsRedeemed}");
        return sb.ToString();
    }

    private static string Escape(string v) => v.Contains(';') ? $"\"{v.Replace("\"", "\"\"")}\"" : v;

    private async Task MutateProgramAsync(Guid tenantId, CancellationToken ct, Func<LoyaltyProgram, Task> mutate)
    {
        // POR QUÉ Include ambos navigations: el agregado guarda StampRules/Rewards en listas privadas;
        // para que EF detecte (cascade) los nuevos hijos al agregar vía AddStampRule/AddReward, las
        // colecciones deben estar materializadas y rastreadas — sin Include quedan vacías y el insert no se persiste.
        var program = await _db.LoyaltyPrograms
            .Include(p => p.StampRules)
            .Include(p => p.Rewards)
            .FirstOrDefaultAsync(p => p.TenantId == tenantId, ct)
            ?? throw new DomainException("El tenant no tiene un programa de lealtad configurado.");

        await mutate(program);

        try { await _db.SaveChangesAsync(ct); }
        catch (DbUpdateException ex)
        {
            throw new DomainException($"No se pudo guardar la configuración del programa: {ex.InnerException?.Message ?? ex.Message}");
        }

        // Invalida cache para que la caja tome la nueva config (TTL corto no basta si se repite).
        await _programCache.InvalidateAsync(program.Id, ct);
    }
}