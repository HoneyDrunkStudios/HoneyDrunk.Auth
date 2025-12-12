namespace HoneyDrunk.Auth.Abstractions;

/// <summary>
/// Defines stable failure reason codes for authentication operations.
/// </summary>
/// <remarks>
/// These codes are designed to be versionable and suitable for API responses and audit logs.
/// </remarks>
public enum AuthenticationFailureCode
{
    /// <summary>
    /// No failure occurred (authentication succeeded).
    /// </summary>
    None = 0,

    /// <summary>
    /// The credential was not provided or was empty.
    /// </summary>
    MissingCredential = 1,

    /// <summary>
    /// The credential format is invalid or malformed.
    /// </summary>
    MalformedCredential = 2,

    /// <summary>
    /// The authentication scheme is not supported.
    /// </summary>
    UnsupportedScheme = 3,

    /// <summary>
    /// The token signature is invalid.
    /// </summary>
    InvalidSignature = 4,

    /// <summary>
    /// The token has expired.
    /// </summary>
    TokenExpired = 5,

    /// <summary>
    /// The token is not yet valid (not-before claim).
    /// </summary>
    TokenNotYetValid = 6,

    /// <summary>
    /// The token issuer is not trusted.
    /// </summary>
    InvalidIssuer = 7,

    /// <summary>
    /// The token audience does not match expected value.
    /// </summary>
    InvalidAudience = 8,

    /// <summary>
    /// A required claim is missing from the token.
    /// </summary>
    MissingClaim = 9,

    /// <summary>
    /// An internal error occurred during authentication.
    /// </summary>
    InternalError = 99,
}
