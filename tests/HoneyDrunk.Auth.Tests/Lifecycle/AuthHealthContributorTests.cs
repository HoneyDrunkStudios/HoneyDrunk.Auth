using HoneyDrunk.Auth.Lifecycle;
using HoneyDrunk.Auth.Secrets;
using HoneyDrunk.Auth.Tests.Helpers;
using HoneyDrunk.Kernel.Abstractions.Health;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace HoneyDrunk.Auth.Tests.Lifecycle;

/// <summary>
/// Tests for <see cref="AuthHealthContributor"/>.
/// </summary>
public sealed class AuthHealthContributorTests
{
    /// <summary>
    /// Tests that Name returns expected value.
    /// </summary>
    [Fact]
    public void Name_ReturnsAuth()
    {
        // Arrange
        var keyProvider = new InMemorySigningKeyProvider();
        var contributor = new AuthHealthContributor(keyProvider, NullLogger<AuthHealthContributor>.Instance);

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
        var contributor = new AuthHealthContributor(keyProvider, NullLogger<AuthHealthContributor>.Instance);

        // Assert
        Assert.Equal(50, contributor.Priority);
    }

    /// <summary>
    /// Tests that IsCritical returns true.
    /// </summary>
    [Fact]
    public void IsCritical_ReturnsTrue()
    {
        // Arrange
        var keyProvider = new InMemorySigningKeyProvider();
        var contributor = new AuthHealthContributor(keyProvider, NullLogger<AuthHealthContributor>.Instance);

        // Assert
        Assert.True(contributor.IsCritical);
    }

    /// <summary>
    /// Tests that CheckHealthAsync returns healthy when signing keys are available.
    /// </summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Fact]
    public async Task CheckHealthAsync_SigningKeysAvailable_ReturnsHealthy()
    {
        // Arrange
        var keyProvider = new InMemorySigningKeyProvider()
            .AddKey("test-key", new byte[32]);
        var contributor = new AuthHealthContributor(keyProvider, NullLogger<AuthHealthContributor>.Instance);

        // Act
        var (status, message) = await contributor.CheckHealthAsync(CancellationToken.None);

        // Assert
        Assert.Equal(HealthStatus.Healthy, status);
        Assert.Contains("1 signing key(s) available", message);
    }

    /// <summary>
    /// Tests that CheckHealthAsync returns healthy with multiple keys.
    /// </summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Fact]
    public async Task CheckHealthAsync_MultipleSigningKeys_ReturnsHealthyWithCount()
    {
        // Arrange
        var keyProvider = new InMemorySigningKeyProvider()
            .AddKey("key-1", new byte[32])
            .AddKey("key-2", new byte[32])
            .AddKey("key-3", new byte[32]);
        var contributor = new AuthHealthContributor(keyProvider, NullLogger<AuthHealthContributor>.Instance);

        // Act
        var (status, message) = await contributor.CheckHealthAsync(CancellationToken.None);

        // Assert
        Assert.Equal(HealthStatus.Healthy, status);
        Assert.Contains("3 signing key(s) available", message);
    }

    /// <summary>
    /// Tests that CheckHealthAsync returns unhealthy when no signing keys are available.
    /// </summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Fact]
    public async Task CheckHealthAsync_NoSigningKeys_ReturnsUnhealthy()
    {
        // Arrange
        var keyProvider = new InMemorySigningKeyProvider();
        var contributor = new AuthHealthContributor(keyProvider, NullLogger<AuthHealthContributor>.Instance);

        // Act
        var (status, message) = await contributor.CheckHealthAsync(CancellationToken.None);

        // Assert
        Assert.Equal(HealthStatus.Unhealthy, status);
        Assert.Equal("No signing keys available", message);
    }

    /// <summary>
    /// Tests that CheckHealthAsync returns unhealthy when exception is thrown.
    /// </summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Fact]
    public async Task CheckHealthAsync_ExceptionThrown_ReturnsUnhealthy()
    {
        // Arrange
        var keyProvider = Substitute.For<ISigningKeyProvider>();
        keyProvider.GetSigningKeysAsync(Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Vault connection failed"));
        var contributor = new AuthHealthContributor(keyProvider, NullLogger<AuthHealthContributor>.Instance);

        // Act
        var (status, message) = await contributor.CheckHealthAsync(CancellationToken.None);

        // Assert
        Assert.Equal(HealthStatus.Unhealthy, status);
        Assert.Contains("Failed to retrieve signing keys", message);
        Assert.Contains("Vault connection failed", message);
    }

    /// <summary>
    /// Tests that constructor throws when keyProvider is null.
    /// </summary>
    [Fact]
    public void Constructor_NullKeyProvider_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new AuthHealthContributor(null!, NullLogger<AuthHealthContributor>.Instance));
    }

    /// <summary>
    /// Tests that constructor throws when logger is null.
    /// </summary>
    [Fact]
    public void Constructor_NullLogger_ThrowsArgumentNullException()
    {
        var keyProvider = new InMemorySigningKeyProvider();
        Assert.Throws<ArgumentNullException>(() =>
            new AuthHealthContributor(keyProvider, null!));
    }
}
