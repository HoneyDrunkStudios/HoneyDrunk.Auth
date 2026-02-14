using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using System.Security.Claims;

namespace HoneyDrunk.Auth.Canary.Helpers;

/// <summary>
/// Helper for minting JWT tokens in canary scenarios.
/// </summary>
internal static class TokenMinter
{
    public const string DefaultIssuer = "https://canary.honeydrunk.io";
    public const string DefaultAudience = "api://canary";

    /// <summary>
    /// Generates a new symmetric signing key.
    /// </summary>
    public static SymmetricSecurityKey GenerateKey(string keyId = "canary-key-1")
    {
        var keyBytes = new byte[32];
        Random.Shared.NextBytes(keyBytes);
        return new SymmetricSecurityKey(keyBytes) { KeyId = keyId };
    }

    /// <summary>
    /// Mints a valid token with standard required claims.
    /// </summary>
    public static string MintValid(
        SymmetricSecurityKey key,
        string subject = "user-123",
        string? name = "Canary User",
        IEnumerable<Claim>? additionalClaims = null,
        string issuer = DefaultIssuer,
        string audience = DefaultAudience)
    {
        var claims = new List<Claim> { new(JwtRegisteredClaimNames.Sub, subject) };

        if (!string.IsNullOrEmpty(name))
        {
            claims.Add(new Claim(JwtRegisteredClaimNames.Name, name));
        }

        if (additionalClaims is not null)
        {
            claims.AddRange(additionalClaims);
        }

        return CreateToken(key, claims, issuer, audience, DateTime.UtcNow.AddHours(1));
    }

    /// <summary>
    /// Mints a token missing the 'sub' claim.
    /// </summary>
    public static string MintWithoutSub(
        SymmetricSecurityKey key,
        string? name = "No Sub User",
        string issuer = DefaultIssuer,
        string audience = DefaultAudience)
    {
        var claims = new List<Claim>();
        if (!string.IsNullOrEmpty(name))
        {
            claims.Add(new Claim(JwtRegisteredClaimNames.Name, name));
        }

        return CreateToken(key, claims, issuer, audience, DateTime.UtcNow.AddHours(1));
    }

    /// <summary>
    /// Mints a token with 'sub' but missing a specified custom required claim.
    /// </summary>
    public static string MintMissingClaim(
        SymmetricSecurityKey key,
        string missingClaimName,
        string subject = "user-123",
        string issuer = DefaultIssuer,
        string audience = DefaultAudience)
    {
        // Include sub but deliberately omit the specified claim
        var claims = new List<Claim> { new(JwtRegisteredClaimNames.Sub, subject) };
        return CreateToken(key, claims, issuer, audience, DateTime.UtcNow.AddHours(1));
    }

    /// <summary>
    /// Mints an expired token.
    /// </summary>
    public static string MintExpired(
        SymmetricSecurityKey key,
        string subject = "user-123",
        string issuer = DefaultIssuer,
        string audience = DefaultAudience)
    {
        var claims = new List<Claim> { new(JwtRegisteredClaimNames.Sub, subject) };
        return CreateToken(
            key,
            claims,
            issuer,
            audience,
            DateTime.UtcNow.AddHours(-1),
            DateTime.UtcNow.AddHours(-2));
    }

    /// <summary>
    /// Mints a token with an unknown key ID (not in the trusted key set).
    /// </summary>
    public static (string token, SymmetricSecurityKey key) MintWithUnknownKid(
        string unknownKid,
        string subject = "user-123",
        string issuer = DefaultIssuer,
        string audience = DefaultAudience)
    {
        var unknownKey = GenerateKey(unknownKid);
        var claims = new List<Claim> { new(JwtRegisteredClaimNames.Sub, subject) };
        var token = CreateToken(unknownKey, claims, issuer, audience, DateTime.UtcNow.AddHours(1));
        return (token, unknownKey);
    }

    /// <summary>
    /// Mints a token with an invalid signature (signed by a different key than provided).
    /// </summary>
    public static string MintInvalidSignature(
        string subject = "user-123",
        string issuer = DefaultIssuer,
        string audience = DefaultAudience)
    {
        // Sign with a random key that will never be in the trusted set
        var fakeKey = GenerateKey("fake-key-never-trusted");
        var claims = new List<Claim> { new(JwtRegisteredClaimNames.Sub, subject) };
        return CreateToken(fakeKey, claims, issuer, audience, DateTime.UtcNow.AddHours(1));
    }

    private static string CreateToken(
        SymmetricSecurityKey key,
        List<Claim> claims,
        string issuer,
        string audience,
        DateTime expires,
        DateTime? notBefore = null)
    {
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = issuer,
            Audience = audience,
            Subject = new ClaimsIdentity(claims),
            NotBefore = notBefore ?? DateTime.UtcNow.AddMinutes(-1),
            Expires = expires,
            SigningCredentials = credentials,
        };

        return new JsonWebTokenHandler().CreateToken(descriptor);
    }
}
