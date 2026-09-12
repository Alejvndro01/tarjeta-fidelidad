namespace Loyalty.Core.Domain.Stores;

/// <summary>
/// Punto de venta (local) de un tenant. Cada local define dónde se registran
/// transacciones de fidelización y dónde se canjean cupones (actor Cajero).
/// </summary>
public sealed class Store : IRequiresTenant
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid TenantId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string? Address { get; private set; }
    public bool IsActive { get; private set; } = true;

    private Store() { }

    public static Store Create(Guid tenantId, string name, string? address = null)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Nombre del local obligatorio.", nameof(name));
        return new Store { TenantId = tenantId, Name = name.Trim(), Address = address };
    }
}