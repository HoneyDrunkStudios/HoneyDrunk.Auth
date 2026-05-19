using HoneyDrunk.Auth.Secrets;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace HoneyDrunk.Auth.Tests.Secrets;

/// <summary>
/// Tests for <see cref="CachingSigningKeyProvider"/>.
/// </summary>
public sealed class CachingSigningKeyProviderTests
{
    /// <summary>
    /// Verifies that the constructor rejects non-positive cache durations.
    /// </summary>
    [Fact]
    public void ConstructorRejectsNonPositiveCacheTtl()
    {
        var inner = new RecordingSigningKeyProvider();
        var options = Options.Create(new AuthOptions { CacheTtl = TimeSpan.Zero });

        Assert.Throws<ArgumentOutOfRangeException>(() => CreateProvider(inner, options));
    }

    /// <summary>
    /// Verifies that fetched values are reused while the cache entry remains fresh.
    /// </summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Fact]
    public async Task GetMethodsReuseFreshCacheEntries()
    {
        var inner = new RecordingSigningKeyProvider();
        var provider = CreateProvider(inner);

        var keys1 = await provider.GetSigningKeysAsync();
        var keys2 = await provider.GetSigningKeysAsync();
        var issuer1 = await provider.GetIssuerAsync();
        var issuer2 = await provider.GetIssuerAsync();
        var audience1 = await provider.GetAudienceAsync();
        var audience2 = await provider.GetAudienceAsync();
        var clockSkew1 = await provider.GetClockSkewAsync();
        var clockSkew2 = await provider.GetClockSkewAsync();

        Assert.Same(keys1, keys2);
        Assert.Equal("issuer-1", issuer1);
        Assert.Equal(issuer1, issuer2);
        Assert.Equal("audience-1", audience1);
        Assert.Equal(audience1, audience2);
        Assert.Equal(TimeSpan.FromSeconds(31), clockSkew1);
        Assert.Equal(clockSkew1, clockSkew2);
        Assert.Equal(1, inner.SigningKeyCalls);
        Assert.Equal(1, inner.IssuerCalls);
        Assert.Equal(1, inner.AudienceCalls);
        Assert.Equal(1, inner.ClockSkewCalls);
    }

    /// <summary>
    /// Verifies that preloading populates every cache slot and subsequent reads use the cache.
    /// </summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Fact]
    public async Task PreloadAsyncPopulatesAllCachedValues()
    {
        var inner = new RecordingSigningKeyProvider();
        var provider = CreateProvider(inner);

        await provider.PreloadAsync(CancellationToken.None);

        Assert.Equal("issuer-1", await provider.GetIssuerAsync());
        Assert.Equal("audience-1", await provider.GetAudienceAsync());
        Assert.Equal(TimeSpan.FromSeconds(31), await provider.GetClockSkewAsync());
        Assert.Single(await provider.GetSigningKeysAsync());
        Assert.Equal(1, inner.SigningKeyCalls);
        Assert.Equal(1, inner.IssuerCalls);
        Assert.Equal(1, inner.AudienceCalls);
        Assert.Equal(1, inner.ClockSkewCalls);
    }

    /// <summary>
    /// Verifies that refresh failures fall back to the last known good cached values.
    /// </summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Fact]
    public async Task ExpiredCacheFallsBackToLastKnownGoodWhenInnerFails()
    {
        var inner = new RecordingSigningKeyProvider();
        var provider = CreateProvider(inner, Options.Create(new AuthOptions { CacheTtl = TimeSpan.FromMilliseconds(1) }));
        var keys = await provider.GetSigningKeysAsync();
        var issuer = await provider.GetIssuerAsync();
        var audience = await provider.GetAudienceAsync();
        var clockSkew = await provider.GetClockSkewAsync();

        await Task.Delay(20);
        inner.FailAll = true;

        Assert.Same(keys, await provider.GetSigningKeysAsync());
        Assert.Equal(issuer, await provider.GetIssuerAsync());
        Assert.Equal(audience, await provider.GetAudienceAsync());
        Assert.Equal(clockSkew, await provider.GetClockSkewAsync());
    }

