namespace HoneyDrunk.Auth.Abstractions;

/// <summary>
/// Defines the contract for authentication providers.
/// </summary>
/// <remarks>
/// Implementations validate credentials and produce authenticated identities.
/// </remarks>
public interface IAuthenticationProvider
{
    /// <summary>
    /// Gets the authentication schemes supported by this provider.
    /// </summary>
    IReadOnlyList<string> SupportedSchemes { get; }

    /// <summary>
    /// Authenticates a credential and returns the result.
    /// </summary>
    /// <param name="credential">The credential to authenticate.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The authentication result.</returns>
    Task<AuthenticationResult> AuthenticateAsync(AuthCredential credential, CancellationToken cancellationToken = default);
}
