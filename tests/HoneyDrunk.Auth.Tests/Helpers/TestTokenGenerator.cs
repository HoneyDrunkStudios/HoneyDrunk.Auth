using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using System.Security.Claims;

namespace HoneyDrunk.Auth.Tests.Helpers;

/// <summary>
/// Utility for generating test JWT tokens.
/// </summary>
public static class TestTokenGenerator
{
    /// <summary>
    /// Generates a symmetric key for testing.
    /// </summary>
    /// <param name="keyId">The key identifier.</param>
    /// <returns>A symmetric security key.</returns>
    public static SymmetricSecurityKey GenerateKey(string keyId = "test-key-1")
    {
        var keyBytes = new byte[32];
        Random.Shared.NextBytes(keyBytes);
        return new SymmetricSecurityKey(keyBytes) { KeyId = keyId };
    }

    /// <summary>
    /// Generates a JWT token with the specified parameters.
    /// </summary>
    /// <param name="signingKey">The signing key.</param>
    /// <param name="issuer">The token issuer.</param>
    /// <param name="audience">The token audience.</param>
    /// <param name="subject">The subject identifier.</param>
    /// <param name="name">The display name.</param>
    /// <param name="claims">Additional claims.</param>
    /// <param name="expires">The expiration time.</param>
    /// <param name="notBefore">The not-before time.</param>
    /// <returns>The JWT token string.</returns>
    public static string GenerateToken(
        SecurityKey signingKey,
        string issuer = "https://test.honeydrunk.io",
        string audience = "api://test",
        string subject = "user-123",
        string? name = "Test User",
        IEnumerable<Claim>? claims = null,
        DateTime? expires = null,
        DateTime? notBefore = null)
    {
        var claimsList = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, subject),
        };

        if (!string.IsNullOrEmpty(name))
        {
            claimsList.Add(new Claim(JwtRegisteredClaimNames.Name, name));
        }

        if (claims != null)
        {
            claimsList.AddRange(claims);
        }

        var credentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Issuer = issuer,
            Audience = audience,
            Subject = new ClaimsIdentity(claimsList),
            NotBefore = notBefore ?? DateTime.UtcNow.AddMinutes(-1),
            Expires = expires ?? DateTime.UtcNow.AddHours(1),
            SigningCredentials = credentials,
        };

        return new JsonWebTokenHandler().CreateToken(tokenDescriptor);
    }

    /// <summary>
    /// Generates an expired token.
    /// </summary>
    /// <param name="signingKey">The signing key.</param>
    /// <param name="issuer">The token issuer.</param>
    /// <param name="audience">The token audience.</param>
    /// <returns>An expired JWT token string.</returns>
    public static string GenerateExpiredToken(
        SecurityKey signingKey,
        string issuer = "https://test.honeydrunk.io",
        string audience = "api://test")
    {
        return GenerateToken(
            signingKey,
            issuer,
            audience,
            expires: DateTime.UtcNow.AddHours(-1),
            notBefore: DateTime.UtcNow.AddHours(-2));
    }

    /// <summary>
    /// Generates a not-yet-valid token.
    /// </summary>
    /// <param name="signingKey">The signing key.</param>
    /// <param name="issuer">The token issuer.</param>
    /// <param name="audience">The token audience.</param>
    /// <returns>A not-yet-valid JWT token string.</returns>
    public static string GenerateNotYetValidToken(
        SecurityKey signingKey,
        string issuer = "https://test.honeydrunk.io",
        string audience = "api://test")
    {
        return GenerateToken(
            signingKey,
            issuer,
            audience,
            notBefore: DateTime.UtcNow.AddHours(1),
            expires: DateTime.UtcNow.AddHours(2));
    }
}
