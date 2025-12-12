using HoneyDrunk.Auth.Lifecycle;
using HoneyDrunk.Auth.Secrets;
using HoneyDrunk.Auth.Tests.Helpers;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace HoneyDrunk.Auth.Tests.Lifecycle;

/// <summary>
/// Tests for <see cref="AuthStartupHook"/>.
/// </summary>
public sealed class AuthStartupHookTests
{
    /// <summary>
    /// Tests that Priority returns expected value.
    /// </summary>
    [Fact]
    public void Priority_Returns100()
    {
        // Arrange
        var keyProvider = new InMemorySigningKeyProvider();
        var hook = new AuthStartupHook(keyProvider, NullLogger<AuthStartupHook>.Instance);

        // Assert
        Assert.Equal(100, hook.Priority);
    }

    /// <summary>
    /// Tests that ExecuteAsync succeeds when all config is valid.
    /// </summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Fact]
    public async Task ExecuteAsync_AllConfigValid_Succeeds()
    {
        // Arrange
        var keyProvider = new InMemorySigningKeyProvider()
            .WithIssuer("https://issuer.example.com")
            .WithAudience("api://test")
            .AddKey("test-key", new byte[32]);
        var hook = new AuthStartupHook(keyProvider, NullLogger<AuthStartupHook>.Instance);

        // Act & Assert - should not throw
        await hook.ExecuteAsync(CancellationToken.None);
    }

