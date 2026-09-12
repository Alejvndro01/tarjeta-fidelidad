namespace Loyalty.Core.Domain.Wallets;

/// <summary>
/// Pase digitalemitido para Apple Wallet (.pkpass) o Google Wallet. En la Fase 1
/// modelamos la entidad y su ciclo de vida; la firma/push/vigencia real se implementa
/// en la Fase 5 (módulo Wallet Engine).
/// </summary>
public sealed class WalletPass : IRequiresTenant
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid TenantId { get; private set; }
    public Guid MemberId { get; private set; }

    public WalletProvider Provider { get; private set; }
    public string PassIdentifier { get; private set; } = string.Empty;  // idem passTypeIdentifier (Apple) / objectId (Google)
    public string? SerialNumber { get; private set; }
    public string? PassToken { get; private set; }                       // token de actualización push
    public PassStatus Status { get; private set; } = PassStatus.Active;

    /// <summary>Última versión firmada enviada al wallet; la actualización push la incrementa.</summary>
    public int Revision { get; private set; }
    public DateTimeOffset? LastUpdatedUtc { get; private set; }

    private WalletPass() { }

    public static WalletPass Create(Guid tenantId, Guid memberId, WalletProvider provider,
        string passIdentifier, string? serialNumber, string? passToken)
    {
        return new WalletPass
        {
            TenantId = tenantId,
            MemberId = memberId,
            Provider = provider,
            PassIdentifier = passIdentifier,
            SerialNumber = serialNumber,
            PassToken = passToken,
            Revision = 0,
            LastUpdatedUtc = DateTimeOffset.UtcNow
        };
    }

    public void MarkUpdated()
    {
        Revision++;
        LastUpdatedUtc = DateTimeOffset.UtcNow;
    }

    public void MarkExpired() => Status = PassStatus.Expired;
    public void MarkTerminated() => Status = PassStatus.Terminated;
}

public enum WalletProvider { ApplePassKit = 1, GoogleWallet = 2 }
public enum PassStatus { Active = 1, Expired = 2, Terminated = 3 }