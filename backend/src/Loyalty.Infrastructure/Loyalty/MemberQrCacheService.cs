using System.Text.Json;
using Loyalty.Application.Loyalty;
using Loyalty.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;

namespace Loyalty.Infrastructure.Loyalty;

/// <summary>
/// Caché Redis para validación instantánea de QR en caja. POR QUÉ: en horas punta, el escáner
/// consulta el QR decenas de veces por segundo; ir a PostgreSQL por cada scan saturaría la BD.
/// Redis sirve la resolución (TenantId, MemberId) en ~<1ms con TTL corto. Un miss (expiración o
/// reinicio) cae a BD para re-sembrar sin bloquear el flujo.
/// </summary>
public sealed class MemberQrCacheService : IMemberQrCacheService
{
    private readonly IDistributedCache _cache;
    private readonly LoyaltyDbContext _db;

    // TTL corto: equilibra frescura del saldo con reducción de round-trips a BD.
    private static readonly TimeSpan QrTtl = TimeSpan.FromMinutes(10);

    public MemberQrCacheService(IDistributedCache cache, LoyaltyDbContext db)
    {
        _cache = cache;
        _db = db;
    }

    public async Task<QrCacheEntry?> TryResolveAsync(string qrHash, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(qrHash)) return null;

        var key = $"qr:{qrHash}";
        var cached = await _cache.GetStringAsync(key, ct);
        if (cached != null)
        {
            return JsonSerializer.Deserialize<QrCacheEntry>(cached);
        }

        // Miss → resolver desde BD y re-sembrar. Un miss no debe invalidar el flujo: si el miembro
        // no existe, retornamos null (el controlador traduce a 404/fila inválida).
        var member = await _db.Members
            .AsNoTracking()
            .Where(m => m.QrHash == qrHash)
            .Select(m => new { m.Id, m.TenantId })
            .FirstOrDefaultAsync(ct);

        if (member == null) return null;

        var entry = new QrCacheEntry(member.TenantId, member.Id);
        await _cache.SetStringAsync(key, JsonSerializer.Serialize(entry), new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = QrTtl
        }, ct);
        return entry;
    }

    public async Task SeedAsync(Guid tenantId, Guid memberId, string qrHash, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(qrHash)) return;
        var entry = new QrCacheEntry(tenantId, memberId);
        await _cache.SetStringAsync($"qr:{qrHash}", JsonSerializer.Serialize(entry), new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = QrTtl
        }, ct);
    }
}