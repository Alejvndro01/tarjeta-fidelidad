using Loyalty.Contracts.Wallets;

namespace Loyalty.Application.Wallets;

/// <summary>
/// Genera pases de fidelidad para Apple Wallet (.pkpass firmado) y Google Wallet (JWT).
/// Ruta de producción real documentada: los certificados comerciales (Apple .p12 + WWDR /
/// service-account de Google) sustituyen al material de desarrollo local sin tocar los contratos.
/// </summary>
public interface IWalletPassService
{
    /// <summary>Genera y persiste (tracking) un pase para el miembro en el proveedor indicado.</summary>
    Task<WalletPassResultDto> GeneratePassAsync(Guid memberId, string provider, CancellationToken ct = default);

    /// <summary>Re-vigora la firma/token de un pase ya existente (updates de saldo/sellos).</summary>
    Task<WalletPassResultDto> RefreshPassAsync(Guid memberId, string provider, CancellationToken ct = default);
}

public static class WalletProviderNames
{
    public const string Apple = "apple";
    public const string Google = "google";
}