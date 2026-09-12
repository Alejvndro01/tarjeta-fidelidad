using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Loyalty.Core.Domain.Identity;
using Loyalty.Core.Domain.Security;
using Loyalty.Infrastructure.Auth;
using Microsoft.Extensions.Options;
using Xunit;

namespace Loyalty.UnitTests.Auth;

/// <summary>Tests unitarios de hashing de contraseñas y firma/validación de tokens (sin infra).</summary>
public class PasswordAndTokenTests
{
    private readonly PasswordHasherService _hasher = new();
    private readonly TokenService _tokens;

    public PasswordAndTokenTests()
    {
        _tokens = new TokenService(Options.Create(new JwtOptions
        {
            Issuer = "test", Audience = "pwa", SecretKey = new string('x', 40), AccessTokenMinutes = 30
        }));
    }

    [Fact]
    public void HashPassword_is_salted_and_verifiable()
    {
        var h1 = _hasher.HashPassword("MiPassword123");
        var h2 = _hasher.HashPassword("MiPassword123");

        // Salt aleatorio → hashes distintos aunque la contraseña sea igual.
        Assert.NotEqual(h1, h2);
        Assert.True(_hasher.VerifyPassword("MiPassword123", h1));
        Assert.False(_hasher.VerifyPassword("otra", h1));

        // Formato versionable "Pbkdf2$iter$salt$hash".
        var parts = h1.Split('$');
        Assert.Equal(4, parts.Length);
        Assert.Equal("Pbkdf2", parts[0]);
    }

    [Fact]
    public void TokenService_issues_and_validates_round_trip()
    {
        var user = AppUser.Create(
            Guid.NewGuid(), Guid.NewGuid(), "cajero@test.dev", "hash", "salt", UserRole.Cajero);

        var token = _tokens.CreateAccessToken(user);
        Assert.False(string.IsNullOrWhiteSpace(token));

        var principal = _tokens.ValidateAccessToken(token);
        Assert.NotNull(principal);
        Assert.Equal("Cajero", principal!.FindFirst(ClaimTypes.Role)?.Value);
        Assert.Equal(user.Id.ToString(), principal.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
    ?? principal.FindFirst(ClaimTypes.NameIdentifier)?.Value);
    }

    [Fact]
    public void ValidateAccessToken_rejects_tampered_token()
    {
        var user = AppUser.Create(null, null, "admin@test.dev", "hash", "salt", UserRole.SuperAdmin);
        var token = _tokens.CreateAccessToken(user);
        var tampered = token[..^2] + "ab"; // corrompe la firma

        Assert.Null(_tokens.ValidateAccessToken(tampered));
    }

    [Fact]
    public void HashRefreshToken_is_deterministic_sha256()
    {
        var a = _tokens.HashRefreshToken("abc");
        var b = _tokens.HashRefreshToken("abc");
        Assert.Equal(a, b);
        Assert.NotEqual(a, _tokens.HashRefreshToken("abd"));
        Assert.Matches("^[A-Za-z0-9+/]+=*$", a); // Base64
    }
}