using HoneyDrunk.Auth.Abstractions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Net.Http.Headers;
using System.Security.Claims;

namespace HoneyDrunk.Auth.AspNetCore.Middleware;

/// <summary>
/// Middleware that authenticates requests using Bearer tokens.
/// </summary>
/// <remarks>
/// Reads the Authorization header, validates the token, and sets the authenticated identity
/// in HttpContext.Items for downstream access.
/// </remarks>
/// <remarks>
/// Initializes a new instance of the <see cref="HoneyDrunkAuthMiddleware"/> class.
/// </remarks>
/// <param name="next">The next request delegate.</param>
/// <param name="logger">The logger.</param>
public sealed class HoneyDrunkAuthMiddleware(RequestDelegate next, ILogger<HoneyDrunkAuthMiddleware> logger)
{
    private const string BearerPrefix = "Bearer ";

    private readonly RequestDelegate _next = next ?? throw new ArgumentNullException(nameof(next));
    private readonly ILogger<HoneyDrunkAuthMiddleware> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

    /// <summary>
    /// Invokes the middleware to authenticate the request.
    /// </summary>
    /// <param name="context">The HTTP context.</param>
    /// <param name="authProvider">The authentication provider.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task InvokeAsync(
        HttpContext context,
        IAuthenticationProvider authProvider)
    {
        var authorizationHeader = context.Request.Headers[HeaderNames.Authorization].ToString();

        if (string.IsNullOrEmpty(authorizationHeader) ||
            !authorizationHeader.StartsWith(BearerPrefix, StringComparison.OrdinalIgnoreCase))
        {
            // No token provided - continue without authentication
            await _next(context);
            return;
        }

        var token = authorizationHeader[BearerPrefix.Length..].Trim();

        if (string.IsNullOrEmpty(token))
        {
            // Empty token - continue without authentication
            await _next(context);
            return;
        }

        var credential = AuthCredential.Bearer(token);
        var result = await authProvider.AuthenticateAsync(credential, context.RequestAborted);

        if (result.IsAuthenticated && result.Identity is not null)
        {
            // Store identity in HttpContext.Items for IAuthenticatedIdentityAccessor
            context.Items[HttpContextIdentityAccessor.IdentityKey] = result.Identity;

            // Also set HttpContext.User for compatibility with ASP.NET Core authorization
            context.User = CreateClaimsPrincipal(result.Identity);

            _logger.LogDebug(
                "Request authenticated: subject={SubjectId}, scheme={Scheme}",
                result.Identity.SubjectId,
                result.Identity.Scheme);
        }
        else
        {
            _logger.LogDebug(
                "Request authentication failed: code={FailureCode}, message={Message}",
                result.FailureCode,
                result.FailureMessage);
        }

        await _next(context);
    }

    private static ClaimsPrincipal CreateClaimsPrincipal(AuthenticatedIdentity identity)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, identity.SubjectId),
        };

        if (!string.IsNullOrEmpty(identity.DisplayName))
        {
            claims.Add(new Claim(ClaimTypes.Name, identity.DisplayName));
        }

        foreach (var claim in identity.Claims)
        {
            foreach (var value in claim.Value)
            {
                // Map standard claim types
                var claimType = claim.Key switch
                {
                    AuthClaimTypes.Email => ClaimTypes.Email,
                    AuthClaimTypes.Role => ClaimTypes.Role,
                    _ => claim.Key,
                };

                claims.Add(new Claim(claimType, value));
            }
        }

        var claimsIdentity = new ClaimsIdentity(claims, identity.Scheme);
        return new ClaimsPrincipal(claimsIdentity);
    }
}
