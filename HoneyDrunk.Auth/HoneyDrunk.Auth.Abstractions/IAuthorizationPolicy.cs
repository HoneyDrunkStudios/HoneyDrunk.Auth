namespace HoneyDrunk.Auth.Abstractions;

/// <summary>
/// Defines the contract for authorization policy evaluation.
/// </summary>
/// <remarks>
/// Implementations evaluate authorization requests against authenticated identities.
/// </remarks>
public interface IAuthorizationPolicy
{
    /// <summary>
    /// Gets the policy name for audit logging.
    /// </summary>
    string PolicyName { get; }

    /// <summary>
    /// Evaluates an authorization request against an authenticated identity.
    /// </summary>
    /// <param name="identity">The authenticated identity (or null if not authenticated).</param>
    /// <param name="request">The authorization request.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The authorization decision.</returns>
    Task<AuthorizationDecision> EvaluateAsync(
        AuthenticatedIdentity? identity,
        AuthorizationRequest request,
        CancellationToken cancellationToken = default);
}
