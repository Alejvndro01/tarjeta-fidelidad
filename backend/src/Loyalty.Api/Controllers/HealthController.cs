using Loyalty.Core.Domain.Tenants;
using Loyalty.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;

namespace Loyalty.Api.Controllers;

/// <summary>
/// Health-check operativo de cimientos: verifica que el DbContext (PostgreSQL) y Redis
/// estén vivos en run-time. No expone datos de negocio.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class HealthController : ControllerBase
{
    private readonly LoyaltyDbContext _db;
    private readonly IDistributedCache _cache;

    public HealthController(LoyaltyDbContext db, IDistributedCache cache)
    {
        _db = db;
        _cache = cache;
    }

    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        // Candado de conexión mínima: SELECT 1 forzado por EF sin mapear tablas.
        var pgOk = await _db.Database.CanConnectAsync(ct);

        // Redis: escribir+leer un ping con TTL efímero para validar el canal completo.
        var cacheKey = "health:ping";
        await _cache.SetStringAsync(cacheKey, "pong", new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(30)
        }, ct);
        var redisOk = (await _cache.GetStringAsync(cacheKey, ct)) == "pong";

        var status = pgOk && redisOk ? "ok" : "degraded";
        return StatusCode(pgOk && redisOk ? 200 : 503, new
        {
            status,
            postgres = pgOk,
            redis = redisOk,
            timestampUtc = DateTimeOffset.UtcNow
        });
    }
}