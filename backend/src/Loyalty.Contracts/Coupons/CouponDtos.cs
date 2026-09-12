namespace Loyalty.Contracts.Coupons;

/// <summary>Cupón activo visible para el miembro (PWA).</summary>
public sealed record MemberCouponDto
{
    public Guid CouponId { get; init; }
    public string CouponCode { get; init; } = "";
    public string Title { get; init; } = "";
    public string? Description { get; init; }
    public DateTimeOffset IssuedUtc { get; init; }
    public DateTimeOffset? ExpiresUtc { get; init; }
    public string Status { get; init; } = "";
    public Guid? SourcePassId { get; init; }
    public string? SourceProvider { get; init; }
}

/// <summary>Resultado del canje de un cupón en caja.</summary>
public sealed record CouponRedeemResultDto
{
    public Guid CouponId { get; init; }
    public string CouponCode { get; init; } = "";
    public string Title { get; init; } = "";
    public bool Redeemed { get; init; }
    public DateTimeOffset RedeemedUtc { get; init; }
    public Guid RedeemedByUserId { get; init; }
    public Guid StoreId { get; init; }
    public string Message { get; init; } = "";
}

/// <summary>Request del panel de cajero para canjear un cupón por código.</summary>
public sealed record RedeemCouponRequestDto
{
    public string CouponCode { get; init; } = "";
    public Guid StoreId { get; init; }
}

/// <summary>Registro de un cupón ya canjeado (historial de auditoría del panel de cajero).</summary>
public sealed record RedeemedCouponDto
{
    public Guid CouponId { get; init; }
    public string CouponCode { get; init; } = "";
    public string Title { get; init; } = "";
    public DateTimeOffset RedeemedUtc { get; init; }
    public Guid? RedeemedByUserId { get; init; }
    public string? RedeemedByEmail { get; init; }
    public Guid? StoreId { get; init; }
    public string? StoreName { get; init; }
}

/// <summary>Una tienda del tenant (selector del panel de cajero).</summary>
public sealed record StoreOptionDto
{
    public Guid StoreId { get; init; }
    public string Name { get; init; } = "";
}