    /// <summary>
    /// Verifies that unknown key refresh is disabled when the option is disabled.
    /// </summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Fact]
    public async Task TryRefreshForUnknownKeyIdAsyncReturnsFalseWhenDisabled()
    {
        var inner = new RecordingSigningKeyProvider();
        var provider = CreateProvider(inner, Options.Create(new AuthOptions { RefreshOnUnknownKeyId = false }));

        var refreshed = await provider.TryRefreshForUnknownKeyIdAsync("key-1");

        Assert.False(refreshed);
        Assert.Equal(0, inner.SigningKeyCalls);
    }

    /// <summary>
    /// Verifies that unknown key refresh returns true only when the refreshed key set contains the requested key ID.
    /// </summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Fact]
    public async Task TryRefreshForUnknownKeyIdAsyncReturnsWhetherKeyWasFound()
    {
        var inner = new RecordingSigningKeyProvider();
        var provider = CreateProvider(inner);

        Assert.True(await provider.TryRefreshForUnknownKeyIdAsync("key-1"));
        Assert.False(await provider.TryRefreshForUnknownKeyIdAsync("missing-key"));
    }

    /// <summary>
    /// Verifies that unknown key refresh failures are swallowed and reported as unsuccessful refreshes.
    /// </summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Fact]
    public async Task TryRefreshForUnknownKeyIdAsyncReturnsFalseWhenInnerFails()
    {
        var inner = new RecordingSigningKeyProvider { FailKeys = true };
        var provider = CreateProvider(inner);

        var refreshed = await provider.TryRefreshForUnknownKeyIdAsync("key-1");

        Assert.False(refreshed);
    }

    private static CachingSigningKeyProvider CreateProvider(
        ISigningKeyProvider inner,
        IOptions<AuthOptions>? options = null)
    {
        return new CachingSigningKeyProvider(
            inner,
            options ?? Options.Create(new AuthOptions()),
            NullLogger<CachingSigningKeyProvider>.Instance);
    }

    private sealed class RecordingSigningKeyProvider : ISigningKeyProvider
    {
        private readonly IReadOnlyList<SecurityKey> _keys = new[]
        {
            new SymmetricSecurityKey(new byte[32]) { KeyId = "key-1" },
        };

        public bool FailAll { get; set; }

        public bool FailKeys { get; set; }

        public int SigningKeyCalls { get; private set; }

        public int IssuerCalls { get; private set; }

        public int AudienceCalls { get; private set; }

        public int ClockSkewCalls { get; private set; }

        public Task<IReadOnlyList<SecurityKey>> GetSigningKeysAsync(CancellationToken cancellationToken = default)
        {
            SigningKeyCalls++;
            if (FailAll || FailKeys)
            {
                return Task.FromException<IReadOnlyList<SecurityKey>>(new InvalidOperationException("keys unavailable"));
            }

            return Task.FromResult(_keys);
        }

        public Task<string> GetIssuerAsync(CancellationToken cancellationToken = default)
        {
            IssuerCalls++;
            return FailAll
                ? Task.FromException<string>(new InvalidOperationException("issuer unavailable"))
                : Task.FromResult($"issuer-{IssuerCalls}");
        }

        public Task<string> GetAudienceAsync(CancellationToken cancellationToken = default)
        {
            AudienceCalls++;
            return FailAll
                ? Task.FromException<string>(new InvalidOperationException("audience unavailable"))
                : Task.FromResult($"audience-{AudienceCalls}");
        }

        public Task<TimeSpan> GetClockSkewAsync(CancellationToken cancellationToken = default)
        {
            ClockSkewCalls++;
            return FailAll
                ? Task.FromException<TimeSpan>(new InvalidOperationException("clock skew unavailable"))
                : Task.FromResult(TimeSpan.FromSeconds(30 + ClockSkewCalls));
        }
    }
}
