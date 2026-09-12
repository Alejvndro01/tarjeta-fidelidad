using System.Security.Cryptography;
using Loyalty.Application.Auth;

namespace Loyalty.Infrastructure.Auth;

/// <summary>
/// Hashing PBKDF2 (Rfc2898DeriveBytes) con salt de 16 bytes, 100k iteraciones (OWASP) y
/// formato versionable "Pbkdf2$iterations$salt$hash" en Base64. POR QUÉ formato propio y no
/// el MNF64 de PNORD Identity: el almacén se integra con EF Core sin el paquete ASP.NET
/// Identity completo (que arrastra cookie/claim schemas no usados), y las iteraciones altas
/// hacen inviable la fuerza bruta offline aun si la BD se filtra.
/// </summary>
public sealed class PasswordHasherService : IPasswordHasherService
{
    private const int SaltSize = 16;         // 128 bits
    private const int HashSize = 32;         // 256 bits
    private const int Iterations = 100_000;  // OWASP mínimo recomendado para PBKDF2
    private static readonly HashAlgorithmName _algo = HashAlgorithmName.SHA256;

    public string HashPassword(string password)
    {
        ArgumentNullException.ThrowIfNull(password);
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, _algo, HashSize);
        return $"Pbkdf2${Iterations}${Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}";
    }

    public bool VerifyPassword(string password, string passwordHash)
    {
        ArgumentNullException.ThrowIfNull(password);
        if (string.IsNullOrWhiteSpace(passwordHash)) return false;

        var parts = passwordHash.Split('$');
        if (parts.Length != 4 || parts[0] != "Pbkdf2") return false;
        if (!int.TryParse(parts[1], out var iterations) || iterations < 1) return false;

        byte[] salt, expectedHash;
        try
        {
            salt = Convert.FromBase64String(parts[2]);
            expectedHash = Convert.FromBase64String(parts[3]);
        }
        catch (FormatException) { return false; }

        var actual = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, _algo, expectedHash.Length);
        return CryptographicOperations.FixedTimeEquals(actual, expectedHash); // timing-safe
    }
}