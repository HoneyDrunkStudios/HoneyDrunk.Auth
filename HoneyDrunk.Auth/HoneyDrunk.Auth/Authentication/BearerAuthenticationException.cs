using HoneyDrunk.Auth.Abstractions;

namespace HoneyDrunk.Auth.Authentication;

/// <summary>
/// Exception used by <see cref="BearerTokenAuthenticationProvider"/> to propagate
/// authentication failures along with the originating <see cref="AuthenticationFailureCode"/>.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="BearerAuthenticationException"/> class.
/// </remarks>
/// <param name="failureCode">The failure code that caused the exception.</param>
/// <param name="message">A human-readable failure message.</param>
public sealed class BearerAuthenticationException(AuthenticationFailureCode failureCode, string message) : Exception(message)
{
    /// <summary>
    /// Gets the failure code that caused the exception.
    /// </summary>
    public AuthenticationFailureCode FailureCode { get; } = failureCode;
}
