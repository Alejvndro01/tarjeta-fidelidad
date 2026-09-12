namespace Loyalty.Contracts.Members;

/// <summary>Registro de un consumidor (miembro) en un tenant. El correo es opcional (fricción mínima).</summary>
public sealed record RegisterMemberRequestDto
{
    public string PhoneNumber { get; init; } = string.Empty;
    public string FullName { get; init; } = string.Empty;
    public string? Email { get; init; }
    /// <summary>Slug del tenant (ej. "burger-demo") al que se registra.</summary>
    public string TenantSlug { get; init; } = string.Empty;
}

/// <summary>Respuesta de registro: emite el token de acceso del miembro + datos básicos y su QR.</summary>
public sealed record RegisterMemberResponseDto
{
    public string AccessToken { get; init; } = string.Empty;
    public int AccessTokenExpiresInSeconds { get; init; }
    public MemberProfileDto Member { get; init; } = new();
}

/// <summary>Perfil del consumidor consumido por la PWA (saldo, sellos, QR).</summary>
public sealed record MemberProfileDto
{
    public Guid MemberId { get; init; }
    public string PhoneNumber { get; init; } = string.Empty;
    public string FullName { get; init; } = string.Empty;
    public string? Email { get; init; }
    public int PointsBalance { get; init; }
    public string QrHash { get; init; } = string.Empty;
    public string TenantSlug { get; init; } = string.Empty;
    public IReadOnlyList<MemberStampDto> Stamps { get; init; } = Array.Empty<MemberStampDto>();
}

/// <summary>Sello acumulado por SKU (para la pantalla de "mis sellos").</summary>
public sealed record MemberStampDto
{
    public string ProductSku { get; init; } = string.Empty;
    public string ProductName { get; init; } = string.Empty;
    public int Count { get; init; }
    public int StampsRequired { get; init; }
    /// <summary>true si Count >= StampsRequired (hito completado).</summary>
    public bool Completed { get; init; }
}