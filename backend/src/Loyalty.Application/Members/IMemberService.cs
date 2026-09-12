using Loyalty.Contracts.Members;

namespace Loyalty.Application.Members;

/// <summary>
/// Servicio de autogestión del consumidor: registro sin fricción (celular + nombre + correo opcional)
/// y consulta de su perfil (saldo, sellos, QR). POR QUÉ el miembro no usa el flujo de staff: es un
/// actor distinto; su identidad es el phone y su acceso es un token propio de miembro (claim actor).
/// </summary>
public interface IMemberService
{
    Task<RegisterMemberResponseDto> RegisterAsync(RegisterMemberRequestDto request, CancellationToken ct = default);
    Task<MemberProfileDto> GetProfileAsync(Guid memberId, CancellationToken ct = default);
}