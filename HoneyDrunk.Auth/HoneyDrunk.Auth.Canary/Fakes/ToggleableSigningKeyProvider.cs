using HoneyDrunk.Auth.Secrets;
using Microsoft.IdentityModel.Tokens;

namespace HoneyDrunk.Auth.Canary.Fakes;

/// <summary>
/// Toggleable signing key provider for canary testing.
/// Sits underneath CachingSigningKeyProvider to test cache semantics.
/// </summary>
internal sealed class ToggleableSigningKeyProvider : ISigningKeyProvider
{
    private readonly List<SecurityKey> _keys = [];
    private readonly string _issuer;
    private readonly string _audience;
    private readonly TimeSpan _clockSkew;

    private bool _isAvailable = true;
    private int _getKeysCallCount;

    public ToggleableSigningKeyProvider(
        string issuer = "https://canary.honeydrunk.io",
        string audience = "api://canary",
        TimeSpan? clockSkew = null)
    {
        _issuer = issuer;
        _audience = audience;
        _clockSkew = clockSkew ?? TimeSpan.FromMinutes(5);
    }

    /// <summary>
    /// Gets or sets a value indicating whether the provider is available.
    /// When false, all operations throw to simulate Vault unavailability.
    /// </summary>
    public bool IsAvailable
    {
        get => _isAvailable;
        set => _isAvailable = value;
    }

    /// <summary>
    /// Gets the number of times GetSigningKeysAsync was called.
    /// Useful for verifying cache refresh behavior.
    /// </summary>
    public int GetKeysCallCount => _getKeysCallCount;

    /// <summary>
    /// Resets the call counter.
    /// </summary>
    public void ResetCallCount() => _getKeysCallCount = 0;

    /// <summary>
    /// Adds a signing key to the provider.
    /// </summary>
    public ToggleableSigningKeyProvider AddKey(SecurityKey key)
    {
        _keys.Add(key);
        return this;
    }

    /// <summary>
    /// Removes all keys and adds the specified keys.
    /// </summary>
    public ToggleableSigningKeyProvider SetKeys(params SecurityKey[] keys)
    {
        _keys.Clear();
        _keys.AddRange(keys);
        return this;
    }

    /// <summary>
    /// Clears all keys.
    /// </summary>
    public void ClearKeys() => _keys.Clear();

    /// <inheritdoc />
    public Task<IReadOnlyList<SecurityKey>> GetSigningKeysAsync(CancellationToken cancellationToken = default)
    {
        Interlocked.Increment(ref _getKeysCallCount);

        if (!_isAvailable)
        {
            throw new InvalidOperationException("Vault unavailable (simulated)");
        }

        return Task.FromResult<IReadOnlyList<SecurityKey>>(_keys.ToList());
    }

    /// <inheritdoc />
    public Task<string> GetIssuerAsync(CancellationToken cancellationToken = default)
    {
        if (!_isAvailable)
        {
            throw new InvalidOperationException("Vault unavailable (simulated)");
        }

        return Task.FromResult(_issuer);
    }

    /// <inheritdoc />
    public Task<string> GetAudienceAsync(CancellationToken cancellationToken = default)
    {
        if (!_isAvailable)
        {
            throw new InvalidOperationException("Vault unavailable (simulated)");
        }

        return Task.FromResult(_audience);
    }

    /// <inheritdoc />
    public Task<TimeSpan> GetClockSkewAsync(CancellationToken cancellationToken = default)
    {
        if (!_isAvailable)
        {
            throw new InvalidOperationException("Vault unavailable (simulated)");
        }

        return Task.FromResult(_clockSkew);
    }
}
