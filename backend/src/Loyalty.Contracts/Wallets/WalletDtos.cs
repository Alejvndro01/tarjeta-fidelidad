namespace Loyalty.Contracts.Wallets;

/// <summary>Resultado de solicitar un pase. Para Apple trae el binario .pkpass; para Google el JWT firmado.</summary>
public sealed record WalletPassResultDto
{
    public string Provider { get; init; } = "";            // "apple" | "google"
    public byte[]? PkpassBytes { get; init; }               // Apple: contenido del .pkpass (zip firmado)
    public string? FileName { get; init; }                  // Apple: nombre de descarga
    public string? MediaType { get; init; }                 // Apple: application/vnd.apple.pkpass
    public string? SignedJwt { get; init; }                 // Google: saved JWT para "Add to Wallet"
    public string? GoogleAddUrl { get; init; }              // Google: https://pay.google.com/gp/v/save/...
}

/// <summary>Perfil de marca mínimo que necesita el pase (grueso en el cliente).</summary>
public sealed record WalletBrandDto
{
    public string OrganizationName { get; init; } = "";
    public string? Description { get; init; }
    public string? LogoUrl { get; init; }
    public string? PrimaryColor { get; init; }
}