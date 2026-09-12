using System.Text.Json;
using System.Text.Json.Serialization;
using Loyalty.Application.Loyalty;
using Loyalty.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;

namespace Loyalty.Infrastructure.Loyalty;

/// <summary>
/// Implementación cache-aside del programa de lealtad sobre Redis.
/// POR QUÉ TTL corto (30s) y no invalidación por evento: los programas se editan muy rara vez
/// (config por tenant) y NO desde Punto de Venta; un TTL corto limita el staleness a ~30s lo que
/// es aceptable operativamente y evita introducir un bus de invalidación completo para ello.
/// El saldo/sellos del miembro nunca pasan por aquí: se leen vivos de PostgreSQL (ACID).
/// </summary>
public sealed class ProgramCacheService : IProgramCacheService
{
    private readonly IDistributedCache _cache;
    private readonly LoyaltyDbContext _db;

    private static readonly TimeSpan Ttl = TimeSpan.FromSeconds(30);
    private const string KeyPrefix = "prog:";

    // JsonStringEnumConverter: nombres estables del enum al JSON (no romper cache al renombrar).
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() }
    };

    public ProgramCacheService(IDistributedCache cache, LoyaltyDbContext db)
    {
        _cache = cache;
        _db = db;
    }

    public async Task<LoyaltyProgramCacheModel?> GetAsync(Guid programId, CancellationToken ct = default)
    {
        var key = KeyPrefix + programId;
        var cached = await _cache.GetStringAsync(key, ct);
        if (cached != null)
        {
            var hit = JsonSerializer.Deserialize<LoyaltyProgramCacheModel>(cached, JsonOpts);
            if (hit != null) return hit;
        }

        var program = await _db.LoyaltyPrograms
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
                    Id = r.Id,
                    ProductSku = r.ProductSku,
                    ProductName = r.ProductName,
                    StampsRequired = r.StampsRequired
                }).ToList(),
                Rewards = p.Rewards.Select(r => new RewardCacheModel
                {
                    Id = r.Id,
                    Name = r.Name,
                    PointsCost = r.PointsCost,
                    StampCost = r.StampCost,
                    IsActive = r.IsActive
                }).ToList()
            })
            .FirstOrDefaultAsync(ct);

        if (program == null) return null;

        await _cache.SetStringAsync(key, JsonSerializer.Serialize(program, JsonOpts),
            new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = Ttl }, ct);
        return program;
    }

    public Task SetAsync(LoyaltyProgramCacheModel program, CancellationToken ct = default)
    {
        if (program == null) return Task.CompletedTask;
        return _cache.SetStringAsync(KeyPrefix + program.Id, JsonSerializer.Serialize(program, JsonOpts),
            new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = Ttl }, ct);
    }

    public Task InvalidateAsync(Guid programId, CancellationToken ct = default)
        => _cache.RemoveAsync(KeyPrefix + programId, ct);
}