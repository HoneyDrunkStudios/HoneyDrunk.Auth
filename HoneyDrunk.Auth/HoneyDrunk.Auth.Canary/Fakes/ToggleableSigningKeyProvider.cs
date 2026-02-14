using HoneyDrunk.Auth.Secrets;
using Microsoft.IdentityModel.Tokens;

namespace HoneyDrunk.Auth.Canary.Fakes;

/// <summary>
/// Toggleable signing key provider for canary testing.
/// Sits underneath CachingSigningKeyProvider to test cache semantics.
/// </summary>
internal sealed class ToggleableSigningKeyProvider(
    string issuer = "https://canary.honeydrunk.io",
    string audience = "api://canary",
    TimeSpan? clockSkew = null) : ISigningKeyProvider
{
    private readonly List<SecurityKey> _keys = [];
    private readonly TimeSpan _clockSkew = clockSkew ?? TimeSpan.FromMinutes(5);
    private int _getKeysCallCount;

    /// <summary>
    /// Gets or sets a value indicating whether the provider is available.
    /// When false, all operations throw to simulate Vault unavailability.
    /// </summary>
    public bool IsAvailable { get; set; } = true;

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

        if (!IsAvailable)
        {
            throw new InvalidOperationException("Vault unavailable (simulated)");
        }

        return Task.FromResult<IReadOnlyList<SecurityKey>>([.. _keys]);
    }

    /// <inheritdoc />
    public Task<string> GetIssuerAsync(CancellationToken cancellationToken = default)
    {
        if (!IsAvailable)
        {
            throw new InvalidOperationException("Vault unavailable (simulated)");
        }

        return Task.FromResult(issuer);
    }

    /// <inheritdoc />
    public Task<string> GetAudienceAsync(CancellationToken cancellationToken = default)
    {
        if (!IsAvailable)
        {
            throw new InvalidOperationException("Vault unavailable (simulated)");
        }

        return Task.FromResult(audience);
    }

    /// <inheritdoc />
    public Task<TimeSpan> GetClockSkewAsync(CancellationToken cancellationToken = default)
    {
        if (!IsAvailable)
        {
            throw new InvalidOperationException("Vault unavailable (simulated)");
        }

        return Task.FromResult(_clockSkew);
    }
}
