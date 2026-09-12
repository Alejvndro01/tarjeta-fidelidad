namespace Loyalty.Contracts.Auth;

/// <summary>Solicitud de inicio de sesión del staff (Cajero/TenantAdmin/SuperAdmin).</summary>
public sealed record LoginRequestDto
{
    public string Email { get; init; } = string.Empty;
    public string Password { get; init; } = string.Empty;
}

/// <summary>Resultado exitoso de autenticación: access token corto + refresh token rotativo de sesión.</summary>
public sealed record AuthResponseDto
{
    public string AccessToken { get; init; } = string.Empty;
    public string RefreshToken { get; init; } = string.Empty;
    public int AccessTokenExpiresInSeconds { get; init; }
    public int RefreshTokenExpiresInSeconds { get; init; }
    public string TokenType { get; init; } = "Bearer";
    public AppUserInfoDto User { get; init; } = new();
}

/// <summary>Datos del usuario autenticado incluyendo su rol y tenant (para el frontend móvil/RBAC).</summary>
public sealed record AppUserInfoDto
{
    public Guid Id { get; init; }
    public string Email { get; init; } = string.Empty;
    public string Role { get; init; } = string.Empty;
    public Guid? TenantId { get; init; }
    public Guid? StoreId { get; init; }
}

/// <summary>Rotación de sesión: true acceso con un refresh token anterior.</summary>
public sealed record RefreshTokenRequestDto
{
    public string RefreshToken { get; init; } = string.Empty;
}

/// <summary>Solicitud de cierre de sesión: revoca el refresh token (y opcionalmente la familia).</summary>
public sealed record LogoutRequestDto
{
    public string RefreshToken { get; init; } = string.Empty;
}

/// <summary>Respuesta de "quién soy" para el panel/STAFF y para validar el token en el frontend.</summary>
public sealed record MeResponseDto
{
    public AppUserInfoDto User { get; init; } = new();
}