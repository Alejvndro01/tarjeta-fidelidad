using System.Security.Claims;
using Loyalty.Application.Auth;
using Loyalty.Infrastructure.Persistence;

namespace Loyalty.Api.Infrastructure;

/// <summary>
/// Middleware que fija el tenant del request en el AsyncLocal de persistencia LEYÉNDOLO del JWT
/// autenticado (claim "tenant_id"), no del body. POR QUÉ: el aislamiento multi-tenant de EF se
/// aplica por request con AmbientTenantAccessor; si un endpoint de POS tomara tenant del body,
/// un cajero podría cruzar al tenant de otra marca. Al leerlo del token validado, el alcance es
/// el del usuario autenticado y los queries se filtran a su tenant real.
/// Requiere correr DESPUÉS de UseAuthentication (principal poblado) y ANTES de los controllers.
/// Requests anónimos (login/health) dejan el tenant nulo → sin aislamiento (correcto).
/// </summary>
public sealed class TenantContextMiddleware
{
    private readonly RequestDelegate _next;

    public TenantContextMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context)
    {
        var principal = context.User;
        if (principal.Identity?.IsAuthenticated == true)
        {
            // El tenant NO viene del body sino del token. Nullable: SuperAdmin no lleva tenant.
            if (Guid.TryParse(principal.FindFirst("tenant_id")?.Value, out var tenantId))
                AmbientTenantAccessor.TenantId = tenantId;
        }

        try
        {
            await _next(context);
        }
        finally
        {
            // Limpieza obligatoria: el AsyncLocal es estático y sobrevive al request; sin esto
            // el siguiente request reutilizaría el mismo thread con un tenant residual (fuga).
            AmbientTenantAccessor.Clear();
        }
    }
}