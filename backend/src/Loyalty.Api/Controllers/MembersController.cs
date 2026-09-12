using System.Security.Claims;
using Loyalty.Application.Coupons;
using Loyalty.Application.Members;
using Loyalty.Contracts.Coupons;
using Loyalty.Contracts.Members;
using Loyalty.Core.Domain.Members;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Loyalty.Api.Controllers;

/// <summary>
/// Autogestión del consumidor (PWA). El registro es público por slug de tenant; el perfil
/// requiere token de miembro (claim actor="member"), que el TenantContextMiddleware interpreta
/// para fijar el tenant del miembro en el AsyncLocal (aislamiento por marca).
/// POR QUÉ no usar el rol staff aquí: los consumidores no son staff ni llevan RBAC; su token
/// tiene actor="member". Validamos ese claim explícitamente.
/// </summary>
[ApiController]
[Route("api/members")]
public class MembersController : ControllerBase
{
    private readonly IMemberService _members;
    private readonly ICouponService _coupons;

    public MembersController(IMemberService members, ICouponService coupons)
    {
        _members = members;
        _coupons = coupons;
    }

    /// <summary>Registro del consumidor: devuelve token de miembro + perfil (público).</summary>
    [HttpPost("register")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(RegisterMemberResponseDto), 200)]
    [ProducesResponseType(409)]
    public async Task<ActionResult<RegisterMemberResponseDto>> Register([FromBody] RegisterMemberRequestDto request, CancellationToken ct)
        => Ok(await _members.RegisterAsync(request, ct));

    /// <summary>Perfil del consumidor autenticado (saldo, sellos, QR). Requiere token de miembro.</summary>
    [HttpGet("me")]
    [Authorize]
    [ProducesResponseType(typeof(MemberProfileDto), 200)]
    [ProducesResponseType(401)]
    public async Task<ActionResult<MemberProfileDto>> Me(CancellationToken ct)
    {
        if (!IsMemberToken()) return Unauthorized(new { error = "Se requiere token de miembro." });
        var memberId = GetMemberId();
        return Ok(await _members.GetProfileAsync(memberId, ct));
    }

    /// <summary>Cupones activos del consumidor (para la app).</summary>
    [HttpGet("coupons")]
    [Authorize]
    [ProducesResponseType(typeof(IReadOnlyList<MemberCouponDto>), 200)]
    [ProducesResponseType(401)]
    public async Task<ActionResult<IReadOnlyList<MemberCouponDto>>> MyCoupons(CancellationToken ct)
    {
        if (!IsMemberToken()) return Unauthorized(new { error = "Se requiere token de miembro." });
        var memberId = GetMemberId();
        return Ok(await _coupons.ListMemberActiveCouponsAsync(memberId, ct));
    }

    private bool IsMemberToken() => User.FindFirst("actor")?.Value == "member";

    private Guid GetMemberId()
    {
        var sub = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? User.FindFirst("sub")?.Value;
        return Guid.Parse(sub ?? throw new DomainException("Token inválido sin identificador de miembro."));
    }
}