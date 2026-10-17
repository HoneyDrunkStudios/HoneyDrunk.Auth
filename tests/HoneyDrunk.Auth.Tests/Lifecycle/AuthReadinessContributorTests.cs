using HoneyDrunk.Auth.Lifecycle;
using HoneyDrunk.Auth.Secrets;
using HoneyDrunk.Auth.Tests.Helpers;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace HoneyDrunk.Auth.Tests.Lifecycle;

/// <summary>
/// Tests for <see cref="AuthReadinessContributor"/>.
/// </summary>
public sealed class AuthReadinessContributorTests
{
    /// <summary>
    /// Tests that Name returns expected value.
    /// </summary>
    [Fact]
    public void Name_ReturnsAuth()
    {
        // Arrange
        var keyProvider = new InMemorySigningKeyProvider();
        var contributor = new AuthReadinessContributor(keyProvider, NullLogger<AuthReadinessContributor>.Instance);

        // Assert
        Assert.Equal("Auth", contributor.Name);
    }

    /// <summary>
    /// Tests that Priority returns expected value.
    /// </summary>
    [Fact]
    public void Priority_Returns50()
    {
        // Arrange
        var keyProvider = new InMemorySigningKeyProvider();
        var contributor = new AuthReadinessContributor(keyProvider, NullLogger<AuthReadinessContributor>.Instance);

        // Assert
        Assert.Equal(50, contributor.Priority);
    }

    /// <summary>
    /// Tests that IsRequired returns true.
    /// </summary>
    [Fact]
    public void IsRequired_ReturnsTrue()
    {
        // Arrange
        var keyProvider = new InMemorySigningKeyProvider();
        var contributor = new AuthReadinessContributor(keyProvider, NullLogger<AuthReadinessContributor>.Instance);

        // Assert
        Assert.True(contributor.IsRequired);
    }

    /// <summary>
    /// Tests that CheckReadinessAsync returns ready when all config is available.
    /// </summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Fact]
    public async Task CheckReadinessAsync_AllConfigAvailable_ReturnsReady()
    {
        // Arrange
        var keyProvider = new InMemorySigningKeyProvider()
            .WithIssuer("https://issuer.example.com")
            .WithAudience("api://test")
            .AddKey("test-key", new byte[32]);
        var contributor = new AuthReadinessContributor(keyProvider, NullLogger<AuthReadinessContributor>.Instance);

        // Act
        var (isReady, reason) = await contributor.CheckReadinessAsync(CancellationToken.None);

        // Assert
        Assert.True(isReady);
        Assert.Equal("Auth system ready", reason);
    }

    /// <summary>
    /// Tests that CheckReadinessAsync returns not ready when issuer is empty.
    /// </summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Fact]
    public async Task CheckReadinessAsync_EmptyIssuer_ReturnsNotReady()
    {
        // Arrange
        var keyProvider = new InMemorySigningKeyProvider()
            .WithIssuer(string.Empty)
            .WithAudience("api://test")
            .AddKey("test-key", new byte[32]);
        var contributor = new AuthReadinessContributor(keyProvider, NullLogger<AuthReadinessContributor>.Instance);

        // Act
        var (isReady, reason) = await contributor.CheckReadinessAsync(CancellationToken.None);

        // Assert
        Assert.False(isReady);
        Assert.Equal("Auth secrets not fully configured", reason);
    }

    /// <summary>
    /// Tests that CheckReadinessAsync returns not ready when audience is empty.
    /// </summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Fact]
    public async Task CheckReadinessAsync_EmptyAudience_ReturnsNotReady()
    {
        // Arrange
        var keyProvider = new InMemorySigningKeyProvider()
            .WithIssuer("https://issuer.example.com")
            .WithAudience(string.Empty)
            .AddKey("test-key", new byte[32]);
        var contributor = new AuthReadinessContributor(keyProvider, NullLogger<AuthReadinessContributor>.Instance);

        // Act
        var (isReady, reason) = await contributor.CheckReadinessAsync(CancellationToken.None);

        // Assert
        Assert.False(isReady);
        Assert.Equal("Auth secrets not fully configured", reason);
    }

    /// <summary>
    /// Tests that CheckReadinessAsync returns not ready when no signing keys.
    /// </summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Fact]
    public async Task CheckReadinessAsync_NoSigningKeys_ReturnsNotReady()
    {
        // Arrange
        var keyProvider = new InMemorySigningKeyProvider()
            .WithIssuer("https://issuer.example.com")
            .WithAudience("api://test");
        var contributor = new AuthReadinessContributor(keyProvider, NullLogger<AuthReadinessContributor>.Instance);

        // Act
        var (isReady, reason) = await contributor.CheckReadinessAsync(CancellationToken.None);

        // Assert
        Assert.False(isReady);
        Assert.Equal("Auth secrets not fully configured", reason);
    }

    /// <summary>
    /// Tests that CheckReadinessAsync returns not ready when exception is thrown.
    /// </summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Fact]
    public async Task CheckReadinessAsync_ExceptionThrown_ReturnsNotReady()
    {
        // Arrange
        var keyProvider = Substitute.For<ISigningKeyProvider>();
        keyProvider.GetIssuerAsync(Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Vault connection failed"));
        var contributor = new AuthReadinessContributor(keyProvider, NullLogger<AuthReadinessContributor>.Instance);

        // Act
        var (isReady, reason) = await contributor.CheckReadinessAsync(CancellationToken.None);

        // Assert
        Assert.False(isReady);
        Assert.Contains("Failed to verify Auth secrets", reason);
        Assert.Contains("Vault connection failed", reason);
    }

    /// <summary>
    /// Tests that constructor throws when keyProvider is null.
    /// </summary>
    [Fact]
    public void Constructor_NullKeyProvider_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new AuthReadinessContributor(null!, NullLogger<AuthReadinessContributor>.Instance));
    }

    /// <summary>
    /// Tests that constructor throws when logger is null.
    /// </summary>
    [Fact]
    public void Constructor_NullLogger_ThrowsArgumentNullException()
    {
        var keyProvider = new InMemorySigningKeyProvider();
        Assert.Throws<ArgumentNullException>(() =>
            new AuthReadinessContributor(keyProvider, null!));
    }
}
