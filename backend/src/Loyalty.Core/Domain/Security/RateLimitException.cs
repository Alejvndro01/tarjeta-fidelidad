namespace Loyalty.Core.Domain.Security;

/// <summary>
/// Lanzada por throttling anti-fuerza-bruta. El middleware la traduce a HTTP 429 (Too Many Requests)
/// con cabecera Retry-After. Separada de DomainException para no mezclar reglas de negocio con
/// límites de seguridad de la infraestructura.
/// </summary>
public sealed class RateLimitException : Exception
{
    /// <summary>Segundos que el cliente debe esperar antes de reintentar (cabecera Retry-After).</summary>
    public int RetryAfterSeconds { get; }

    public RateLimitException(int retryAfterSeconds, string? message = null)
        : base(message ?? $"Demasiados intentos. Reintente en {retryAfterSeconds}s.")
    {
        RetryAfterSeconds = retryAfterSeconds;
    }
}