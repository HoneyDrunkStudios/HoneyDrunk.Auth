using HoneyDrunk.Auth.Abstractions;
using HoneyDrunk.Auth.AspNetCore;
using HoneyDrunk.Auth.AspNetCore.Middleware;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Net.Http.Headers;
using NSubstitute;

namespace HoneyDrunk.Auth.Tests;

/// <summary>
/// Tests for <see cref="HoneyDrunkAuthMiddleware"/>.
/// </summary>
public sealed class HoneyDrunkAuthMiddlewareTests
{
    /// <summary>
    /// Tests that request without Authorization header continues without authentication.
    /// </summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Fact]
    public async Task InvokeAsync_NoAuthorizationHeader_ContinuesWithoutAuthentication()
    {
        // Arrange
        var context = new DefaultHttpContext();
        var nextCalled = false;
        var middleware = CreateMiddleware(() => nextCalled = true);

        var authProvider = Substitute.For<IAuthenticationProvider>();

        // Act
        await middleware.InvokeAsync(context, authProvider);

        // Assert
        Assert.True(nextCalled);
        Assert.False(context.Items.ContainsKey(HttpContextIdentityAccessor.IdentityKey));
        await authProvider.DidNotReceive().AuthenticateAsync(Arg.Any<AuthCredential>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// Tests that request with non-Bearer scheme continues without authentication.
    /// </summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Fact]
    public async Task InvokeAsync_NonBearerScheme_ContinuesWithoutAuthentication()
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Request.Headers[HeaderNames.Authorization] = "Basic dXNlcjpwYXNz";

        var nextCalled = false;
        var middleware = CreateMiddleware(() => nextCalled = true);

        var authProvider = Substitute.For<IAuthenticationProvider>();

        // Act
        await middleware.InvokeAsync(context, authProvider);

        // Assert
        Assert.True(nextCalled);
        Assert.False(context.Items.ContainsKey(HttpContextIdentityAccessor.IdentityKey));
    }

    /// <summary>
    /// Tests that valid Bearer token sets identity in HttpContext.Items.
    /// </summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Fact]
    public async Task InvokeAsync_ValidBearerToken_SetsIdentityInContext()
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Request.Headers[HeaderNames.Authorization] = "Bearer valid-token";

        var nextCalled = false;
        var middleware = CreateMiddleware(() => nextCalled = true);

        var expectedIdentity = new AuthenticatedIdentity("user-123", AuthScheme.Bearer, "Test User");
        var authProvider = Substitute.For<IAuthenticationProvider>();
        authProvider.AuthenticateAsync(Arg.Any<AuthCredential>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(AuthenticationResult.Success(expectedIdentity)));

        // Act
        await middleware.InvokeAsync(context, authProvider);

        // Assert
        Assert.True(nextCalled);
        Assert.True(context.Items.ContainsKey(HttpContextIdentityAccessor.IdentityKey));
        var identity = context.Items[HttpContextIdentityAccessor.IdentityKey] as AuthenticatedIdentity;
        Assert.NotNull(identity);
        Assert.Equal("user-123", identity.SubjectId);
    }

    /// <summary>
    /// Tests that valid Bearer token sets HttpContext.User.
    /// </summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Fact]
    public async Task InvokeAsync_ValidBearerToken_SetsHttpContextUser()
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Request.Headers[HeaderNames.Authorization] = "Bearer valid-token";

        var nextCalled = false;
        var middleware = CreateMiddleware(() => nextCalled = true);

        var expectedIdentity = new AuthenticatedIdentity("user-123", AuthScheme.Bearer, "Test User");
        var authProvider = Substitute.For<IAuthenticationProvider>();
        authProvider.AuthenticateAsync(Arg.Any<AuthCredential>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(AuthenticationResult.Success(expectedIdentity)));

        // Act
        await middleware.InvokeAsync(context, authProvider);

        // Assert
        Assert.True(nextCalled);
        Assert.True(context.User.Identity?.IsAuthenticated);
        Assert.Equal("user-123", context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value);
    }

    /// <summary>
    /// Tests that invalid Bearer token continues without setting identity.
    /// </summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Fact]
    public async Task InvokeAsync_InvalidBearerToken_ContinuesWithoutIdentity()
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Request.Headers[HeaderNames.Authorization] = "Bearer invalid-token";

        var nextCalled = false;
        var middleware = CreateMiddleware(() => nextCalled = true);

        var authProvider = Substitute.For<IAuthenticationProvider>();
        authProvider.AuthenticateAsync(Arg.Any<AuthCredential>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(AuthenticationResult.Fail(AuthenticationFailureCode.InvalidSignature, "Bad signature")));

        // Act
        await middleware.InvokeAsync(context, authProvider);

        // Assert
        Assert.True(nextCalled);
        Assert.False(context.Items.ContainsKey(HttpContextIdentityAccessor.IdentityKey));
        Assert.False(context.User.Identity?.IsAuthenticated);
    }

    /// <summary>
    /// Tests that empty Bearer token continues without authentication.
    /// </summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Fact]
    public async Task InvokeAsync_EmptyBearerToken_ContinuesWithoutAuthentication()
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Request.Headers[HeaderNames.Authorization] = "Bearer ";

        var nextCalled = false;
        var middleware = CreateMiddleware(() => nextCalled = true);

        var authProvider = Substitute.For<IAuthenticationProvider>();

        // Act
        await middleware.InvokeAsync(context, authProvider);

        // Assert
        Assert.True(nextCalled);
        await authProvider.DidNotReceive().AuthenticateAsync(Arg.Any<AuthCredential>(), Arg.Any<CancellationToken>());
    }

    private static HoneyDrunkAuthMiddleware CreateMiddleware(Action onNext)
    {
        Task Next(HttpContext httpContext)
        {
            onNext();
            return Task.CompletedTask;
        }

        return new HoneyDrunkAuthMiddleware(Next, NullLogger<HoneyDrunkAuthMiddleware>.Instance);
    }
}
