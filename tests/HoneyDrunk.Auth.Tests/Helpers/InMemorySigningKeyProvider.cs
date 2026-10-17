using HoneyDrunk.Auth.Secrets;
using Microsoft.IdentityModel.Tokens;

namespace HoneyDrunk.Auth.Tests.Helpers;

/// <summary>
/// In-memory implementation of <see cref="ISigningKeyProvider"/> for testing.
/// </summary>
public sealed class InMemorySigningKeyProvider : ISigningKeyProvider
{
    private readonly List<SecurityKey> _signingKeys = [];
    private string _issuer = "https://test.honeydrunk.io";
    private string _audience = "api://test";
    private TimeSpan _clockSkew = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Gets the list of signing keys for adding test keys.
    /// </summary>
    public IList<SecurityKey> SigningKeys => _signingKeys;

    /// <summary>
    /// Sets the issuer for token validation.
    /// </summary>
    /// <param name="issuer">The issuer.</param>
    /// <returns>This provider for chaining.</returns>
    public InMemorySigningKeyProvider WithIssuer(string issuer)
    {
        _issuer = issuer;
        return this;
    }

    /// <summary>
    /// Sets the audience for token validation.
    /// </summary>
    /// <param name="audience">The audience.</param>
    /// <returns>This provider for chaining.</returns>
    public InMemorySigningKeyProvider WithAudience(string audience)
    {
        _audience = audience;
        return this;
    }

    /// <summary>
    /// Sets the clock skew for token validation.
    /// </summary>
    /// <param name="clockSkew">The clock skew.</param>
    /// <returns>This provider for chaining.</returns>
    public InMemorySigningKeyProvider WithClockSkew(TimeSpan clockSkew)
    {
        _clockSkew = clockSkew;
        return this;
    }

    /// <summary>
    /// Adds a symmetric signing key.
    /// </summary>
    /// <param name="keyId">The key ID.</param>
    /// <param name="keyBytes">The key bytes.</param>
    /// <returns>This provider for chaining.</returns>
    public InMemorySigningKeyProvider AddKey(string keyId, byte[] keyBytes)
    {
        _signingKeys.Add(new SymmetricSecurityKey(keyBytes) { KeyId = keyId });
        return this;
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<SecurityKey>> GetSigningKeysAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyList<SecurityKey>>(_signingKeys.ToList().AsReadOnly());
    }

    /// <inheritdoc />
    public Task<string> GetIssuerAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_issuer);
    }

    /// <inheritdoc />
    public Task<string> GetAudienceAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_audience);
    }

    /// <inheritdoc />
    public Task<TimeSpan> GetClockSkewAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_clockSkew);
    }
}
