using HoneyDrunk.Auth.Abstractions;

namespace HoneyDrunk.Auth.Tests.Abstractions;

/// <summary>
/// Tests for <see cref="AuthenticationResult"/>.
/// </summary>
public sealed class AuthenticationResultTests
{
    /// <summary>
    /// Tests that Success creates an authenticated result with identity.
    /// </summary>
    [Fact]
    public void Success_ValidIdentity_CreatesAuthenticatedResult()
    {
        // Arrange
        var identity = new AuthenticatedIdentity("user-123", "Bearer", "Test User");

        // Act
        var result = AuthenticationResult.Success(identity);

        // Assert
        Assert.True(result.IsAuthenticated);
        Assert.NotNull(result.Identity);
        Assert.Equal("user-123", result.Identity.SubjectId);
        Assert.Equal(AuthenticationFailureCode.None, result.FailureCode);
        Assert.Null(result.FailureMessage);
    }

    /// <summary>
    /// Tests that Success throws when identity is null.
    /// </summary>
    [Fact]
    public void Success_NullIdentity_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => AuthenticationResult.Success(null!));
    }

    /// <summary>
    /// Tests that Fail creates a failed result with code and message.
    /// </summary>
    [Fact]
    public void Fail_WithCodeAndMessage_CreatesFailedResult()
    {
        // Act
        var result = AuthenticationResult.Fail(AuthenticationFailureCode.TokenExpired, "Token has expired");

        // Assert
        Assert.False(result.IsAuthenticated);
        Assert.Null(result.Identity);
        Assert.Equal(AuthenticationFailureCode.TokenExpired, result.FailureCode);
        Assert.Equal("Token has expired", result.FailureMessage);
    }

    /// <summary>
    /// Tests that Fail creates a failed result with code only.
    /// </summary>
    [Fact]
    public void Fail_WithCodeOnly_CreatesFailedResultWithNullMessage()
    {
        // Act
        var result = AuthenticationResult.Fail(AuthenticationFailureCode.InvalidSignature);

        // Assert
        Assert.False(result.IsAuthenticated);
        Assert.Null(result.Identity);
        Assert.Equal(AuthenticationFailureCode.InvalidSignature, result.FailureCode);
        Assert.Null(result.FailureMessage);
    }

    /// <summary>
    /// Tests all failure codes can be used.
    /// </summary>
    /// <param name="code">The failure code to test.</param>
    [Theory]
    [InlineData(AuthenticationFailureCode.None)]
    [InlineData(AuthenticationFailureCode.InvalidSignature)]
    [InlineData(AuthenticationFailureCode.TokenExpired)]
    [InlineData(AuthenticationFailureCode.TokenNotYetValid)]
    [InlineData(AuthenticationFailureCode.InvalidIssuer)]
    [InlineData(AuthenticationFailureCode.InvalidAudience)]
    [InlineData(AuthenticationFailureCode.MalformedCredential)]
    [InlineData(AuthenticationFailureCode.UnsupportedScheme)]
    [InlineData(AuthenticationFailureCode.InternalError)]
    public void Fail_AllFailureCodes_CreatesResultWithCorrectCode(AuthenticationFailureCode code)
    {
        // Act
        var result = AuthenticationResult.Fail(code, "Test message");

        // Assert
        Assert.Equal(code, result.FailureCode);
    }
}
