namespace Loyalty.Application.Auth;

/// <summary>
/// Throttling anti-fuerza-bruta por email + IP usando operaciones atómicas INCR/EXPIRE de Redis.
/// POR QUÉ Redis y no en-memoria: funciona en varios nodos/locales, es atómico (INCR/TimeToLive)
/// y alinea con el cache distribuido ya usado para QR. Un atacante que rote IP no escapa del
/// límite por email, y viceversa.
/// </summary>
public interface ILoginThrottleService
{
    /// <summary>Registra un intento fallido. Devuelve true si el intento corriente debe permitirse (no bloqueado).</summary>
    bool RecordFailedAttempt(string email, string ipAddress, out int remainRetryAfterSeconds, out int failedCount);

    /// <summary>Limpia el contador de intentos tras un login exitoso.</summary>
    void Reset(string email, string ipAddress);

    /// <summary>Devuelve true si la combinación email+IP está actualmente bloqueada.</summary>
    bool IsBlocked(string email, string ipAddress, out int retryAfterSeconds);
}