using HoneyDrunk.Auth.Abstractions;
using HoneyDrunk.Auth.Telemetry;
using HoneyDrunk.Kernel.Abstractions.Telemetry;
using Microsoft.Extensions.Logging;

namespace HoneyDrunk.Auth.Authorization;

/// <summary>
/// Default authorization policy implementation that evaluates scopes, roles, and ownership.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="DefaultAuthorizationPolicy"/> class.
/// </remarks>
/// <param name="telemetryFactory">The telemetry activity factory.</param>
/// <param name="logger">The logger.</param>
public sealed class DefaultAuthorizationPolicy(
    ITelemetryActivityFactory telemetryFactory,
    ILogger<DefaultAuthorizationPolicy> logger) : IAuthorizationPolicy
{
    private readonly ITelemetryActivityFactory _telemetryFactory = telemetryFactory ?? throw new ArgumentNullException(nameof(telemetryFactory));
    private readonly ILogger<DefaultAuthorizationPolicy> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

    /// <inheritdoc />
    public string PolicyName => "Default";

    /// <inheritdoc />
    public Task<AuthorizationDecision> EvaluateAsync(
        AuthenticatedIdentity? identity,
        AuthorizationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        using var activity = _telemetryFactory.Start(
            AuthTelemetry.AuthorizeActivityName,
            new Dictionary<string, object?>
            {
                [AuthTelemetry.Tags.Policy] = PolicyName,
            });

        var denyReasons = new List<DenyReason>();
        var satisfiedRequirements = new List<string>();

        // Check authentication
        if (identity is null)
        {
            return Task.FromResult(RecordDecision(
                activity,
                AuthorizationDecision.Deny(AuthorizationDenyCode.NotAuthenticated, "Request is not authenticated")));
        }

        satisfiedRequirements.Add("authenticated");

        // Check required scopes
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

        // Check required roles (any role is sufficient)
        if (request.RequiredRoles.Count > 0)
        {
            var hasAnyRole = false;
            foreach (var requiredRole in request.RequiredRoles)
            {
                if (identity.HasClaim(AuthClaimTypes.Role, requiredRole))
                {
                    satisfiedRequirements.Add($"role:{requiredRole}");
                    hasAnyRole = true;
                    break;
                }
            }

            if (!hasAnyRole)
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
            _logger.LogWarning(
                "Authorization denied for subject {SubjectId} on {Action} {Resource}: {Reasons}",
                identity.SubjectId,
                request.Action,
                request.Resource,
                string.Join("; ", denyReasons.Select(r => r.Message)));

            return Task.FromResult(RecordDecision(
                activity,
                AuthorizationDecision.Deny(denyReasons, satisfiedRequirements)));
        }

        _logger.LogDebug(
            "Authorization allowed for subject {SubjectId} on {Action} {Resource}",
            identity.SubjectId,
            request.Action,
            request.Resource);

        return Task.FromResult(RecordDecision(
            activity,
            AuthorizationDecision.Allow(satisfiedRequirements)));
    }

    private static AuthorizationDecision RecordDecision(
        System.Diagnostics.Activity? activity,
        AuthorizationDecision decision)
    {
        if (activity != null)
        {
            activity.SetTag(AuthTelemetry.Tags.AuthzResult, decision.IsAllowed ? AuthTelemetry.ResultAllow : AuthTelemetry.ResultDeny);

            if (!decision.IsAllowed && decision.DenyReasons.Count > 0)
            {
                activity.SetTag(AuthTelemetry.Tags.AuthzFailureCode, decision.DenyReasons[0].Code.ToString());
            }
        }

        return decision;
    }
}
