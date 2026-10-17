using HoneyDrunk.Auth.Abstractions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace HoneyDrunk.Auth.AspNetCore.Authorization;

/// <summary>
/// Extension methods for authorization in endpoint handlers.
/// </summary>
public static class AuthorizationEndpointExtensions
{
    /// <summary>
    /// Authorizes the current request against the specified authorization request.
    /// </summary>
    /// <param name="context">The HTTP context.</param>
    /// <param name="request">The authorization request.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The authorization decision.</returns>
    public static async Task<AuthorizationDecision> AuthorizeAsync(
        this HttpContext context,
        AuthorizationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(request);

        var policy = context.RequestServices.GetRequiredService<IAuthorizationPolicy>();
        var identityAccessor = context.RequestServices.GetRequiredService<IAuthenticatedIdentityAccessor>();

        return await policy.EvaluateAsync(identityAccessor.Identity, request, cancellationToken);
    }

    /// <summary>
    /// Authorizes the current request and returns a 403 Forbidden response if denied.
    /// </summary>
    /// <param name="context">The HTTP context.</param>
    /// <param name="request">The authorization request.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>True if allowed; false if denied (response is written).</returns>
    public static async Task<bool> AuthorizeOrForbidAsync(
        this HttpContext context,
        AuthorizationRequest request,
        CancellationToken cancellationToken = default)
    {
        var decision = await context.AuthorizeAsync(request, cancellationToken);

        if (decision.IsAllowed)
        {
            return true;
        }

        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        return false;
    }

    /// <summary>
    /// Ensures the current request is authenticated, returning 401 if not.
    /// </summary>
    /// <param name="context">The HTTP context.</param>
    /// <returns>True if authenticated; false if not (response is written).</returns>
    public static bool RequireAuthentication(this HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var identityAccessor = context.RequestServices.GetRequiredService<IAuthenticatedIdentityAccessor>();

        if (identityAccessor.IsAuthenticated)
        {
            return true;
        }

        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        return false;
    }
}
