using Loyalty.Contracts.Admin;

namespace Loyalty.Application.Admin;

/// <summary>
/// Panel de administración (TenantAdmin/SuperAdmin): KPIs, gestión de miembros,
/// cupones manuales, configuración del programa de lealtad y auditoría de transacciones.
/// Todo AISLADO por tenant — el tenant siempre viene del JWT del operador (ICurrentUser.TenantId),
/// nunca del body, para que un admin no pueda leer/escribir datos de otra marca.
/// </summary>
public interface IAdminService
{
    /// <summary>KPIs resumidos del dashboard (miembros, cupones activos/canjeados, puntos emitidos/canjeados).</summary>
    Task<AdminDashboardDto> GetDashboardAsync(Guid tenantId, CancellationToken ct = default);

    /// <summary>Lista paginada de miembros del tenant (orden por fecha de alta desc).</summary>
    Task<(IReadOnlyList<AdminMemberDto> Items, int Total)> ListMembersAsync(Guid tenantId, int page, int pageSize, string? search, CancellationToken ct = default);

    /// <summary>Lista de cupones del tenant (todos los estados), orden por emisión desc.</summary>
    Task<(IReadOnlyList<AdminCouponDto> Items, int Total)> ListCouponsAsync(Guid tenantId, int page, int pageSize, CancellationToken ct = default);

    /// <summary>Configuración completa del programa + reglas + rewards del tenant.</summary>
    Task<AdminProgramDto> GetProgramAsync(Guid tenantId, CancellationToken ct = default);

    /// <summary>Actualiza la config de puntos/sellos del programa (invalida cache).</summary>
    Task UpdateProgramPointsAsync(Guid tenantId, UpdateProgramPointsRequest request, CancellationToken ct = default);

    /// <summary>Agrega una regla de sellos al programa (invalida cache).</summary>
    Task AddStampRuleAsync(Guid tenantId, AddStampRuleRequest request, CancellationToken ct = default);

    /// <summary>Agrega una recompensa canjeable al programa (invalida cache).</summary>
    Task AddRewardAsync(Guid tenantId, AddRewardRequest request, CancellationToken ct = default);

    /// <summary>Crea un cupón manual para un miembro del tenant (campaña/bonificación).</summary>
    Task<CreateCouponResultDto> CreateCouponAsync(Guid tenantId, CreateCouponRequest request, CancellationToken ct = default);

    /// <summary>Auditoría paginada de transacciones de fidelización del tenant.</summary>
    Task<(IReadOnlyList<AdminTransactionDto> Items, int Total)> ListTransactionsAsync(Guid tenantId, int page, int pageSize, CancellationToken ct = default);

    /// <summary>Reporte agregado del rango: métricas globales + por tienda + por día.</summary>
    Task<AdminReportDto> GetReportAsync(Guid tenantId, DateTimeOffset from, DateTimeOffset to, Guid? storeId, CancellationToken ct = default);

    /// <summary>Genera el CSV del reporte del rango (por tienda + totales) para descarga.</summary>
    Task<string> ExportReportCsvAsync(Guid tenantId, DateTimeOffset from, DateTimeOffset to, Guid? storeId, CancellationToken ct = default);
}