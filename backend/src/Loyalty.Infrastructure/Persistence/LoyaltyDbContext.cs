using System.Linq.Expressions;
using Loyalty.Core.Domain;
using Loyalty.Core.Domain.Coupons;
using Loyalty.Core.Domain.Identity;
using Loyalty.Core.Domain.Loyalty;
using Loyalty.Core.Domain.Members;
using Loyalty.Core.Domain.Security;
using Loyalty.Core.Domain.Stores;
using Loyalty.Core.Domain.Tenants;
using Loyalty.Core.Domain.Transactions;
using Loyalty.Core.Domain.Wallets;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Loyalty.Infrastructure.Persistence;

/// <summary>
/// POR QUÉ AsyncLocal y no un campo Scoped: EF Core cachea el modelo compilado una sola vez por contexto.
/// Si el tenant se capturara en el build del modelo, el filtro quedaría congelado al valor del PRIMER
/// request (fuga de aislamiento entre marcas). Con AsyncLocal el valor se re-lee en CADA query (el filtro
/// global se expansiona por query, no por build), de modo que un único modelo sirve a todos los tenants
/// sin necesidad de cache-key por tenant ni de reconstrucción del modelo.
/// </summary>
public static class AmbientTenantAccessor
{
    private static readonly AsyncLocal<Guid?> _current = new();

    public static Guid? TenantId
    {
        get => _current.Value;
        set => _current.Value = value;
    }

    // Limpieza por unit-of-work para evitar fuga entre requests del mismo thread al reutilizarse.
    public static void Clear() => _current.Value = null;
}

/// <summary>Contrato consumido por el middleware para fijar y limpiar el tenant del request.</summary>
public interface ITenantContext
{
    Guid? TenantId { get; }
}

/// <summary>Puente: expone el tenant actual leyendo del AsyncLocal sin que Api toque la estática directamente.</summary>
public sealed class TenantContext : ITenantContext
{
    public Guid? TenantId => AmbientTenantAccessor.TenantId;
}

/// <summary>DbContext del sistema multi-tenant. Todo query/save de entidades de tenant pasa por filtro global.</summary>
public sealed class LoyaltyDbContext : DbContext
{
    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<LoyaltyProgram> LoyaltyPrograms => Set<LoyaltyProgram>();
    public DbSet<StampRule> StampRules => Set<StampRule>();
    public DbSet<MemberStamp> MemberStamps => Set<MemberStamp>();
    public DbSet<RedemptionReward> RedemptionRewards => Set<RedemptionReward>();
    public DbSet<Member> Members => Set<Member>();
    public DbSet<WalletPass> WalletPasses => Set<WalletPass>();
    public DbSet<Coupon> Coupons => Set<Coupon>();
    public DbSet<Store> Stores => Set<Store>();
    public DbSet<AppUser> AppUsers => Set<AppUser>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<RewardTransaction> RewardTransactions => Set<RewardTransaction>();

    public LoyaltyDbContext(DbContextOptions<LoyaltyDbContext> options)
        : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Índices críticos de concurrencia y búsqueda de caja rápida (evitan N+1 y scans).
        modelBuilder.Entity<Member>(e =>
        {
            e.ToTable("members");
            e.HasIndex(x => new { x.TenantId, x.PhoneNumber }).IsUnique();
            e.HasIndex(x => new { x.TenantId, x.QrHash }).IsUnique();  // índice compuesto monitoreado
            e.Property(x => x.PointsBalance).HasDefaultValue(0);
            e.Property<byte[]>("RowVersion").IsRowVersion(); // concurrency optimista en saldos
        });

        modelBuilder.Entity<Coupon>(e =>
        {
            e.ToTable("coupons");
            e.HasIndex(x => new { x.TenantId, x.CouponCode }).IsUnique();
            e.HasIndex(x => x.Status);
        });

        modelBuilder.Entity<Tenant>(e =>
        {
            e.ToTable("tenants");
            e.HasIndex(x => x.Slug).IsUnique();
            e.Property(x => x.Plan).HasConversion<int>();
        });

        modelBuilder.Entity<AppUser>(e =>
        {
            e.ToTable("app_users");
            e.HasIndex(x => x.Email).IsUnique();
            e.Property(x => x.Role).HasConversion<int>();
        });

        // Índice del refresh token por hash (lookup de rotación/revalidación) — UNIQUE.
        modelBuilder.Entity<RefreshToken>(e =>
        {
            e.ToTable("refresh_tokens");
            e.HasIndex(x => x.TokenHash).IsUnique();
            e.HasIndex(x => x.FamilyId);
        });

        modelBuilder.Entity<WalletPass>(e =>
        {
            e.ToTable("wallet_passes");
            e.Property(x => x.Provider).HasConversion<int>();
            e.Property(x => x.Status).HasConversion<int>();
            e.HasIndex(x => new { x.TenantId, x.MemberId, x.Provider });
        });

        modelBuilder.Entity<LoyaltyProgram>(e =>
        {
            e.ToTable("loyalty_programs");
            e.HasOne<Tenant>().WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Cascade);
        });
        modelBuilder.Entity<StampRule>(e => e.ToTable("stamp_rules"));
        modelBuilder.Entity<MemberStamp>(e =>
        {
            e.ToTable("member_stamps");
            // Índice compuesto para búsqueda/actualización por (tenant, member, sku) en picos de caja.
            e.HasIndex(x => new { x.TenantId, x.MemberId, x.ProductSku }).IsUnique();
        });
        modelBuilder.Entity<RedemptionReward>(e => e.ToTable("redemption_rewards"));
        modelBuilder.Entity<Store>(e => { e.ToTable("stores"); e.HasIndex(x => new { x.TenantId, x.Name }).IsUnique(); });
        modelBuilder.Entity<RewardTransaction>(e =>
        {
            e.ToTable("reward_transactions");
            e.HasIndex(x => new { x.TenantId, x.CreatedUtc });
            e.HasIndex(x => new { x.TenantId, x.MemberId, x.CreatedUtc });
        });

        ApplyGlobalTenantFilters(modelBuilder);
    }

    /// <summary>
    /// Se compila UNA vez en el modelo, pero el predicado lee AmbientTenantAccessor.TenantId en cada
    /// query (EF expansiona los filtros por query, no por build). tenant==null → sin aislamiento
    /// (registro público / migraciones); tenant definido → aísla esa marca.
    /// </summary>
    private static void ApplyGlobalTenantFilters(ModelBuilder modelBuilder)
    {
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            var clrType = entityType.ClrType;
            if (!typeof(IRequiresTenant).IsAssignableFrom(clrType)) continue;
            if (entityType.FindProperty("TenantId") is not IProperty tenantProp) continue;

            var param = Expression.Parameter(clrType, "e");
            var tenantIdExpr = Expression.Convert(
                Expression.Property(param, "TenantId"), typeof(Guid?)); // Guid → Guid? para comparación con el accessor
            var currentTenant = Expression.Property(null, typeof(AmbientTenantAccessor), "TenantId");
            // (e => tenant == null ? true : e.TenantId == tenant)
            var filter = Expression.Lambda(
                Expression.Condition(
                    Expression.Equal(currentTenant, Expression.Constant(null, typeof(Guid?))),
                    Expression.Constant(true),
                    Expression.Equal(tenantIdExpr, currentTenant)),
                param);

            entityType.SetQueryFilter(filter);
        }
    }
}