using System.Security.Claims;
using Loyalty.Application.Admin;
using Loyalty.Application.Auth;
using Loyalty.Contracts.Admin;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace Loyalty.Api.Controllers;

/// <summary>
/// Panel de administración multi-tenant. Rol: TenantAdmin o SuperAdmin.
/// El tenant NUNCA viene del body — se lee del JWT (ICurrentUser.TenantId), de modo que
/// un admin no pueda apuntar queries a otra marca. Un usuario sin tenant asignado (SuperAdmin
/// global sin tenant) NO puede usar este panel hasta elegir un tenant; la seguridad exige
/// el tenant del token, no un parámetro de consulta.
/// </summary>
[ApiController]
[Route("api/admin")]
[Authorize(Roles = "TenantAdmin,SuperAdmin")]
public class AdminController : ControllerBase
{
    private readonly IAdminService _admin;
    private readonly ICurrentUser _currentUser;

    public AdminController(IAdminService admin, ICurrentUser currentUser)
    {
        _admin = admin;
        _currentUser = currentUser;
    }

    private Guid RequireTenant()
    {
        return _currentUser.TenantId
            ?? throw new UnauthorizedAccessException("Tu usuario no tiene un tenant asignado. Asócialo a una marca para usar el panel de administración.");
    }

    [HttpGet("dashboard")]
    [ProducesResponseType(typeof(AdminDashboardDto), 200)]
    public async Task<ActionResult<AdminDashboardDto>> Dashboard(CancellationToken ct)
        => Ok(await _admin.GetDashboardAsync(RequireTenant(), ct));

    [HttpGet("members")]
    [ProducesResponseType(typeof(IReadOnlyList<AdminMemberDto>), 200)]
    public async Task<ActionResult<IReadOnlyList<AdminMemberDto>>> Members(int page = 1, int pageSize = 20, string? search = null, CancellationToken ct = default)
    {
        var (items, total) = await _admin.ListMembersAsync(RequireTenant(), page, pageSize, search, ct);
        Response.Headers["X-Total-Count"] = total.ToString();
        return Ok(items);
    }

    [HttpGet("coupons")]
    [ProducesResponseType(typeof(IReadOnlyList<AdminCouponDto>), 200)]
    public async Task<ActionResult<IReadOnlyList<AdminCouponDto>>> Coupons(int page = 1, int pageSize = 20, CancellationToken ct = default)
    {
        var (items, total) = await _admin.ListCouponsAsync(RequireTenant(), page, pageSize, ct);
        Response.Headers["X-Total-Count"] = total.ToString();
        return Ok(items);
    }

    [HttpGet("program")]
    [ProducesResponseType(typeof(AdminProgramDto), 200)]
    public async Task<ActionResult<AdminProgramDto>> Program(CancellationToken ct)
        => Ok(await _admin.GetProgramAsync(RequireTenant(), ct));

    [HttpPut("program/points")]
    public async Task<IActionResult> UpdatePoints([FromBody] UpdateProgramPointsRequest request, CancellationToken ct)
    {
        await _admin.UpdateProgramPointsAsync(RequireTenant(), request, ct);
        return NoContent();
    }

    [HttpPost("program/stamp-rules")]
    public async Task<IActionResult> AddStampRule([FromBody] AddStampRuleRequest request, CancellationToken ct)
    {
        await _admin.AddStampRuleAsync(RequireTenant(), request, ct);
        return NoContent();
    }

    [HttpPost("program/rewards")]
    public async Task<IActionResult> AddReward([FromBody] AddRewardRequest request, CancellationToken ct)
    {
        await _admin.AddRewardAsync(RequireTenant(), request, ct);
        return NoContent();
    }

    [HttpPost("coupons")]
    [ProducesResponseType(typeof(CreateCouponResultDto), 200)]
    public async Task<ActionResult<CreateCouponResultDto>> CreateCoupon([FromBody] CreateCouponRequest request, CancellationToken ct)
        => Ok(await _admin.CreateCouponAsync(RequireTenant(), request, ct));

    [HttpGet("transactions")]
    [ProducesResponseType(typeof(IReadOnlyList<AdminTransactionDto>), 200)]
    public async Task<ActionResult<IReadOnlyList<AdminTransactionDto>>> Transactions(int page = 1, int pageSize = 20, CancellationToken ct = default)
    {
        var (items, total) = await _admin.ListTransactionsAsync(RequireTenant(), page, pageSize, ct);
        Response.Headers["X-Total-Count"] = total.ToString();
        return Ok(items);
    }

    [HttpGet("report")]
    [ProducesResponseType(typeof(AdminReportDto), 200)]
    public async Task<ActionResult<AdminReportDto>> Report(DateTimeOffset? from, DateTimeOffset? to, Guid? storeId, CancellationToken ct)
    {
        var range = ResolveRange(from, to);
        return Ok(await _admin.GetReportAsync(RequireTenant(), range.from, range.to, storeId, ct));
    }

    [HttpGet("report/export")]
    public async Task<IActionResult> ReportExport(DateTimeOffset? from, DateTimeOffset? to, Guid? storeId, CancellationToken ct)
    {
        var range = ResolveRange(from, to);
        var csv = await _admin.ExportReportCsvAsync(RequireTenant(), range.from, range.to, storeId, ct);
        return Content(csv, "text/csv; charset=utf-8");
    }

    private static (DateTimeOffset from, DateTimeOffset to) ResolveRange(DateTimeOffset? from, DateTimeOffset? to)
    {
        var end = to?.UtcDateTime ?? DateTime.UtcNow;
        var start = from?.UtcDateTime ?? end.AddDays(-30);
        return (start, end);
    }
}