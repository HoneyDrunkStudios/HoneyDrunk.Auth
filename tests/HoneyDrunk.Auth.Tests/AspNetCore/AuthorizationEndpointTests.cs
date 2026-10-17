using HoneyDrunk.Auth.Abstractions;
using HoneyDrunk.Auth.AspNetCore;
using HoneyDrunk.Auth.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace HoneyDrunk.Auth.Tests.AspNetCore;

/// <summary>Verifies endpoint authentication and authorization responses.</summary>
public sealed class AuthorizationEndpointTests
{
    /// <summary>Denied requests become 403 and policy evaluation receives the request token.</summary>
    /// <param name="allowed">The policy result.</param>
    /// <returns>The test task.</returns>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task AuthorizationPreservesPolicyDecisionAndCancellation(bool allowed)
    {
        var policy = Substitute.For<IAuthorizationPolicy>();
        var identity = Substitute.For<IAuthenticatedIdentityAccessor>();
        var request = new AuthorizationRequest("read", "resource");
        using var cancellation = new CancellationTokenSource();
        policy.EvaluateAsync(null, request, cancellation.Token).Returns(allowed ? AuthorizationDecision.Allow() : AuthorizationDecision.Deny([]));
        var services = new ServiceCollection();
        services.AddSingleton(policy);
        services.AddSingleton(identity);
        using var provider = services.BuildServiceProvider();
        var context = new DefaultHttpContext { RequestServices = provider };

        var result = await context.AuthorizeOrForbidAsync(request, cancellation.Token);

        Assert.Equal(allowed, result);
        Assert.Equal(allowed ? StatusCodes.Status200OK : StatusCodes.Status403Forbidden, context.Response.StatusCode);
        await policy.Received(1).EvaluateAsync(null, request, cancellation.Token);
    }

    /// <summary>Authentication gates return 401 only when no authenticated identity is available.</summary>
    /// <param name="authenticated">Whether an authenticated identity is available.</param>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void AuthenticationGateReturnsTheCorrectStatus(bool authenticated)
    {
        var identity = Substitute.For<IAuthenticatedIdentityAccessor>();
        identity.IsAuthenticated.Returns(authenticated);
        var services = new ServiceCollection();
        services.AddSingleton(identity);
        using var provider = services.BuildServiceProvider();
        var context = new DefaultHttpContext { RequestServices = provider };

        Assert.Equal(authenticated, context.RequireAuthentication());
        Assert.Equal(authenticated ? StatusCodes.Status200OK : StatusCodes.Status401Unauthorized, context.Response.StatusCode);
    }
}
