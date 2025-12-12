using HoneyDrunk.Auth.Abstractions;
using Microsoft.AspNetCore.Http;

namespace HoneyDrunk.Auth.AspNetCore;

/// <summary>
/// Provides access to the authenticated identity via HttpContext.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="HttpContextIdentityAccessor"/> class.
/// </remarks>
/// <param name="httpContextAccessor">The HTTP context accessor.</param>
public sealed class HttpContextIdentityAccessor(IHttpContextAccessor httpContextAccessor) : IAuthenticatedIdentityAccessor
{
    /// <summary>
    /// The key used to store the authenticated identity in HttpContext.Items.
    /// </summary>
    public const string IdentityKey = "HoneyDrunk.Auth.Identity";

    private readonly IHttpContextAccessor _httpContextAccessor = httpContextAccessor ?? throw new ArgumentNullException(nameof(httpContextAccessor));

    /// <inheritdoc />
    public AuthenticatedIdentity? Identity
    {
        get
        {
            var httpContext = _httpContextAccessor.HttpContext;
            if (httpContext is null)
            {
                return null;
            }

            return httpContext.Items.TryGetValue(IdentityKey, out var identity)
                ? identity as AuthenticatedIdentity
                : null;
        }
    }

    /// <inheritdoc />
    public bool IsAuthenticated => Identity is not null;
}
