using HoneyDrunk.Auth.Abstractions;
using HoneyDrunk.Auth.AspNetCore;
using Microsoft.AspNetCore.Http;
using NSubstitute;

namespace HoneyDrunk.Auth.Tests.AspNetCore;

/// <summary>
/// Tests for <see cref="HttpContextIdentityAccessor"/>.
/// </summary>
public sealed class HttpContextIdentityAccessorTests
{
    /// <summary>
    /// Tests that Identity returns identity when stored in HttpContext.Items.
    /// </summary>
    [Fact]
    public void Identity_IdentityInHttpContext_ReturnsIdentity()
    {
        // Arrange
        var httpContext = new DefaultHttpContext();
        var expectedIdentity = new AuthenticatedIdentity("user-123", "Bearer", "Test User");
        httpContext.Items[HttpContextIdentityAccessor.IdentityKey] = expectedIdentity;

        var httpContextAccessor = Substitute.For<IHttpContextAccessor>();
        httpContextAccessor.HttpContext.Returns(httpContext);

        var accessor = new HttpContextIdentityAccessor(httpContextAccessor);

        // Act
        var identity = accessor.Identity;

        // Assert
        Assert.NotNull(identity);
        Assert.Equal("user-123", identity.SubjectId);
        Assert.Equal("Test User", identity.DisplayName);
    }

    /// <summary>
    /// Tests that Identity returns null when HttpContext is null.
    /// </summary>
    [Fact]
    public void Identity_NullHttpContext_ReturnsNull()
    {
        // Arrange
        var httpContextAccessor = Substitute.For<IHttpContextAccessor>();
        httpContextAccessor.HttpContext.Returns((HttpContext?)null);

        var accessor = new HttpContextIdentityAccessor(httpContextAccessor);

        // Act
        var identity = accessor.Identity;

        // Assert
        Assert.Null(identity);
    }

    /// <summary>
    /// Tests that Identity returns null when identity key is not in Items.
    /// </summary>
    [Fact]
    public void Identity_IdentityKeyNotInItems_ReturnsNull()
    {
        // Arrange
        var httpContext = new DefaultHttpContext();
        var httpContextAccessor = Substitute.For<IHttpContextAccessor>();
        httpContextAccessor.HttpContext.Returns(httpContext);

        var accessor = new HttpContextIdentityAccessor(httpContextAccessor);

        // Act
        var identity = accessor.Identity;

        // Assert
        Assert.Null(identity);
    }

    /// <summary>
    /// Tests that Identity returns null when item is not an AuthenticatedIdentity.
    /// </summary>
    [Fact]
    public void Identity_WrongTypeInItems_ReturnsNull()
    {
        // Arrange
        var httpContext = new DefaultHttpContext();
        httpContext.Items[HttpContextIdentityAccessor.IdentityKey] = "not an identity";

        var httpContextAccessor = Substitute.For<IHttpContextAccessor>();
        httpContextAccessor.HttpContext.Returns(httpContext);

        var accessor = new HttpContextIdentityAccessor(httpContextAccessor);

        // Act
        var identity = accessor.Identity;

        // Assert
        Assert.Null(identity);
    }

    /// <summary>
    /// Tests that IsAuthenticated returns true when identity is present.
    /// </summary>
    [Fact]
    public void IsAuthenticated_IdentityPresent_ReturnsTrue()
    {
        // Arrange
        var httpContext = new DefaultHttpContext();
        httpContext.Items[HttpContextIdentityAccessor.IdentityKey] = new AuthenticatedIdentity("user-123", "Bearer");

        var httpContextAccessor = Substitute.For<IHttpContextAccessor>();
        httpContextAccessor.HttpContext.Returns(httpContext);

        var accessor = new HttpContextIdentityAccessor(httpContextAccessor);

        // Act
        var isAuthenticated = accessor.IsAuthenticated;

        // Assert
        Assert.True(isAuthenticated);
    }

    /// <summary>
    /// Tests that IsAuthenticated returns false when identity is not present.
    /// </summary>
    [Fact]
    public void IsAuthenticated_IdentityNotPresent_ReturnsFalse()
    {
        // Arrange
        var httpContext = new DefaultHttpContext();
        var httpContextAccessor = Substitute.For<IHttpContextAccessor>();
        httpContextAccessor.HttpContext.Returns(httpContext);

        var accessor = new HttpContextIdentityAccessor(httpContextAccessor);

        // Act
        var isAuthenticated = accessor.IsAuthenticated;

        // Assert
        Assert.False(isAuthenticated);
    }

    /// <summary>
    /// Tests that IsAuthenticated returns false when HttpContext is null.
    /// </summary>
    [Fact]
    public void IsAuthenticated_NullHttpContext_ReturnsFalse()
    {
        // Arrange
        var httpContextAccessor = Substitute.For<IHttpContextAccessor>();
        httpContextAccessor.HttpContext.Returns((HttpContext?)null);

        var accessor = new HttpContextIdentityAccessor(httpContextAccessor);

        // Act
        var isAuthenticated = accessor.IsAuthenticated;

        // Assert
        Assert.False(isAuthenticated);
    }

    /// <summary>
    /// Tests that IdentityKey constant has expected value.
    /// </summary>
    [Fact]
    public void IdentityKey_HasExpectedValue()
    {
        Assert.Equal("HoneyDrunk.Auth.Identity", HttpContextIdentityAccessor.IdentityKey);
    }

    /// <summary>
    /// Tests that constructor throws when httpContextAccessor is null.
    /// </summary>
    [Fact]
    public void Constructor_NullHttpContextAccessor_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new HttpContextIdentityAccessor(null!));
    }
}
