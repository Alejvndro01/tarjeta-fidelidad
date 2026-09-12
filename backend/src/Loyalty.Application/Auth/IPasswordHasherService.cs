namespace Loyalty.Application.Auth;

/// <summary>
/// Sella las contraseñas del staff con PBKDF2 (Rfc2898 = .NET nativo, sin cola de Identity),
/// iteraciones altas configurables y salt aleatorio de 16 bytes. POR QUÉ PBKDF2 nativo y no
/// BCrypt/Argon2 externo: evita dependencias nativas (cola de native libs en Reale de la API,
/// incompatibles con el sandbox), coste de CPU controlable y un estándar aprobado por OWASP.
/// </summary>
public interface IPasswordHasherService
{
    /// <summary>Devuelve el MNF64 del clave "iterations.salt.hash" encriptada.</summary>
    string HashPassword(string password);

    /// <summary>Verifica la contraseña contra el hash almacenado. Devuelve false si el formato es inválido.</summary>
    bool VerifyPassword(string password, string passwordHash);
}