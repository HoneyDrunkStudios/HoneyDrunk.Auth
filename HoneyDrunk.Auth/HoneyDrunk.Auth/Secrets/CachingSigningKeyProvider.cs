using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace HoneyDrunk.Auth.Secrets;

/// <summary>
/// Caching decorator for <see cref="ISigningKeyProvider"/> that maintains a last-known-good cache.
/// </summary>
/// <remarks>
/// Implements TTL-based caching with fail-safe behavior:
/// - Returns cached values when available
/// - Refreshes cache on TTL expiry
/// - Falls back to last-known-good on Vault failures
/// - Supports refresh on unknown key ID.
/// </remarks>
internal sealed class CachingSigningKeyProvider : ISigningKeyProvider
{
    private readonly ISigningKeyProvider _inner;
    private readonly ILogger<CachingSigningKeyProvider> _logger;
    private readonly TimeSpan _ttl;
    private readonly bool _refreshOnUnknownKid;
    private readonly object _lock = new();

    private volatile CacheEntry<IReadOnlyList<SecurityKey>>? _keysCache;
    private volatile CacheEntry<string>? _issuerCache;
    private volatile CacheEntry<string>? _audienceCache;
    private volatile CacheEntry<TimeSpan>? _clockSkewCache;

    private volatile bool _refreshInProgress;

