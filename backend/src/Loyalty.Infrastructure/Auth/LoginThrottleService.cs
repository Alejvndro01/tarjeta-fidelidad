using Loyalty.Application.Auth;
using StackExchange.Redis;

namespace Loyalty.Infrastructure.Auth;

/// <summary>
/// Anti-fuerza-bruta por email + IP usando INCR / EXPIRE atómicos de Redis.
/// POR QUÉ INCR/EXPIRE y NO Get/Set: dos logins simultáneos sobre Get→+1→Set pueden leer el
/// mismo contador y eludir el bloqueo; INCR+EXPIRE hace el conteo y el periodo atómicos y
/// distribuidos (todos los nodos del POS comparten el lock, coherentes en picos).
/// Umbral: 5 fallos en 5 min → bloqueo 15 min (email+IP). Quien rota IP no elude el límite
/// por email, ni quien cambia email elude el límite por IP.
/// </summary>
public sealed class LoginThrottleService : ILoginThrottleService
{
    private readonly IConnectionMultiplexer _redis;
    private const string Prefix = "loyalty:throttle:login:";
    private static readonly TimeSpan _failWindow = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan _blockDuration = TimeSpan.FromMinutes(15);
    private const int MaxFailures = 5;

    public LoginThrottleService(IConnectionMultiplexer redis) => _redis = redis;

    public bool RecordFailedAttempt(string email, string ipAddress, out int retryAfterSeconds, out int failedCount)
    {
        var db = _redis.GetDatabase();
        var counterKey = KeyFor(email, ipAddress);
        var blockedKey = BlockedKeyFor(email, ipAddress);

        // INCR devuelve el nuevo valor; EXPIRE renueva la ventana en cada fallo (lazy sliding).
        failedCount = (int)db.StringIncrement(counterKey, 1);
        db.KeyExpire(counterKey, _failWindow);

        if (failedCount >= MaxFailures)
        {
            // SET blocked con TTL de bloqueo. Idempotente: si ya está bloqueado, renueva.
            db.StringSet(blockedKey, "1", _blockDuration);
            retryAfterSeconds = (int)_blockDuration.TotalSeconds;
        }
        else
        {
            retryAfterSeconds = 0;
        }

        return !IsBlocked(email, ipAddress, out _);
    }

    public void Reset(string email, string ipAddress)
    {
        var db = _redis.GetDatabase();
        db.KeyDelete(new[] { KeyFor(email, ipAddress), BlockedKeyFor(email, ipAddress) });
    }

    public bool IsBlocked(string email, string ipAddress, out int retryAfterSeconds)
    {
        retryAfterSeconds = 0;
        var db = _redis.GetDatabase();
        var blockedKey = BlockedKeyFor(email, ipAddress);
        if (!db.KeyExists(blockedKey)) return false;

        // TTL restante real del bloqueo (segundos) para la cabecera Retry-After.
        retryAfterSeconds = (int)Math.Max(1, db.KeyTimeToLive(blockedKey)?.TotalSeconds ?? _blockDuration.TotalSeconds);
        return true;
    }

    private static RedisKey KeyFor(string email, string ip) => new($"{Prefix}{email.Trim().ToLowerInvariant()}|{ip}");
    private static RedisKey BlockedKeyFor(string email, string ip) => new($"{Prefix}blocked:{email.Trim().ToLowerInvariant()}|{ip}");
}