namespace HoneyDrunk.Auth.Abstractions;

/// <summary>
/// Represents the result of an authentication operation.
/// </summary>
public sealed class AuthenticationResult
{
    private AuthenticationResult(
        bool isAuthenticated,
        AuthenticatedIdentity? identity,
        AuthenticationFailureCode failureCode,
        string? failureMessage)
    {
        IsAuthenticated = isAuthenticated;
        Identity = identity;
        FailureCode = failureCode;
        FailureMessage = failureMessage;
    }

    /// <summary>
    /// Gets a value indicating whether authentication succeeded.
    /// </summary>
    public bool IsAuthenticated { get; }

    /// <summary>
    /// Gets the authenticated identity when successful.
    /// </summary>
    public AuthenticatedIdentity? Identity { get; }

    /// <summary>
    /// Gets the failure code when authentication failed.
    /// </summary>
    public AuthenticationFailureCode FailureCode { get; }

    /// <summary>
    /// Gets the failure message when authentication failed.
    /// </summary>
    public string? FailureMessage { get; }

    /// <summary>
    /// Creates a successful authentication result.
    /// </summary>
    /// <param name="identity">The authenticated identity.</param>
    /// <returns>A successful authentication result.</returns>
    /// <exception cref="ArgumentNullException">Thrown when identity is null.</exception>
    public static AuthenticationResult Success(AuthenticatedIdentity identity)
    {
        ArgumentNullException.ThrowIfNull(identity);
        return new AuthenticationResult(true, identity, AuthenticationFailureCode.None, null);
    }

    /// <summary>
    /// Creates a failed authentication result.
    /// </summary>
    /// <param name="code">The failure code.</param>
    /// <param name="message">The failure message.</param>
    /// <returns>A failed authentication result.</returns>
    public static AuthenticationResult Fail(AuthenticationFailureCode code, string? message = null)
    {
        return new AuthenticationResult(false, null, code, message);
    }
}
