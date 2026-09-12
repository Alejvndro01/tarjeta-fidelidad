namespace Loyalty.Core.Domain.Loyalty;

/// <summary>
/// Configuración de lealtad por tenant
/// </summary>
public sealed class LoyaltyProgram : IRequiresTenant
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid TenantId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public bool PointsEnabled { get; private set; }
    public bool StampsEnabled { get; private set; }
    public int PointsPerMonetaryUnit { get; private set; } = 1; // puntos ganados por cada unidad monetaria base
    public string CurrencyCode { get; private set; } = "CLP";   // unidad monetaria base del tenant

    /// <summary>Exposición de lectura para caja rápida &lt;200ms sin proyectar navegación al agregado entero.</summary>
    public IReadOnlyCollection<StampRule> StampRules => _stampRules;
    private List<StampRule> _stampRules = new();

    public IReadOnlyCollection<RedemptionReward> Rewards => _rewards;
    private List<RedemptionReward> _rewards = new();

    private LoyaltyProgram() { }

    /// <summary>
    /// Fábrica pública: única vía de crear/configurar el programa híbrido de un tenant.
    /// Define si el tenant usa puntos, sellos o ambos, y el factor de conversión de puntos.
    /// </summary>
    public static LoyaltyProgram Create(Guid tenantId, string name, bool pointsEnabled, bool stampsEnabled,
        int pointsPerMonetaryUnit, string currencyCode = "CLP")
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Nombre del programa obligatorio.", nameof(name));
        if (pointsPerMonetaryUnit <= 0) throw new ArgumentOutOfRangeException(nameof(pointsPerMonetaryUnit));

        return new LoyaltyProgram
        {
            TenantId = tenantId,
            Name = name.Trim(),
            PointsEnabled = pointsEnabled,
            StampsEnabled = stampsEnabled,
            PointsPerMonetaryUnit = pointsPerMonetaryUnit,
            CurrencyCode = currencyCode
        };
    }

    /// <summary>Agrega una regla de sellos para un SKU específico del programa.</summary>
    public void AddStampRule(Guid tenantId, string productSku, string productName, int stampsRequired)
    {
        if (tenantId != TenantId) throw new InvalidOperationException("La regla pertenece a otro tenant.");
        if (string.IsNullOrWhiteSpace(productSku)) throw new ArgumentException("SKU obligatorio.", nameof(productSku));
        if (stampsRequired <= 0) throw new ArgumentOutOfRangeException(nameof(stampsRequired));

        _stampRules.Add(StampRule.Create(Id, productSku, productName, stampsRequired));
    }

    /// <summary>Actualiza la configuración de puntos + banderas híbridas desde el panel admin (cache invalidada al persistir).</summary>
    public void UpdatePointsConfig(int pointsPerMonetaryUnit, bool pointsEnabled, bool stampsEnabled)
    {
        if (pointsPerMonetaryUnit <= 0) throw new ArgumentOutOfRangeException(nameof(pointsPerMonetaryUnit));
        PointsPerMonetaryUnit = pointsPerMonetaryUnit;
        PointsEnabled = pointsEnabled;
        StampsEnabled = stampsEnabled;
    }

    /// <summary>Agrega una recompensa canjeable (por puntos, sellos o ambos) desde el panel admin.</summary>
    public void AddReward(Guid tenantId, string name, string? description, int pointsCost, int stampCost)
    {
        if (tenantId != TenantId) throw new InvalidOperationException("La recompensa pertenece a otro tenant.");
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Nombre obligatorio.", nameof(name));
        if (pointsCost < 0) throw new ArgumentOutOfRangeException(nameof(pointsCost));
        if (stampCost < 0) throw new ArgumentOutOfRangeException(nameof(stampCost));
        if (pointsCost == 0 && stampCost == 0) throw new ArgumentException("Defina costo en puntos y/o sellos.", nameof(pointsCost));

        _rewards.Add(RedemptionReward.Create(Id, name, description, pointsCost, stampCost));
    }
}

/// <summary>
/// Regla de sellos: un producto específico (por SKU del tenant) acumula un sello
/// por unidad comprada hasta completar el umbral y canjear la recompensa.
/// </summary>
public sealed class StampRule
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid LoyaltyProgramId { get; private set; }
    public string ProductSku { get; private set; } = string.Empty;
    public string ProductName { get; private set; } = string.Empty;
    public int StampsRequired { get; private set; }

    private StampRule() { } // EF Core

    /// <summary>Fábrica usada por el agregado LoyaltyProgram; los invariantes se validan en el padre.</summary>
    internal static StampRule Create(Guid loyaltyProgramId, string productSku, string productName, int stampsRequired)
        => new()
        {
            LoyaltyProgramId = loyaltyProgramId,
            ProductSku = productSku,
            ProductName = productName,
            StampsRequired = stampsRequired
        };
}

/// <summary>
/// Recompensa canjeable por puntos o sellos. Se materializa como cupón emitido
/// al wallet del usuario (véase WalletPass) cuando se canjea.
/// </summary>
public sealed class RedemptionReward
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid LoyaltyProgramId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public int PointsCost { get; private set; }    // costo en puntos (0 si aplica solo sellos)
        public int StampCost { get; private set; }     // costo en sellos (0 si aplica solo puntos)
        public bool IsActive { get; private set; } = true;

        private RedemptionReward() { } // EF Core

        /// <summary>Fábrica interna usada por el agregado LoyaltyProgram; los invariantes se validan en el padre.</summary>
        internal static RedemptionReward Create(Guid loyaltyProgramId, string name, string? description, int pointsCost, int stampCost)
            => new()
            {
                LoyaltyProgramId = loyaltyProgramId,
                Name = name,
                Description = description,
                PointsCost = pointsCost,
                StampCost = stampCost
            };
    }