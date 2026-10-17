using HoneyDrunk.Audit.Abstractions;
using HoneyDrunk.Auth.Authentication;
using HoneyDrunk.Auth.Lifecycle;
using HoneyDrunk.Auth.Secrets;
using HoneyDrunk.Auth.Tests.Helpers;
using Microsoft.Extensions.Logging;
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
        var hook = new AuthStartupHook(keyProvider, Substitute.For<IAuditLog>(), NullLogger<AuthStartupHook>.Instance);

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
        var hook = new AuthStartupHook(keyProvider, Substitute.For<IAuditLog>(), NullLogger<AuthStartupHook>.Instance);

        // Act — capture any exception via Record.ExceptionAsync so the "must not
        // throw" property is asserted explicitly (Sonar S2699 blocker).
        var exception = await Record.ExceptionAsync(() => hook.ExecuteAsync(CancellationToken.None));

        // Assert
        Assert.Null(exception);
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
        var hook = new AuthStartupHook(keyProvider, Substitute.For<IAuditLog>(), NullLogger<AuthStartupHook>.Instance);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            hook.ExecuteAsync(CancellationToken.None));
        Assert.Contains("Auth:Issuer is empty or missing", ex.Message);
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
        var hook = new AuthStartupHook(keyProvider, Substitute.For<IAuditLog>(), NullLogger<AuthStartupHook>.Instance);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            hook.ExecuteAsync(CancellationToken.None));
        Assert.Contains("Auth:Issuer is empty or missing", ex.Message);
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
        var hook = new AuthStartupHook(keyProvider, Substitute.For<IAuditLog>(), NullLogger<AuthStartupHook>.Instance);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            hook.ExecuteAsync(CancellationToken.None));
        Assert.Contains("Auth:Audience is empty or missing", ex.Message);
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
        var hook = new AuthStartupHook(keyProvider, Substitute.For<IAuditLog>(), NullLogger<AuthStartupHook>.Instance);

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
        var hook = new AuthStartupHook(keyProvider, Substitute.For<IAuditLog>(), NullLogger<AuthStartupHook>.Instance);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            hook.ExecuteAsync(CancellationToken.None));
        Assert.Contains("Auth:Issuer is empty or missing", ex.Message);
        Assert.Contains("Auth:Audience is empty or missing", ex.Message);
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
            .Returns([TestTokenGenerator.GenerateKey()]);
        var hook = new AuthStartupHook(keyProvider, Substitute.For<IAuditLog>(), NullLogger<AuthStartupHook>.Instance);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            hook.ExecuteAsync(CancellationToken.None));
        Assert.Contains("Failed to retrieve Auth:Issuer", ex.Message);
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
            .Returns([TestTokenGenerator.GenerateKey()]);
        var hook = new AuthStartupHook(keyProvider, Substitute.For<IAuditLog>(), NullLogger<AuthStartupHook>.Instance);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            hook.ExecuteAsync(CancellationToken.None));
        Assert.Contains("Failed to retrieve Auth:Audience", ex.Message);
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
        var hook = new AuthStartupHook(keyProvider, Substitute.For<IAuditLog>(), NullLogger<AuthStartupHook>.Instance);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            hook.ExecuteAsync(CancellationToken.None));
        Assert.Contains("Failed to retrieve Jwt--SigningKeys", ex.Message);
        Assert.Contains("Vault keys error", ex.Message);
    }

    /// <summary>
    /// Tests that ExecuteAsync logs a warning when the no-op audit sink is active.
    /// </summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Fact]
    public async Task ExecuteAsync_NullAuditLog_LogsWarning()
    {
        // Arrange
        var keyProvider = new InMemorySigningKeyProvider()
            .WithIssuer("https://issuer.example.com")
            .WithAudience("api://test")
            .AddKey("test-key", new byte[32]);
        var logger = new CapturingLogger<AuthStartupHook>();
        var hook = new AuthStartupHook(keyProvider, new NullAuditLog(), logger);

        // Act
        await hook.ExecuteAsync(CancellationToken.None);

        // Assert
        Assert.Contains(logger.Messages, message => message.Contains("NullAuditLog fallback is active", StringComparison.Ordinal));
    }

    /// <summary>
    /// Tests that constructor throws when keyProvider is null.
    /// </summary>
    [Fact]
    public void Constructor_NullKeyProvider_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new AuthStartupHook(null!, Substitute.For<IAuditLog>(), NullLogger<AuthStartupHook>.Instance));
    }

    /// <summary>
    /// Tests that constructor throws when logger is null.
    /// </summary>
    [Fact]
    public void Constructor_NullLogger_ThrowsArgumentNullException()
    {
        var keyProvider = new InMemorySigningKeyProvider();
        Assert.Throws<ArgumentNullException>(() =>
            new AuthStartupHook(keyProvider, Substitute.For<IAuditLog>(), null!));
    }

    private sealed class CapturingLogger<T> : ILogger<T>
    {
        public List<string> Messages { get; } = [];

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            Messages.Add(formatter(state, exception));
        }
    }
}
