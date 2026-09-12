namespace Loyalty.Contracts.Loyalty;

/// <summary>Ítem de una venta en caja: precio pagado y/o producto que aplica sellos.</summary>
public sealed record SaleLineItemDto
{
    public string ProductSku { get; init; } = string.Empty;
    public string? ProductName { get; init; }
    public int Quantity { get; init; } = 1;
    public decimal UnitPrice { get; init; }
}

/// <summary>Resultado del procesamiento híbrido de una venta: puntos + sellos + recompensa completada.</summary>
public sealed record SaleProcessingResult
{
    public Guid MemberId { get; init; }
    public int PointsEarned { get; init; }
    public int PointsBalance { get; init; }
    public IReadOnlyList<StampEarnedDto> StampsEarned { get; init; } = Array.Empty<StampEarnedDto>();
    public IReadOnlyList<StampCompletedDto> StampsCompleted { get; init; } = Array.Empty<StampCompletedDto>();
    public Guid? RewardId { get; init; }
    public bool RewardCompleted { get; init; }
    public IReadOnlyList<string> CouponsIssued { get; init; } = Array.Empty<string>();
    public decimal TotalAmount { get; init; }
}

public sealed record StampEarnedDto
{
    public string ProductSku { get; init; } = string.Empty;
    public int Count { get; init; }
    public int Total { get; init; }
}

public sealed record StampCompletedDto
{
    public string ProductSku { get; init; } = string.Empty;
    public string StampRuleId { get; init; } = string.Empty;
}