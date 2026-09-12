using System.Net;
using Loyalty.Core.Domain.Members;
using Loyalty.Core.Domain.Security;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Loyalty.Api.Infrastructure;

/// <summary>
/// Middleware de manejo centralizado de excepciones. POR QUÉ: traduce excepciones de dominio y
/// técnicas a respuestas HTTP consistentes sin exponer detalles internos (saudable en multi-tenant).
/// Las DomainException (reglas de negocio) → 4xx; lo inesperado → 500 genérico logged.
/// </summary>
public sealed class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (DomainException ex)
        {
            // Violación de regla de negocio: el mensaje es seguro de mostrar.
            await WriteErrorAsync(context, HttpStatusCode.Conflict, "business_error", ex.Message);
        }
        catch (AuthenticationException ex)
        {
            // Credenciales/sesión inválidas → 401 (no revelar causa interna).
            await WriteErrorAsync(context, HttpStatusCode.Unauthorized, "unauthorized", ex.Message);
        }
        catch (RateLimitException ex)
        {
            // Anti-fuerza-bruta → 429 con cabecera Retry-After para el cliente y proxies.
            context.Response.Headers["Retry-After"] = ex.RetryAfterSeconds.ToString();
            await WriteErrorAsync(context, HttpStatusCode.TooManyRequests, "rate_limited",
                $"Demasiados intentos. Reintente en {ex.RetryAfterSeconds}s.");
        }
        catch (UnauthorizedAccessException ex)
        {
            // Operación no permitida para el usuario actual (ej: panel admin sin tenant asignado).
            await WriteErrorAsync(context, HttpStatusCode.Unauthorized, "unauthorized", ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            // Conflictos de concurrencia/estado que no son de negocio pero son 4xx seguros.
            await WriteErrorAsync(context, HttpStatusCode.Conflict, "conflict", "Conflict state.");
            _logger.LogWarning(ex, "InvalidOperation during request");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception during request {Path}", context.Request.Path);
            await WriteErrorAsync(context, HttpStatusCode.InternalServerError, "internal_error",
                "Ocurrió un error inesperado.");
        }
    }

    private static async Task WriteErrorAsync(HttpContext context, HttpStatusCode code, string codeName, string message)
    {
        context.Response.StatusCode = (int)code;
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsJsonAsync(new { error = codeName, message });
    }
}