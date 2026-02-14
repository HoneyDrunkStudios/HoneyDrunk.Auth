using HoneyDrunk.Auth.Abstractions;
using HoneyDrunk.Auth.Telemetry;
using HoneyDrunk.Kernel.Abstractions.Telemetry;
using Microsoft.Extensions.Logging;

namespace HoneyDrunk.Auth.Authorization;

/// <summary>
/// Default authorization policy implementation that wraps pure evaluation with telemetry.
/// </summary>
/// <remarks>
/// <para>
/// This class delegates to <see cref="AuthorizationPolicyEvaluator"/> for the actual
/// policy evaluation, which is pure and side-effect free. This wrapper adds telemetry
/// and logging as cross-cutting concerns.
/// </para>
/// <para>
/// Initializes a new instance of the <see cref="DefaultAuthorizationPolicy"/> class.
/// </para>
/// </remarks>
/// <param name="evaluator">The pure policy evaluator.</param>
/// <param name="telemetryFactory">The telemetry activity factory.</param>
/// <param name="logger">The logger.</param>
public sealed class DefaultAuthorizationPolicy(
    AuthorizationPolicyEvaluator evaluator,
    ITelemetryActivityFactory telemetryFactory,
    ILogger<DefaultAuthorizationPolicy> logger) : IAuthorizationPolicy
{
    private readonly AuthorizationPolicyEvaluator _evaluator = evaluator ?? throw new ArgumentNullException(nameof(evaluator));
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

        // Start telemetry span
        using var activity = _telemetryFactory.Start(
            AuthTelemetry.AuthorizeActivityName,
            new Dictionary<string, object?>
            {
                [AuthTelemetry.Tags.Policy] = PolicyName,
            });

        // Delegate to pure evaluator
        var decision = _evaluator.Evaluate(identity, request);

        // Record telemetry and logging (side effects isolated here)
        RecordTelemetry(activity, decision);
        LogDecision(identity, request, decision);

        return Task.FromResult(decision);
    }

    private static void RecordTelemetry(System.Diagnostics.Activity? activity, AuthorizationDecision decision)
    {
        if (activity is null)
        {
            return;
        }

        activity.SetTag(AuthTelemetry.Tags.AuthzResult, decision.IsAllowed ? AuthTelemetry.ResultAllow : AuthTelemetry.ResultDeny);

        if (!decision.IsAllowed && decision.DenyReasons.Count > 0)
        {
            activity.SetTag(AuthTelemetry.Tags.AuthzFailureCode, decision.DenyReasons[0].Code.ToString());
        }
    }

    private void LogDecision(AuthenticatedIdentity? identity, AuthorizationRequest request, AuthorizationDecision decision)
    {
        if (decision.IsAllowed)
        {
            _logger.LogDebug(
                "Authorization allowed on {Action} {Resource}",
                request.Action,
                request.Resource);
        }
        else
        {
            // Do not log subject IDs in warnings to avoid leaking sensitive information
            _logger.LogDebug(
                "Authorization denied on {Action} {Resource}: {Reasons}",
                request.Action,
                request.Resource,
                string.Join("; ", decision.DenyReasons.Select(r => r.Message)));
        }
    }
}