    public CachingSigningKeyProvider(
        ISigningKeyProvider inner,
        IOptions<AuthOptions> options,
        ILogger<CachingSigningKeyProvider> logger)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        var opts = options?.Value ?? new AuthOptions();
        _ttl = opts.CacheTtl;
        _refreshOnUnknownKid = opts.RefreshOnUnknownKeyId;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<SecurityKey>> GetSigningKeysAsync(CancellationToken cancellationToken = default)
    {
        var cache = _keysCache;
        if (cache is not null && !cache.IsExpired)
        {
            return cache.Value;
        }

        return await RefreshKeysAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<string> GetIssuerAsync(CancellationToken cancellationToken = default)
    {
        var cache = _issuerCache;
        if (cache is not null && !cache.IsExpired)
        {
            return cache.Value;
        }

        return await RefreshIssuerAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<string> GetAudienceAsync(CancellationToken cancellationToken = default)
    {
        var cache = _audienceCache;
        if (cache is not null && !cache.IsExpired)
        {
            return cache.Value;
        }

        return await RefreshAudienceAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<TimeSpan> GetClockSkewAsync(CancellationToken cancellationToken = default)
    {
        var cache = _clockSkewCache;
        if (cache is not null && !cache.IsExpired)
        {
            return cache.Value;
        }

        return await RefreshClockSkewAsync(cancellationToken);
    }

    /// <summary>
    /// Attempts to refresh keys if an unknown key ID was encountered.
    /// Returns true if refresh occurred and new keys are available.
    /// </summary>
    public async Task<bool> TryRefreshForUnknownKeyIdAsync(string keyId, CancellationToken cancellationToken = default)
    {
        if (!_refreshOnUnknownKid)
        {
            return false;
        }

        // Prevent concurrent refresh attempts for the same reason
        lock (_lock)
        {
            if (_refreshInProgress)
            {
                return false;
            }

            _refreshInProgress = true;
        }

        try
        {
            _logger.LogInformation("Attempting key refresh for unknown key ID");

            var keys = await _inner.GetSigningKeysAsync(cancellationToken);
            _keysCache = new CacheEntry<IReadOnlyList<SecurityKey>>(keys, _ttl);

            // Check if the requested key ID is now available
            var found = keys.Any(k => string.Equals(k.KeyId, keyId, StringComparison.Ordinal));
            if (found)
            {
                _logger.LogInformation("Key refresh successful, key ID now available");
            }

            return found;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Key refresh for unknown key ID failed");
            return false;
        }
        finally
        {
            _refreshInProgress = false;
        }
    }

    /// <summary>
    /// Preloads the cache with values from Vault. Called at startup.
    /// Throws if Vault is unavailable.
    /// </summary>
    public async Task PreloadAsync(CancellationToken cancellationToken)
    {
        // Preload in parallel
        var keysTask = _inner.GetSigningKeysAsync(cancellationToken);
        var issuerTask = _inner.GetIssuerAsync(cancellationToken);
        var audienceTask = _inner.GetAudienceAsync(cancellationToken);
        var clockSkewTask = _inner.GetClockSkewAsync(cancellationToken);

        await Task.WhenAll(keysTask, issuerTask, audienceTask, clockSkewTask);

        _keysCache = new CacheEntry<IReadOnlyList<SecurityKey>>(await keysTask, _ttl);
        _issuerCache = new CacheEntry<string>(await issuerTask, _ttl);
        _audienceCache = new CacheEntry<string>(await audienceTask, _ttl);
        _clockSkewCache = new CacheEntry<TimeSpan>(await clockSkewTask, _ttl);

        _logger.LogDebug("Auth cache preloaded with TTL of {Ttl}", _ttl);
    }

    private async Task<IReadOnlyList<SecurityKey>> RefreshKeysAsync(CancellationToken cancellationToken)
    {
        var lastKnown = _keysCache?.Value;

        try
        {
            var keys = await _inner.GetSigningKeysAsync(cancellationToken);
            _keysCache = new CacheEntry<IReadOnlyList<SecurityKey>>(keys, _ttl);
            return keys;
        }
        catch (Exception ex) when (lastKnown is not null)
        {
            _logger.LogWarning(ex, "Failed to refresh signing keys, using last-known-good cache");

            // Extend the cache entry with a shorter TTL for retry
            _keysCache = new CacheEntry<IReadOnlyList<SecurityKey>>(lastKnown, TimeSpan.FromSeconds(30));
            return lastKnown;
        }
    }

    private async Task<string> RefreshIssuerAsync(CancellationToken cancellationToken)
    {
        var lastKnown = _issuerCache?.Value;

        try
        {
            var issuer = await _inner.GetIssuerAsync(cancellationToken);
            _issuerCache = new CacheEntry<string>(issuer, _ttl);
            return issuer;
        }
        catch (Exception ex) when (lastKnown is not null)
        {
            _logger.LogWarning(ex, "Failed to refresh issuer, using last-known-good cache");
            _issuerCache = new CacheEntry<string>(lastKnown, TimeSpan.FromSeconds(30));
            return lastKnown;
        }
    }

    private async Task<string> RefreshAudienceAsync(CancellationToken cancellationToken)
    {
        var lastKnown = _audienceCache?.Value;

        try
        {
            var audience = await _inner.GetAudienceAsync(cancellationToken);
            _audienceCache = new CacheEntry<string>(audience, _ttl);
            return audience;
        }
        catch (Exception ex) when (lastKnown is not null)
        {
            _logger.LogWarning(ex, "Failed to refresh audience, using last-known-good cache");
            _audienceCache = new CacheEntry<string>(lastKnown, TimeSpan.FromSeconds(30));
            return lastKnown;
        }
    }

    private async Task<TimeSpan> RefreshClockSkewAsync(CancellationToken cancellationToken)
    {
        var lastKnown = _clockSkewCache?.Value;

        try
        {
            var clockSkew = await _inner.GetClockSkewAsync(cancellationToken);
            _clockSkewCache = new CacheEntry<TimeSpan>(clockSkew, _ttl);
            return clockSkew;
        }
        catch (Exception ex) when (lastKnown.HasValue)
        {
            _logger.LogWarning(ex, "Failed to refresh clock skew, using last-known-good cache");
            _clockSkewCache = new CacheEntry<TimeSpan>(lastKnown.Value, TimeSpan.FromSeconds(30));
            return lastKnown.Value;
        }
    }

    private sealed class CacheEntry<T>
    {
        private readonly DateTimeOffset _expiresAt;

        public CacheEntry(T value, TimeSpan ttl)
        {
            Value = value;
            _expiresAt = DateTimeOffset.UtcNow.Add(ttl);
        }

        public T Value { get; }

        public bool IsExpired => DateTimeOffset.UtcNow >= _expiresAt;
    }
}
