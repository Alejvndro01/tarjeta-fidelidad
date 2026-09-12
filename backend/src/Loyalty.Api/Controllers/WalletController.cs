using System.Security.Claims;
using Loyalty.Application.Wallets;
using Loyalty.Contracts.Wallets;
using Loyalty.Core.Domain.Members;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Loyalty.Api.Controllers;

/// <summary>
/// Pase de fidelidad para Apple/Google Wallet. Requiere token de miembro (actor=member).
/// POR QUÉ restaurar en el controlador y no en program: el miembro se identifica por el `sub`
/// del token (lo re-mapeado a NameIdentifier), no por un id explícito en la URL que permitiría
/// enumerar pases ajenos.
/// </summary>
[ApiController]
[Route("api/wallet")]
[Authorize]
public class WalletController : ControllerBase
{
    private readonly IWalletPassService _wallet;

    public WalletController(IWalletPassService wallet) => _wallet = wallet;

    /// <summary>Genera el pase .pkpass de Apple Wallet para descargar/instalar.</summary>
    [HttpGet("apple")]
    [ProducesResponseType(200)]
    [ProducesResponseType(401)]
    public async Task<IActionResult> Apple(CancellationToken ct)
    {
        if (!IsMemberToken()) return Unauthorized(new { error = "Se requiere token de miembro." });
        var memberId = GetMemberId();
        var result = await _wallet.GeneratePassAsync(memberId, WalletProviderNames.Apple, ct);
        return File(result.PkpassBytes!, result.MediaType!, result.FileName);
    }

    /// <summary>Genera el JWT de Google Wallet para "Añadir a la cartera".</summary>
    [HttpGet("google")]
    [ProducesResponseType(typeof(WalletPassResultDto), 200)]
    [ProducesResponseType(401)]
    public async Task<ActionResult<WalletPassResultDto>> Google(CancellationToken ct)
    {
        if (!IsMemberToken()) return Unauthorized(new { error = "Se requiere token de miembro." });
        var memberId = GetMemberId();
        var result = await _wallet.GeneratePassAsync(memberId, WalletProviderNames.Google, ct);
        return Ok(result);
    }

    private bool IsMemberToken() => User.FindFirst("actor")?.Value == "member";

    private Guid GetMemberId()
    {
        var sub = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? User.FindFirst("sub")?.Value;
        return Guid.Parse(sub ?? throw new DomainException("Token inválido sin identificador de miembro."));
    }
}