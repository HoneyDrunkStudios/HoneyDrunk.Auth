namespace HoneyDrunk.Auth.Abstractions;

/// <summary>
/// Defines stable deny reason codes for authorization operations.
/// </summary>
/// <remarks>
/// These codes are designed to be versionable and suitable for API responses and audit logs.
/// </remarks>
public enum AuthorizationDenyCode
{
    /// <summary>
    /// No denial occurred (authorization succeeded).
    /// </summary>
    None = 0,

    /// <summary>
    /// The request was not authenticated.
    /// </summary>
    NotAuthenticated = 1,

    /// <summary>
    /// A required scope is missing from the identity.
    /// </summary>
    MissingScope = 2,

    /// <summary>
    /// A required role is missing from the identity.
    /// </summary>
    MissingRole = 3,

    /// <summary>
    /// The identity does not own the requested resource.
    /// </summary>
    ResourceOwnershipDenied = 4,

    /// <summary>
    /// The requested action is not permitted.
    /// </summary>
    ActionNotPermitted = 5,

    /// <summary>
    /// The resource is not accessible to the identity.
    /// </summary>
    ResourceNotAccessible = 6,

    /// <summary>
    /// A custom policy requirement was not satisfied.
    /// </summary>
    PolicyNotSatisfied = 7,

    /// <summary>
    /// An internal error occurred during authorization.
    /// </summary>
    InternalError = 99,
}
