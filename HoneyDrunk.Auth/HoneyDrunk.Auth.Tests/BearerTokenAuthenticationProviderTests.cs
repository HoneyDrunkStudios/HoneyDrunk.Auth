using HoneyDrunk.Audit.Abstractions;
using HoneyDrunk.Auth.Abstractions;
using HoneyDrunk.Auth.Authentication;
using HoneyDrunk.Auth.Tests.Helpers;
using HoneyDrunk.Kernel.Abstractions.Context;
using HoneyDrunk.Kernel.Abstractions.Identity;
using HoneyDrunk.Kernel.Abstractions.Telemetry;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using NSubstitute;
using System.Security.Claims;
using System.Text;

namespace HoneyDrunk.Auth.Tests;

/// <summary>
/// Tests for <see cref="BearerTokenAuthenticationProvider"/>.
/// </summary>
public sealed class BearerTokenAuthenticationProviderTests
{
    private readonly InMemorySigningKeyProvider _keyProvider;
    private readonly ITelemetryActivityFactory _telemetryFactory;
    private readonly IAuditLog _auditLog;
    private readonly BearerTokenAuthenticationProvider _provider;

    /// <summary>
    /// Initializes a new instance of the <see cref="BearerTokenAuthenticationProviderTests"/> class.
    /// </summary>
    public BearerTokenAuthenticationProviderTests()
    {
        var key = TestTokenGenerator.GenerateKey();
        _keyProvider = new InMemorySigningKeyProvider()
            .AddKey(key.KeyId!, key.Key);

        _telemetryFactory = Substitute.For<ITelemetryActivityFactory>();
        _auditLog = Substitute.For<IAuditLog>();
        var gridContext = Substitute.For<IGridContext>();
        gridContext.TenantId.Returns(TenantId.Internal);
        gridContext.CorrelationId.Returns("corr-test");
        var gridContextAccessor = Substitute.For<IGridContextAccessor>();
        gridContextAccessor.GridContext.Returns(gridContext);

        var options = Options.Create(new AuthOptions());

        _provider = new BearerTokenAuthenticationProvider(
            _keyProvider,
            options,
            _telemetryFactory,
            _auditLog,
            gridContextAccessor,
            NullLogger<BearerTokenAuthenticationProvider>.Instance);
    }

    /// <summary>
    /// Tests that valid token returns successful authentication.
    /// </summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Fact]
    public async Task AuthenticateAsync_ValidToken_ReturnsSuccess()
    {
        // Arrange
        var key = _keyProvider.SigningKeys[0];
        var token = TestTokenGenerator.GenerateToken(
            key,
            subject: "user-123",
            name: "Test User",
            claims: [new Claim(JwtRegisteredClaimNames.Jti, "token-123")]);
        var credential = AuthCredential.Bearer(token);

        // Act
        var result = await _provider.AuthenticateAsync(credential);

        // Assert
        Assert.True(result.IsAuthenticated);
        Assert.NotNull(result.Identity);
        Assert.Equal("user-123", result.Identity.SubjectId);
        Assert.Equal("Test User", result.Identity.DisplayName);
        Assert.Equal(AuthScheme.Bearer, result.Identity.Scheme);
    }

