namespace Loyalty.Contracts.Admin;

/// <summary>Resumen de KPIs del tenant para el dashboard admin.</summary>
public sealed record AdminDashboardDto
{
    public int MemberCount { get; init; }
    public int ActiveCoupons { get; init; }
    public int RedeemedCoupons { get; init; }
    public long TotalPointsIssued { get; init; }
    public long TotalPointsRedeemed { get; init; }
}

/// <summary>Fila de miembro del panel de administración.</summary>
public sealed record AdminMemberDto
{
    public Guid MemberId { get; init; }
    public string QrHash { get; init; } = "";
    public string PhoneNumber { get; init; } = "";
    public string FullName { get; init; } = "";
    public string? Email { get; init; }
    public int PointsBalance { get; init; }
    public DateTimeOffset JoinedAtUtc { get; init; }
}

/// <summary>Fila de cupón (emitido/canjeado) vista por el admin.</summary>
public sealed record AdminCouponDto
{
    public Guid CouponId { get; init; }
    public string CouponCode { get; init; } = "";
    public string Title { get; init; } = "";
    public string Status { get; init; } = "";
    public Guid MemberId { get; init; }
    public string? MemberName { get; init; }
    public string? MemberPhone { get; init; }
    public DateTimeOffset IssuedUtc { get; init; }
    public DateTimeOffset? ExpiresUtc { get; init; }
    public DateTimeOffset? RedeemedUtc { get; init; }
    public string? StoreName { get; init; }
}

/// <summary>Programa de lealtad completo para la pestaña de configuración del panel.</summary>
public sealed record AdminProgramDto
{
    public Guid ProgramId { get; init; }
    public string Name { get; init; } = "";
    public bool PointsEnabled { get; init; }
    public bool StampsEnabled { get; init; }
    public int PointsPerMonetaryUnit { get; init; }
    public string CurrencyCode { get; init; } = "CLP";
    public IReadOnlyList<AdminStampRuleDto> StampRules { get; init; } = Array.Empty<AdminStampRuleDto>();
    public IReadOnlyList<AdminRewardDto> Rewards { get; init; } = Array.Empty<AdminRewardDto>();
}

/// <summary>Regla de sellos del programa (config).</summary>
public sealed record AdminStampRuleDto
{
    public Guid RuleId { get; init; }
    public string ProductSku { get; init; } = "";
    public string ProductName { get; init; } = "";
    public int StampsRequired { get; init; }
}

/// <summary>Recompensa canjeable del programa (config).</summary>
public sealed record AdminRewardDto
{
    public Guid RewardId { get; init; }
    public string Name { get; init; } = "";
    public string? Description { get; init; }
    public int PointsCost { get; init; }
    public int StampCost { get; init; }
    public bool IsActive { get; init; }
}

/// <summary>Request para actualizar la config de puntos del programa.</summary>
public sealed record UpdateProgramPointsRequest
{
    public bool PointsEnabled { get; init; }
    public bool StampsEnabled { get; init; }
    public int PointsPerMonetaryUnit { get; init; } = 1;
}

/// <summary>Request para agregar una regla de sellos.</summary>
public sealed record AddStampRuleRequest
{
    public string ProductSku { get; init; } = "";
    public string ProductName { get; init; } = "";
    public int StampsRequired { get; init; }
}

/// <summary>Request para agregar una recompensa canjeable.</summary>
public sealed record AddRewardRequest
{
    public string Name { get; init; } = "";
    public string? Description { get; init; }
    public int PointsCost { get; init; }
    public int StampCost { get; init; }
}

/// <summary>Crea manualmente un cupón adicional para un miembro (campaña/bonificación).</summary>
public sealed record CreateCouponRequest
{
    public Guid MemberId { get; init; }
    public string Title { get; init; } = "";
    public string? Description { get; init; }
    public DateTimeOffset? ExpiresUtc { get; init; }
}

/// <summary>Resultado de crear un cupón manualmente.</summary>
public sealed record CreateCouponResultDto
{
    public Guid CouponId { get; init; }
    public string CouponCode { get; init; } = "";
    public string Title { get; init; } = "";
    public DateTimeOffset? ExpiresUtc { get; init; }
}

/// <summary>Registro de una transacción de fidelización (reporte).</summary>
public sealed record AdminTransactionDto
{
    public Guid TransactionId { get; init; }
    public Guid MemberId { get; init; }
    public string? MemberName { get; init; }
    public string Type { get; init; } = "";
    public int PointsDelta { get; init; }
    public int StampsDelta { get; init; }
    public decimal? Amount { get; init; }
    public string Reference { get; init; } = "";
    public string? StoreName { get; init; }
    public DateTimeOffset CreatedUtc { get; init; }
}

// --- Fase 10: Reportes / Insights ---

/// <summary>Resumen agregado por tienda dentro de un rango (PuntosEarned = ventas que dieron puntos).</summary>
public sealed record AdminStoreReportDto
{
    public Guid StoreId { get; init; }
    public string StoreName { get; init; } = "";
    public int Transactions { get; init; }
    public long PointsEarned { get; init; }
    public decimal Amount { get; init; }
    public int CouponsIssued { get; init; }
    public int CouponsRedeemed { get; init; }
}

/// <summary>Resumen diario agregado dentro de un rango.</summary>
public sealed record AdminDailyReportDto
{
    public DateTimeOffset Day { get; init; }
    public int Transactions { get; init; }
    public decimal Amount { get; init; }
    public long PointsEarned { get; init; }
    public int CouponsRedeemed { get; init; }
}

/// <summary>Métricas de conversión y membresía del rango.</summary>
public sealed record AdminInsightsDto
{
    public int NewMembers { get; init; }
    public int ActiveRedemptions { get; init; }
    public decimal GrossRedemptionRate { get; init; }   // cupones canjeados / (emitidos+canjeados) en rango
    public double AvgPointsPerSale { get; init; }
    public int TotalSales { get; init; }
    public int TotalCouponsIssued { get; init; }
    public int TotalCouponsRedeemed { get; init; }
    public int TotalNewMembers { get; init; }
}

/// <summary>
/// Reporte completo del panel admin para un rango: métricas globales + por tienda + por día.
/// Sí se necesita el "snapshot": el panel carga una sola respuesta para pintar los 3 bloques.
/// </summary>
public sealed record AdminReportDto
{
    public AdminInsightsDto Insights { get; init; } = new();
    public IReadOnlyList<AdminStoreReportDto> ByStore { get; init; } = Array.Empty<AdminStoreReportDto>();
    public IReadOnlyList<AdminDailyReportDto> ByDay { get; init; } = Array.Empty<AdminDailyReportDto>();
}

/// <summary>Request para reporte con rango de fechas (bounds opcionales).</summary>
public sealed record ReportRangeRequest
{
    // Filtro por transacciones en rango. Nullable → el servicio aplica un default (últimos 30 días).
    public DateTimeOffset? From { get; init; }
    public DateTimeOffset? To { get; init; }
    public Guid? StoreId { get; init; }  // opcional: filtrar por tienda
}