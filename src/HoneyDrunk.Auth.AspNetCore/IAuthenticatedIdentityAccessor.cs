using HoneyDrunk.Auth.Abstractions;

namespace HoneyDrunk.Auth.AspNetCore;

/// <summary>
/// Provides access to the current authenticated identity.
/// </summary>
public interface IAuthenticatedIdentityAccessor
{
    /// <summary>
    /// Gets the current authenticated identity, or null if not authenticated.
    /// </summary>
    AuthenticatedIdentity? Identity { get; }

    /// <summary>
    /// Gets a value indicating whether the current request is authenticated.
    /// </summary>
    bool IsAuthenticated { get; }
}
