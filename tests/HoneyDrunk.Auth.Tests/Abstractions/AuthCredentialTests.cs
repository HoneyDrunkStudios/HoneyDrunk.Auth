using HoneyDrunk.Auth.Abstractions;

namespace HoneyDrunk.Auth.Tests.Abstractions;

/// <summary>
/// Tests for <see cref="AuthCredential"/>.
/// </summary>
public sealed class AuthCredentialTests
{
    /// <summary>
    /// Tests that constructor sets properties correctly.
    /// </summary>
    [Fact]
    public void Constructor_ValidParameters_SetsProperties()
    {
        // Act
        var credential = new AuthCredential("Bearer", "test-token");

        // Assert
        Assert.Equal("Bearer", credential.Scheme);
        Assert.Equal("test-token", credential.Value);
    }

    /// <summary>
    /// Tests that constructor throws when scheme is null.
    /// </summary>
    [Fact]
    public void Constructor_NullScheme_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new AuthCredential(null!, "test-token"));
    }

    /// <summary>
    /// Tests that constructor throws when scheme is empty.
    /// </summary>
    [Fact]
    public void Constructor_EmptyScheme_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => new AuthCredential(string.Empty, "test-token"));
    }

    /// <summary>
    /// Tests that constructor throws when scheme is whitespace.
    /// </summary>
    [Fact]
    public void Constructor_WhitespaceScheme_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => new AuthCredential("   ", "test-token"));
    }

    /// <summary>
    /// Tests that constructor throws when value is null.
    /// </summary>
    [Fact]
    public void Constructor_NullValue_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new AuthCredential("Bearer", null!));
    }

    /// <summary>
    /// Tests that constructor throws when value is empty.
    /// </summary>
    [Fact]
    public void Constructor_EmptyValue_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => new AuthCredential("Bearer", string.Empty));
    }

    /// <summary>
    /// Tests that constructor throws when value is whitespace.
    /// </summary>
    [Fact]
    public void Constructor_WhitespaceValue_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => new AuthCredential("Bearer", "   "));
    }

    /// <summary>
    /// Tests that Bearer factory creates credential with Bearer scheme.
    /// </summary>
    [Fact]
    public void Bearer_ValidToken_CreatesBearerCredential()
    {
        // Act
        var credential = AuthCredential.Bearer("my-jwt-token");

        // Assert
        Assert.Equal(AuthScheme.Bearer, credential.Scheme);
        Assert.Equal("my-jwt-token", credential.Value);
    }

    /// <summary>
    /// Tests that Bearer factory throws when token is null.
    /// </summary>
    [Fact]
    public void Bearer_NullToken_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => AuthCredential.Bearer(null!));
    }

    /// <summary>
    /// Tests that Bearer factory throws when token is empty.
    /// </summary>
    [Fact]
    public void Bearer_EmptyToken_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => AuthCredential.Bearer(string.Empty));
    }
}
