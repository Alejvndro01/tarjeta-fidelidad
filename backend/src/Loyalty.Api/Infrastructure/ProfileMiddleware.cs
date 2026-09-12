using System.Diagnostics;
using Microsoft.AspNetCore.Http;

namespace Loyalty.Api.Infrastructure;

/// <summary>
/// Middleware de diagnóstico: mide la duración real de procesamiento de CADA request a nivel
/// servidor (sin el overhead del cliente HTTP). POR QUÉ: los benchmarks con curl miden la latencia
/// end-to-end del entorno local (round-trip + proceso curl) y sobreestiman el costo del hot path;
/// esto aísla el tiempo que la API realmente tarda en servir, que es el dato correcto del objetivo
/// &lt;200ms en caja. Solo Development (no debe pagarse en producción).
/// </summary>
public sealed class ProfileMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ProfileMiddleware> _logger;

    public ProfileMiddleware(RequestDelegate next, ILogger<ProfileMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var sw = Stopwatch.StartNew();
        await _next(context);
        sw.Stop();
        _logger.LogInformation("PROFILE route={Route} status={Status} ms={Ms}",
            context.Request.Path, context.Response.StatusCode, sw.ElapsedMilliseconds);
    }
}