namespace Loyalty.Core.Domain.Security;

/// <summary>
/// Opciones tipadas del JWT, ligadas a la sección "Jwt" de appsettings.json.
/// Nota de seguridad: en producción la SecretKey debe venir del secret store (env),
/// nunca del appsettings fuente. Documentado como tal para no ignorarlo.
/// </summary>
public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = "loyalty-api";
    public string Audience { get; set; } = "loyalty-pwa";
    /// <summary>Clave HMAC-SHA256 ≥ 32 bytes. [REDACTED en producción]</summary>
    public string SecretKey { get; set; } = string.Empty;
    public int AccessTokenMinutes { get; set; } = 60;
    /// <summary>Vida del refresh token en minutos (10080 = 7 días).</summary>
    public int RefreshTokenMinutes { get; set; } = 10080;
}