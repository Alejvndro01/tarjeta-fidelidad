using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Loyalty.Application.Loyalty;
using Loyalty.Contracts.Loyalty;
using Loyalty.Core.Domain;
using Loyalty.Core.Domain.Loyalty;
using Loyalty.Core.Domain.Members;
using Loyalty.Core.Domain.Tenants;
using Loyalty.Infrastructure.Loyalty;
using Loyalty.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Loyalty.IntegrationTests;

/// <summary>
/// Test de integración del modelo híbrido contra PostgreSQL real (docker-compose, D B loyalty_test).
/// Verifica ACID: puntos + sellos + auditoría se persisten juntos, y la idempotencia por reference.
/// </summary>
public class SaleProcessingIntegrationTests
{
    private const string Conn =
        "Host=127.0.0.1;Port=5433;Database=loyalty_test;Username=loyalty;Password=loyalty_dev;SslMode=Disable";

    private static LoyaltyDbContext NewDb() =>
        new(new DbContextOptionsBuilder<LoyaltyDbContext>().UseNpgsql(Conn).Options);

    // El cache de programa real vive en Redis; los tests de integración validan el camino ACID,
    // así que un doble de test cargando el modelo vivo desde la BD basta (sin Redis).
    private static SaleProcessingService NewSaleService(LoyaltyDbContext db) =>
        new(db, new FakeProgramCache(db));

    private sealed class FakeProgramCache : IProgramCacheService
    {
        private readonly LoyaltyDbContext _db;
        public FakeProgramCache(LoyaltyDbContext db) => _db = db;

        public async Task<LoyaltyProgramCacheModel?> GetAsync(Guid programId, CancellationToken ct = default)
        {
            return await _db.LoyaltyPrograms
                .AsNoTracking()
                .Where(p => p.Id == programId)
                .Select(p => new LoyaltyProgramCacheModel
                {
                    Id = p.Id,
                    TenantId = p.TenantId,
                    PointsEnabled = p.PointsEnabled,
                    StampsEnabled = p.StampsEnabled,
                    PointsPerMonetaryUnit = p.PointsPerMonetaryUnit,
                    StampRules = p.StampRules.Select(r => new StampRuleCacheModel
                    {
                        Id = r.Id, ProductSku = r.ProductSku, ProductName = r.ProductName, StampsRequired = r.StampsRequired
                    }).ToList(),
                    Rewards = p.Rewards.Select(r => new RewardCacheModel
                    {
                        Id = r.Id, Name = r.Name, PointsCost = r.PointsCost, StampCost = r.StampCost, IsActive = r.IsActive
                    }).ToList()
                })
                .FirstOrDefaultAsync(ct);
        }

        public Task SetAsync(LoyaltyProgramCacheModel p, CancellationToken ct = default) => Task.CompletedTask;
        public Task InvalidateAsync(Guid programId, CancellationToken ct = default) => Task.CompletedTask;
    }

    private static async Task<(Guid tenantId, Guid memberId)> SeedAsync(LoyaltyDbContext db, string phone)
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var tenant = Tenant.Create("Test-" + suffix, "marca-test-" + suffix, TenantPlan.Growth);
        db.Tenants.Add(tenant);
        await db.SaveChangesAsync();

        var program = LoyaltyProgram.Create(tenant.Id, "Híbrido Test", pointsEnabled: true, stampsEnabled: true,
            pointsPerMonetaryUnit: 1, currencyCode: "CLP");
        program.AddStampRule(tenant.Id, "SKU-BURGER", "Combo Burger", stampsRequired: 3);
        db.LoyaltyPrograms.Add(program);
        await db.SaveChangesAsync();

        var member = Member.Create(tenant.Id, program.Id, phone, "Usuario Test", null, "qr-" + phone, "secret");
        db.Members.Add(member);
        await db.SaveChangesAsync();

        return (tenant.Id, member.Id);
    }

    [Fact]
    public async Task ProcessSale_AwardsPoints_AndStamps_Atomically()
    {
        await using var db = NewDb();
        var (tenantId, memberId) = await SeedAsync(db, "5699123456");

        var svc = NewSaleService(db);
        var items = new[]
        {
            new SaleLineItemDto { ProductSku = "SKU-BURGER", Quantity = 3, UnitPrice = 1000m }, // 3 sellos
            new SaleLineItemDto { ProductSku = "SKU-OTRO", Quantity = 1, UnitPrice = 2000m }     // sin sello
        };
        const decimal total = 5000m;

        var result = await svc.ProcessSaleAsync(tenantId, memberId, null, items, total, "REF-1");

        Assert.Equal(5000, result.PointsEarned);         // 5000 * PointsPerMonetaryUnit(=1)
        Assert.Single(result.StampsEarned);              // solo SKU-BURGER
        Assert.Equal(3, result.StampsEarned[0].Total);   // 3 sellos acumulados

        // Persistencia dentro de la misma transacción (ACID).
        var stamped = await db.MemberStamps.SingleAsync(s => s.MemberId == memberId && s.ProductSku == "SKU-BURGER");
        Assert.Equal(3, stamped.Count);

        var audit = await db.RewardTransactions.CountAsync(t => t.MemberId == memberId && t.Reference == "REF-1");
        Assert.Equal(1, audit);

        var reloaded = await db.Members.SingleAsync(m => m.Id == memberId);
        Assert.Equal(5000, reloaded.PointsBalance);
    }

    [Fact]
    public async Task ProcessSale_DuplicateReference_Throws_NoDoubleAward()
    {
        await using var db = NewDb();
        var (tenantId, memberId) = await SeedAsync(db, "5699988776");

        var svc = NewSaleService(db);
        var items = new[] { new SaleLineItemDto { ProductSku = "SKU-BURGER", Quantity = 1, UnitPrice = 500m } };

        await svc.ProcessSaleAsync(tenantId, memberId, null, items, 500m, "REF-SAME");
        await Assert.ThrowsAsync<DomainException>(() =>
            svc.ProcessSaleAsync(tenantId, memberId, null, items, 500m, "REF-SAME"));

        var audit = await db.RewardTransactions.CountAsync(t => t.MemberId == memberId && t.Reference == "REF-SAME");
        Assert.Equal(1, audit); // no se duplicó ni los puntos ni los sellos
    }
}