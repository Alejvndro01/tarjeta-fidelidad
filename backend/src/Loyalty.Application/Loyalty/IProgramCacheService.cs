using Loyalty.Application.Loyalty;

namespace Loyalty.Application.Loyalty;

/// <summary>Regla de sellos plana (serializable) para el hot path de caja.</summary>
public sealed record StampRuleCacheModel
{
    public Guid Id { get; init; }
    public string ProductSku { get; init; } = "";
    public string ProductName { get; init; } = "";
    public int StampsRequired { get; init; }
}

/// <summary>Recompensa canjeable plana para decidir el auto-cupón en caja.</summary>
public sealed record RewardCacheModel
{
    public Guid Id { get; init; }
    public string Name { get; init; } = "";
    public int PointsCost { get; init; }
    public int StampCost { get; init; }
    public bool IsActive { get; init; }
}

/// <summary>
/// Lectura inmutable del programa de lealtad para procesar ventas &lt;200ms sin re-fetchear
/// la configuración desde PostgreSQL en cada scan. El saldo/sellos del miembro SIEMPRE se
/// leen vivos de la BD (ACID); solo esta configuración (cambia rara vez) se sirve desde cache.
/// </summary>
public sealed record LoyaltyProgramCacheModel
{
    public Guid Id { get; init; }
    public Guid TenantId { get; init; }
    public bool PointsEnabled { get; init; }
    public bool StampsEnabled { get; init; }
    public int PointsPerMonetaryUnit { get; init; }
    public IReadOnlyList<StampRuleCacheModel> StampRules { get; init; } = Array.Empty<StampRuleCacheModel>();
    public IReadOnlyList<RewardCacheModel> Rewards { get; init; } = Array.Empty<RewardCacheModel>();
}

/// <summary>Caché Redis del programa de lealtad (config by tenant, cambia rara vez).</summary>
public interface IProgramCacheService
{
    Task<LoyaltyProgramCacheModel?> GetAsync(Guid programId, CancellationToken ct = default);
    Task SetAsync(LoyaltyProgramCacheModel program, CancellationToken ct = default);
    Task InvalidateAsync(Guid programId, CancellationToken ct = default);
}