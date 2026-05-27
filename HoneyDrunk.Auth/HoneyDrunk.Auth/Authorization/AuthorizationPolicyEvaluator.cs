using HoneyDrunk.Auth.Abstractions;

namespace HoneyDrunk.Auth.Authorization;

/// <summary>
/// Pure, side-effect-free authorization policy evaluator.
/// </summary>
/// <remarks>
/// This class contains the core authorization logic with no I/O, logging, or telemetry.
/// It is designed to be wrapped by decorators that add observability concerns.
/// </remarks>
public static class AuthorizationPolicyEvaluator
{
    /// <summary>
    /// Evaluates an authorization request against an authenticated identity.
    /// This method is pure and deterministic: same inputs always produce same outputs.
    /// </summary>
    /// <param name="identity">The authenticated identity (or null if not authenticated).</param>
    /// <param name="request">The authorization request.</param>
    /// <returns>The authorization decision.</returns>
    public static AuthorizationDecision Evaluate(AuthenticatedIdentity? identity, AuthorizationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var denyReasons = new List<DenyReason>();
        var satisfiedRequirements = new List<string>();

        // Check authentication
        if (identity is null)
        {
            return AuthorizationDecision.Deny(AuthorizationDenyCode.NotAuthenticated, "Request is not authenticated");
        }

        satisfiedRequirements.Add("authenticated");

        // Check required scopes (ALL required)
        foreach (var requiredScope in request.RequiredScopes)
        {
            if (identity.HasClaim(AuthClaimTypes.Scope, requiredScope))
            {
                satisfiedRequirements.Add($"scope:{requiredScope}");
            }
            else
            {
                denyReasons.Add(new DenyReason(
                    AuthorizationDenyCode.MissingScope,
                    $"Required scope '{requiredScope}' is missing"));
            }
        }

        // Check required roles (ANY role is sufficient)
        if (request.RequiredRoles.Count > 0)
        {
            var matchedRole = request.RequiredRoles.FirstOrDefault(
                role => identity.HasClaim(AuthClaimTypes.Role, role));

            if (matchedRole is not null)
            {
                satisfiedRequirements.Add($"role:{matchedRole}");
            }
            else
            {
                denyReasons.Add(new DenyReason(
                    AuthorizationDenyCode.MissingRole,
                    $"None of the required roles [{string.Join(", ", request.RequiredRoles)}] are present"));
            }
        }

        // Check resource ownership
        if (!string.IsNullOrEmpty(request.ResourceOwnerId))
        {
            if (string.Equals(identity.SubjectId, request.ResourceOwnerId, StringComparison.Ordinal))
            {
                satisfiedRequirements.Add("owner");
            }
            else
            {
                denyReasons.Add(new DenyReason(
                    AuthorizationDenyCode.ResourceOwnershipDenied,
                    "Identity does not own the requested resource"));
            }
        }

        // Return decision
        if (denyReasons.Count > 0)
        {
            return AuthorizationDecision.Deny(denyReasons, satisfiedRequirements);
        }

        return AuthorizationDecision.Allow(satisfiedRequirements);
    }
}