    /// <summary>
    /// Tests that ExecuteAsync throws when issuer is empty.
    /// </summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Fact]
    public async Task ExecuteAsync_EmptyIssuer_ThrowsInvalidOperationException()
    {
        // Arrange
        var keyProvider = new InMemorySigningKeyProvider()
            .WithIssuer(string.Empty)
            .WithAudience("api://test")
            .AddKey("test-key", new byte[32]);
        var hook = new AuthStartupHook(keyProvider, NullLogger<AuthStartupHook>.Instance);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            hook.ExecuteAsync(CancellationToken.None));
        Assert.Contains("auth:issuer is empty or missing", ex.Message);
    }

    /// <summary>
    /// Tests that ExecuteAsync throws when issuer is whitespace.
    /// </summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Fact]
    public async Task ExecuteAsync_WhitespaceIssuer_ThrowsInvalidOperationException()
    {
        // Arrange
        var keyProvider = new InMemorySigningKeyProvider()
            .WithIssuer("   ")
            .WithAudience("api://test")
            .AddKey("test-key", new byte[32]);
        var hook = new AuthStartupHook(keyProvider, NullLogger<AuthStartupHook>.Instance);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            hook.ExecuteAsync(CancellationToken.None));
        Assert.Contains("auth:issuer is empty or missing", ex.Message);
    }

    /// <summary>
    /// Tests that ExecuteAsync throws when audience is empty.
    /// </summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Fact]
    public async Task ExecuteAsync_EmptyAudience_ThrowsInvalidOperationException()
    {
        // Arrange
        var keyProvider = new InMemorySigningKeyProvider()
            .WithIssuer("https://issuer.example.com")
            .WithAudience(string.Empty)
            .AddKey("test-key", new byte[32]);
        var hook = new AuthStartupHook(keyProvider, NullLogger<AuthStartupHook>.Instance);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            hook.ExecuteAsync(CancellationToken.None));
        Assert.Contains("auth:audience is empty or missing", ex.Message);
    }

    /// <summary>
    /// Tests that ExecuteAsync throws when no signing keys.
    /// </summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Fact]
    public async Task ExecuteAsync_NoSigningKeys_ThrowsInvalidOperationException()
    {
        // Arrange
        var keyProvider = new InMemorySigningKeyProvider()
            .WithIssuer("https://issuer.example.com")
            .WithAudience("api://test");
        var hook = new AuthStartupHook(keyProvider, NullLogger<AuthStartupHook>.Instance);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            hook.ExecuteAsync(CancellationToken.None));
        Assert.Contains("No active signing keys found", ex.Message);
    }

    /// <summary>
    /// Tests that ExecuteAsync includes all errors in exception message.
    /// </summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Fact]
    public async Task ExecuteAsync_MultipleErrors_IncludesAllInExceptionMessage()
    {
        // Arrange
        var keyProvider = new InMemorySigningKeyProvider()
            .WithIssuer(string.Empty)
            .WithAudience(string.Empty);
        var hook = new AuthStartupHook(keyProvider, NullLogger<AuthStartupHook>.Instance);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            hook.ExecuteAsync(CancellationToken.None));
        Assert.Contains("auth:issuer is empty or missing", ex.Message);
        Assert.Contains("auth:audience is empty or missing", ex.Message);
        Assert.Contains("No active signing keys found", ex.Message);
    }

    /// <summary>
    /// Tests that ExecuteAsync handles issuer retrieval exception.
    /// </summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Fact]
    public async Task ExecuteAsync_IssuerRetrievalFails_IncludesErrorInException()
    {
        // Arrange
        var keyProvider = Substitute.For<ISigningKeyProvider>();
        keyProvider.GetIssuerAsync(Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Vault issuer error"));
        keyProvider.GetAudienceAsync(Arg.Any<CancellationToken>())
            .Returns("api://test");
        keyProvider.GetSigningKeysAsync(Arg.Any<CancellationToken>())
            .Returns(new[] { TestTokenGenerator.GenerateKey() });
        var hook = new AuthStartupHook(keyProvider, NullLogger<AuthStartupHook>.Instance);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            hook.ExecuteAsync(CancellationToken.None));
        Assert.Contains("Failed to retrieve auth:issuer", ex.Message);
        Assert.Contains("Vault issuer error", ex.Message);
    }

    /// <summary>
    /// Tests that ExecuteAsync handles audience retrieval exception.
    /// </summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Fact]
    public async Task ExecuteAsync_AudienceRetrievalFails_IncludesErrorInException()
    {
        // Arrange
        var keyProvider = Substitute.For<ISigningKeyProvider>();
        keyProvider.GetIssuerAsync(Arg.Any<CancellationToken>())
            .Returns("https://issuer.example.com");
        keyProvider.GetAudienceAsync(Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Vault audience error"));
        keyProvider.GetSigningKeysAsync(Arg.Any<CancellationToken>())
            .Returns(new[] { TestTokenGenerator.GenerateKey() });
        var hook = new AuthStartupHook(keyProvider, NullLogger<AuthStartupHook>.Instance);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            hook.ExecuteAsync(CancellationToken.None));
        Assert.Contains("Failed to retrieve auth:audience", ex.Message);
        Assert.Contains("Vault audience error", ex.Message);
    }

    /// <summary>
    /// Tests that ExecuteAsync handles signing keys retrieval exception.
    /// </summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Fact]
    public async Task ExecuteAsync_SigningKeysRetrievalFails_IncludesErrorInException()
    {
        // Arrange
        var keyProvider = Substitute.For<ISigningKeyProvider>();
        keyProvider.GetIssuerAsync(Arg.Any<CancellationToken>())
            .Returns("https://issuer.example.com");
        keyProvider.GetAudienceAsync(Arg.Any<CancellationToken>())
            .Returns("api://test");
        keyProvider.GetSigningKeysAsync(Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Vault keys error"));
        var hook = new AuthStartupHook(keyProvider, NullLogger<AuthStartupHook>.Instance);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            hook.ExecuteAsync(CancellationToken.None));
        Assert.Contains("Failed to retrieve auth:signing_keys", ex.Message);
        Assert.Contains("Vault keys error", ex.Message);
    }

    /// <summary>
    /// Tests that constructor throws when keyProvider is null.
    /// </summary>
    [Fact]
    public void Constructor_NullKeyProvider_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new AuthStartupHook(null!, NullLogger<AuthStartupHook>.Instance));
    }

    /// <summary>
    /// Tests that constructor throws when logger is null.
    /// </summary>
    [Fact]
    public void Constructor_NullLogger_ThrowsArgumentNullException()
    {
        var keyProvider = new InMemorySigningKeyProvider();
        Assert.Throws<ArgumentNullException>(() =>
            new AuthStartupHook(keyProvider, null!));
    }
}
