using HoneyDrunk.Auth.Abstractions;
using HoneyDrunk.Auth.Authentication;
using HoneyDrunk.Auth.Tests.Helpers;
using HoneyDrunk.Kernel.Abstractions.Telemetry;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using System.Security.Claims;

namespace HoneyDrunk.Auth.Tests;

/// <summary>
/// Tests for <see cref="BearerTokenAuthenticationProvider"/>.
/// </summary>
public sealed class BearerTokenAuthenticationProviderTests
{
    private readonly InMemorySigningKeyProvider _keyProvider;
    private readonly ITelemetryActivityFactory _telemetryFactory;
    private readonly BearerTokenAuthenticationProvider _provider;

    /// <summary>
    /// Initializes a new instance of the <see cref="BearerTokenAuthenticationProviderTests"/> class.
    /// </summary>
    public BearerTokenAuthenticationProviderTests()
    {
        var key = TestTokenGenerator.GenerateKey();
        _keyProvider = new InMemorySigningKeyProvider()
            .AddKey(key.KeyId!, ((Microsoft.IdentityModel.Tokens.SymmetricSecurityKey)key).Key);

        _telemetryFactory = Substitute.For<ITelemetryActivityFactory>();

        _provider = new BearerTokenAuthenticationProvider(
            _keyProvider,
            _telemetryFactory,
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
        var token = TestTokenGenerator.GenerateToken(key, subject: "user-123", name: "Test User");
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
}