    /// <summary>
    /// Tests that valid bearer tokens append a security audit entry without raw token material.
    /// </summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Fact]
    public async Task AuthenticateAsync_ValidToken_AppendsAuditEntry()
    {
        // Arrange
        AuditEntry? entry = null;
        _auditLog.AppendAsync(Arg.Do<AuditEntry>(e => entry = e), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        var key = _keyProvider.SigningKeys[0];
        var token = TestTokenGenerator.GenerateToken(
            key,
            subject: "user-123",
            name: "Test User",
            claims: [new Claim(JwtRegisteredClaimNames.Jti, "token-123")]);
        var credential = AuthCredential.Bearer(token);

        // Act
        var result = await _provider.AuthenticateAsync(credential);

        // Assert
        Assert.True(result.IsAuthenticated);
        Assert.NotNull(entry);
        Assert.Equal("auth.token.validate", entry.EventName);
        Assert.Equal(AuditCategory.Security, entry.Category);
        Assert.Equal(AuditOutcome.Succeeded, entry.Outcome);
        Assert.Equal("user-123", entry.Actor);
        Assert.Equal("auth.token", entry.Target.Type);
        Assert.Equal("token-123", entry.Target.Id);
        Assert.Equal(TenantId.Internal, entry.TenantId);
        Assert.Equal("corr-test", entry.CorrelationId);
        Assert.DoesNotContain(token, string.Join(" ", entry.Metadata?.Values ?? []));
        Assert.DoesNotContain(entry.Metadata ?? new Dictionary<string, string>(), kvp => kvp.Key.Contains("sub", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Tests that failed bearer token validation appends a denied audit entry.
    /// </summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Fact]
    public async Task AuthenticateAsync_InvalidSignature_AppendsDeniedAuditEntry()
    {
        // Arrange
        AuditEntry? entry = null;
        _auditLog.AppendAsync(Arg.Do<AuditEntry>(e => entry = e), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        var wrongKey = TestTokenGenerator.GenerateKey("wrong-key");
        var token = TestTokenGenerator.GenerateToken(wrongKey);
        var credential = AuthCredential.Bearer(token);

        // Act
        var result = await _provider.AuthenticateAsync(credential);

        // Assert
        Assert.False(result.IsAuthenticated);
        Assert.NotNull(entry);
        Assert.Equal("auth.token.validate", entry.EventName);
        Assert.Equal(AuditOutcome.Denied, entry.Outcome);
        Assert.Equal("anonymous", entry.Actor);
        Assert.Equal("unavailable", entry.Target.Id);
        Assert.Equal("InvalidSignature", entry.Reason);
        Assert.Equal("InvalidSignature", entry.Metadata?["failureCode"]);
        Assert.DoesNotContain(token, string.Join(" ", entry.Metadata?.Values ?? []));
    }

    /// <summary>
    /// Tests that expired token returns TokenExpired failure.
    /// </summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Fact]
    public async Task AuthenticateAsync_ExpiredToken_ReturnsTokenExpired()
    {
        // Arrange
        var key = _keyProvider.SigningKeys[0];
        var token = TestTokenGenerator.GenerateExpiredToken(key);
        var credential = AuthCredential.Bearer(token);

        // Act
        var result = await _provider.AuthenticateAsync(credential);

        // Assert
        Assert.False(result.IsAuthenticated);
        Assert.Equal(AuthenticationFailureCode.TokenExpired, result.FailureCode);
    }

    /// <summary>
    /// Tests that not-yet-valid token returns TokenNotYetValid failure.
    /// </summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Fact]
    public async Task AuthenticateAsync_NotYetValidToken_ReturnsTokenNotYetValid()
    {
        // Arrange
        var key = _keyProvider.SigningKeys[0];
        var token = TestTokenGenerator.GenerateNotYetValidToken(key);
        var credential = AuthCredential.Bearer(token);

        // Act
        var result = await _provider.AuthenticateAsync(credential);

        // Assert
        Assert.False(result.IsAuthenticated);
        Assert.Equal(AuthenticationFailureCode.TokenNotYetValid, result.FailureCode);
    }

    /// <summary>
    /// Tests that token with wrong issuer returns InvalidIssuer failure.
    /// </summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Fact]
    public async Task AuthenticateAsync_WrongIssuer_ReturnsInvalidIssuer()
    {
        // Arrange
        var key = _keyProvider.SigningKeys[0];
        var token = TestTokenGenerator.GenerateToken(key, issuer: "https://wrong-issuer.com");
        var credential = AuthCredential.Bearer(token);

        // Act
        var result = await _provider.AuthenticateAsync(credential);

        // Assert
        Assert.False(result.IsAuthenticated);
        Assert.Equal(AuthenticationFailureCode.InvalidIssuer, result.FailureCode);
    }

    /// <summary>
    /// Tests that token with wrong audience returns InvalidAudience failure.
    /// </summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Fact]
    public async Task AuthenticateAsync_WrongAudience_ReturnsInvalidAudience()
    {
        // Arrange
        var key = _keyProvider.SigningKeys[0];
        var token = TestTokenGenerator.GenerateToken(key, audience: "wrong-audience");
        var credential = AuthCredential.Bearer(token);

        // Act
        var result = await _provider.AuthenticateAsync(credential);

        // Assert
        Assert.False(result.IsAuthenticated);
        Assert.Equal(AuthenticationFailureCode.InvalidAudience, result.FailureCode);
    }

    /// <summary>
    /// Tests that token with invalid signature returns InvalidSignature failure.
    /// </summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Fact]
    public async Task AuthenticateAsync_InvalidSignature_ReturnsInvalidSignature()
    {
        // Arrange
        var wrongKey = TestTokenGenerator.GenerateKey("wrong-key");
        var token = TestTokenGenerator.GenerateToken(wrongKey);
        var credential = AuthCredential.Bearer(token);

        // Act
        var result = await _provider.AuthenticateAsync(credential);

        // Assert
        Assert.False(result.IsAuthenticated);
        Assert.Equal(AuthenticationFailureCode.InvalidSignature, result.FailureCode);
    }

    /// <summary>
    /// Tests that unsupported scheme returns UnsupportedScheme failure.
    /// </summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Fact]
    public async Task AuthenticateAsync_UnsupportedScheme_ReturnsUnsupportedScheme()
    {
        // Arrange
        var credential = new AuthCredential("Basic", "username:password");

        // Act
        var result = await _provider.AuthenticateAsync(credential);

        // Assert
        Assert.False(result.IsAuthenticated);
        Assert.Equal(AuthenticationFailureCode.UnsupportedScheme, result.FailureCode);
    }

    /// <summary>
    /// Tests that audit-log failures do not change the authentication outcome.
    /// </summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Fact]
    public async Task AuthenticateAsync_AuditLogThrows_ReturnsAuthenticationOutcome()
    {
        // Arrange
        _auditLog.AppendAsync(Arg.Any<AuditEntry>(), Arg.Any<CancellationToken>())
            .Returns(_ => throw new InvalidOperationException("audit down"));
        var key = _keyProvider.SigningKeys[0];
        var token = TestTokenGenerator.GenerateToken(key, subject: "user-123", name: "Test User");
        var credential = AuthCredential.Bearer(token);

        // Act
        var result = await _provider.AuthenticateAsync(credential);

        // Assert
        Assert.True(result.IsAuthenticated);
        Assert.Equal("user-123", result.Identity?.SubjectId);
    }

    /// <summary>
    /// Tests that token-validation audit metadata uses the allow-list and caps oversized context.
    /// </summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Fact]
    public async Task AuthenticateAsync_AuditMetadata_UsesAllowedClaimsAndCapsOversizedContext()
    {
        // Arrange
        AuditEntry? entry = null;
        _auditLog.AppendAsync(Arg.Do<AuditEntry>(e => entry = e), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        var longIssuer = "https://" + new string('i', 10_000) + ".example.com";
        _keyProvider.WithIssuer(longIssuer);
        var key = _keyProvider.SigningKeys[0];
        var claims = new[]
        {
            new Claim("secret-claim", "do-not-record"),
            new Claim("name", "Do Not Record"),
        };
        var token = TestTokenGenerator.GenerateToken(key, issuer: longIssuer, subject: "user-123", claims: claims);
        var credential = AuthCredential.Bearer(token);

        // Act
        var result = await _provider.AuthenticateAsync(credential);

        // Assert
        Assert.True(result.IsAuthenticated);
        Assert.NotNull(entry);
        Assert.Equal("true", entry.Metadata?["context.truncated"]);
        var context = entry.Metadata?["context"];
        Assert.NotNull(context);
        Assert.True(Encoding.UTF8.GetByteCount(context) <= 4096);
        Assert.DoesNotContain("�", context, StringComparison.Ordinal);
        Assert.EndsWith("...[truncated]", context, StringComparison.Ordinal);
        Assert.DoesNotContain(token, context, StringComparison.Ordinal);
        Assert.DoesNotContain("do-not-record", context, StringComparison.Ordinal);
        Assert.DoesNotContain("Do Not Record", context, StringComparison.Ordinal);
        Assert.DoesNotContain("user-123", context, StringComparison.Ordinal);
    }

    /// <summary>
    /// Tests that scopes are correctly extracted from token.
    /// </summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Fact]
    public async Task AuthenticateAsync_TokenWithScopes_ExtractsScopes()
    {
        // Arrange
        var key = _keyProvider.SigningKeys[0];
        var claims = new[] { new Claim("scope", "read write admin") };
        var token = TestTokenGenerator.GenerateToken(key, claims: claims);
        var credential = AuthCredential.Bearer(token);

        // Act
        var result = await _provider.AuthenticateAsync(credential);

        // Assert
        Assert.True(result.IsAuthenticated);
        Assert.NotNull(result.Identity);
        var scopes = result.Identity.GetClaimValues(AuthClaimTypes.Scope);
        Assert.Contains("read", scopes);
        Assert.Contains("write", scopes);
        Assert.Contains("admin", scopes);
    }

    /// <summary>
    /// Tests that roles are correctly extracted from token.
    /// </summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Fact]
    public async Task AuthenticateAsync_TokenWithRoles_ExtractsRoles()
    {
        // Arrange
        var key = _keyProvider.SigningKeys[0];
        var claims = new[]
        {
            new Claim("role", "admin"),
            new Claim("role", "user"),
        };
        var token = TestTokenGenerator.GenerateToken(key, claims: claims);
        var credential = AuthCredential.Bearer(token);

        // Act
        var result = await _provider.AuthenticateAsync(credential);

        // Assert
        Assert.True(result.IsAuthenticated);
        Assert.NotNull(result.Identity);
        var roles = result.Identity.GetClaimValues(AuthClaimTypes.Role);
        Assert.Contains("admin", roles);
        Assert.Contains("user", roles);
    }

    /// <summary>
    /// Tests that token signed with old key is validated during key rotation.
    /// </summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Fact]
    public async Task AuthenticateAsync_KeyRotation_ValidatesWithOldKey()
    {
        // Arrange - Add a new key but sign with the old one
        var oldKey = _keyProvider.SigningKeys[0];
        var newKey = TestTokenGenerator.GenerateKey("new-key");
        _keyProvider.AddKey(newKey.KeyId!, newKey.Key);

        var token = TestTokenGenerator.GenerateToken(oldKey);
        var credential = AuthCredential.Bearer(token);

        // Act
        var result = await _provider.AuthenticateAsync(credential);

        // Assert
        Assert.True(result.IsAuthenticated);
    }

    /// <summary>
    /// Tests that token signed with new key is validated during key rotation.
    /// </summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Fact]
    public async Task AuthenticateAsync_KeyRotation_ValidatesWithNewKey()
    {
        // Arrange - Add a new key and sign with it
        var newKey = TestTokenGenerator.GenerateKey("new-key");
        _keyProvider.AddKey(newKey.KeyId!, newKey.Key);

        var token = TestTokenGenerator.GenerateToken(newKey);
        var credential = AuthCredential.Bearer(token);

        // Act
        var result = await _provider.AuthenticateAsync(credential);

        // Assert
        Assert.True(result.IsAuthenticated);
    }

    /// <summary>
    /// Tests that an empty key set surfaces as ConfigurationError (covers the
    /// LoadValidationConfigurationAsync misconfiguration branch).
    /// </summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Fact]
    public async Task AuthenticateAsync_NoSigningKeys_ReturnsConfigurationError()
    {
        // Arrange - key provider has issuer/audience but no keys
        var emptyKeyProvider = new InMemorySigningKeyProvider();
        var provider = BuildProvider(emptyKeyProvider);

        // Act
        var result = await provider.AuthenticateAsync(AuthCredential.Bearer("any-token"));

        // Assert
        Assert.False(result.IsAuthenticated);
        Assert.Equal(AuthenticationFailureCode.ConfigurationError, result.FailureCode);
    }

    /// <summary>
    /// Tests that an empty issuer surfaces as ConfigurationError.
    /// </summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Fact]
    public async Task AuthenticateAsync_EmptyIssuer_ReturnsConfigurationError()
    {
        // Arrange
        var key = TestTokenGenerator.GenerateKey();
        var keyProvider = new InMemorySigningKeyProvider()
            .WithIssuer(string.Empty)
            .AddKey(key.KeyId!, key.Key);
        var provider = BuildProvider(keyProvider);

        // Act
        var result = await provider.AuthenticateAsync(AuthCredential.Bearer("any-token"));

        // Assert
        Assert.False(result.IsAuthenticated);
        Assert.Equal(AuthenticationFailureCode.ConfigurationError, result.FailureCode);
    }

    /// <summary>
    /// Tests that an empty audience surfaces as ConfigurationError.
    /// </summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Fact]
    public async Task AuthenticateAsync_EmptyAudience_ReturnsConfigurationError()
    {
        // Arrange
        var key = TestTokenGenerator.GenerateKey();
        var keyProvider = new InMemorySigningKeyProvider()
            .WithAudience(string.Empty)
            .AddKey(key.KeyId!, key.Key);
        var provider = BuildProvider(keyProvider);

        // Act
        var result = await provider.AuthenticateAsync(AuthCredential.Bearer("any-token"));

        // Assert
        Assert.False(result.IsAuthenticated);
        Assert.Equal(AuthenticationFailureCode.ConfigurationError, result.FailureCode);
    }

    /// <summary>
    /// Tests that a key-provider exception surfaces as VaultUnavailable (covers
    /// the LoadValidationConfigurationAsync catch path).
    /// </summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Fact]
    public async Task AuthenticateAsync_KeyProviderThrows_ReturnsVaultUnavailable()
    {
        // Arrange
        var keyProvider = Substitute.For<HoneyDrunk.Auth.Secrets.ISigningKeyProvider>();
        keyProvider.GetSigningKeysAsync(Arg.Any<CancellationToken>())
            .Returns<Task<IReadOnlyList<Microsoft.IdentityModel.Tokens.SecurityKey>>>(
                _ => throw new InvalidOperationException("Vault unreachable"));
        var provider = BuildProvider(keyProvider);

        // Act
        var result = await provider.AuthenticateAsync(AuthCredential.Bearer("any-token"));

        // Assert
        Assert.False(result.IsAuthenticated);
        Assert.Equal(AuthenticationFailureCode.VaultUnavailable, result.FailureCode);
    }

    private BearerTokenAuthenticationProvider BuildProvider(HoneyDrunk.Auth.Secrets.ISigningKeyProvider keyProvider)
    {
        var gridContext = Substitute.For<IGridContext>();
        gridContext.TenantId.Returns(TenantId.Internal);
        gridContext.CorrelationId.Returns("corr-test");
        var gridContextAccessor = Substitute.For<IGridContextAccessor>();
        gridContextAccessor.GridContext.Returns(gridContext);

        return new BearerTokenAuthenticationProvider(
            keyProvider,
            Options.Create(new AuthOptions()),
            _telemetryFactory,
            _auditLog,
            gridContextAccessor,
            NullLogger<BearerTokenAuthenticationProvider>.Instance);
    }
}
