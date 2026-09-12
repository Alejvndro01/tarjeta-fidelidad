using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Loyalty.Application.Auth;
using Loyalty.Application.Members;
using Loyalty.Contracts.Members;
using Loyalty.Core.Domain.Loyalty;
using Loyalty.Core.Domain.Members;
using Loyalty.Core.Domain.Security;
using Loyalty.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Loyalty.Infrastructure.Members;

/// <summary>
/// Autogestión del consumidor. POR QUÉ transactional en registro: crea el miembro y, si el tenant
/// tiene programa con sellos, conviene sembrar reglas de sellos relevadas en el perfil — pero las
/// reglas ya existen en LoyaltyProgram. Solo insertamos el Member; el QR hash/secret se generan aquí
/// con alta entropía y el token se emite por el TokenService (mismo issuer que staff, claim actor="member").
/// El filtro multi-tenant de EF se desactiva (tenant nulo) porque el registro es público por slug.
/// </summary>
public sealed class MemberService : IMemberService
{
    private readonly LoyaltyDbContext _db;
    private readonly ITokenService _tokens;

    public MemberService(LoyaltyDbContext db, ITokenService tokens)
    {
        _db = db;
        _tokens = tokens;
    }

    public async Task<RegisterMemberResponseDto> RegisterAsync(RegisterMemberRequestDto request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.PhoneNumber)) throw new DomainException("El número de celular es obligatorio.");
        if (string.IsNullOrWhiteSpace(request.FullName)) throw new DomainException("El nombre es obligatorio.");

        var tenant = await _db.Tenants.SingleOrDefaultAsync(t => t.Slug == request.TenantSlug, ct)
            ?? throw new DomainException($"El tenant '{request.TenantSlug}' no existe.");

        var program = await _db.LoyaltyPrograms.SingleOrDefaultAsync(p => p.TenantId == tenant.Id, ct)
            ?? throw new DomainException("El tenant no tiene programa de fidelización configurado.");

        // Idempotencia por phone: si ya existe en el tenant, re-emite token en vez de duplicar.
        var existing = await _db.Members.SingleOrDefaultAsync(
            m => m.TenantId == tenant.Id && m.PhoneNumber == request.PhoneNumber.Trim(), ct);

        Member member;
        if (existing != null)
        {
            member = existing;
        }
        else
        {
            var qrHash = GenerateQrHash();
            var qrSecret = GenerateQrSecret();
            member = Member.Create(tenant.Id, program.Id, request.PhoneNumber, request.FullName,
                request.Email, qrHash, qrSecret);
            _db.Members.Add(member);
            await _db.SaveChangesAsync(ct);
        }

        var token = _tokens.CreateToken(new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, member.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new Claim(ClaimNames.TenantId, tenant.Id.ToString()),
            new Claim(ClaimNames.Actor, "member"),
        });

        var profile = await BuildProfileAsync(member, program, tenant.Slug);
        return new RegisterMemberResponseDto
        {
            AccessToken = token,
            AccessTokenExpiresInSeconds = _tokens.AccessTokenExpiresInSeconds,
            Member = profile
        };
    }

    public async Task<MemberProfileDto> GetProfileAsync(Guid memberId, CancellationToken ct = default)
    {
        var member = await _db.Members.SingleOrDefaultAsync(m => m.Id == memberId, ct)
            ?? throw new DomainException("Miembro no encontrado.");

        var program = await _db.LoyaltyPrograms.AsNoTracking()
            .Include(p => p.StampRules)
            .SingleAsync(p => p.Id == member.LoyaltyProgramId, ct);
        var tenant = await _db.Tenants.AsNoTracking().SingleAsync(t => t.Id == member.TenantId, ct);

        return await BuildProfileAsync(member, program, tenant.Slug);
    }

    private async Task<MemberProfileDto> BuildProfileAsync(Member member, LoyaltyProgram program, string tenantSlug)
    {
        var stamps = new List<MemberStampDto>();
        var storedStamps = await _db.MemberStamps.AsNoTracking()
            .Where(s => s.MemberId == member.Id)
            .ToListAsync();

        foreach (var rule in program.StampRules)
        {
            var count = storedStamps.FirstOrDefault(s => s.ProductSku == rule.ProductSku)?.Count ?? 0;
            stamps.Add(new MemberStampDto
            {
                ProductSku = rule.ProductSku,
                ProductName = rule.ProductName,
                Count = count,
                StampsRequired = rule.StampsRequired,
                Completed = count >= rule.StampsRequired
            });
        }

        return new MemberProfileDto
        {
            MemberId = member.Id,
            PhoneNumber = member.PhoneNumber,
            FullName = member.FullName,
            Email = member.Email,
            PointsBalance = member.PointsBalance,
            QrHash = member.QrHash,
            TenantSlug = tenantSlug,
            Stamps = stamps
        };
    }

    private static string GenerateQrHash()
    {
        // Hash corto, único por tenant (índice (tenant_id, qr_hash)). Sufijo aleatorio alto entropía.
        var bytes = RandomNumberGenerator.GetBytes(8);
        return "M" + Convert.ToHexString(bytes)[..6].ToLowerInvariant();
    }

    private static string GenerateQrSecret()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(bytes);
    }
}