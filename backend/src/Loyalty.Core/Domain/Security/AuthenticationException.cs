namespace Loyalty.Core.Domain.Security;

/// <summary>
/// Fallo de autenticación (credenciales inválidas, sesión inexistente/expirada, usuario inactivo).
/// El middleware la traduce a HTTP 401 (sin revelar si el email existe). Distinta de DomainException
/// (409) para que el cliente distinga "mal autenticado" de "violación de regla de negocio".
/// </summary>
public sealed class AuthenticationException : Exception
{
    public AuthenticationException(string message) : base(message) { }
}