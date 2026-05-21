using HoneyDrunk.Audit.Abstractions;
using HoneyDrunk.Auth.Abstractions;
using HoneyDrunk.Auth.Telemetry;
using HoneyDrunk.Kernel.Abstractions.Context;
using HoneyDrunk.Kernel.Abstractions.Telemetry;
using Microsoft.Extensions.Logging;
using System.Text;
using System.Text.Json;

namespace HoneyDrunk.Auth.Authorization;

/// <summary>
/// Default authorization policy implementation that wraps pure evaluation with telemetry.
/// </summary>
/// <remarks>
/// <para>
/// This class delegates to <see cref="AuthorizationPolicyEvaluator"/> for the actual
/// policy evaluation via its static <see cref="AuthorizationPolicyEvaluator.Evaluate"/> method,
/// which is pure and side-effect free. This wrapper adds telemetry
/// and logging as cross-cutting concerns.
/// </para>
/// <para>
/// Initializes a new instance of the <see cref="DefaultAuthorizationPolicy"/> class.
/// </para>
/// </remarks>
/// <param name="telemetryFactory">The telemetry activity factory.</param>
/// <param name="auditLog">The audit log used to append authorization outcomes.</param>
/// <param name="gridContextAccessor">The Grid context accessor used for correlation and tenant context.</param>
/// <param name="logger">The logger.</param>
public sealed class DefaultAuthorizationPolicy(
    ITelemetryActivityFactory telemetryFactory,
    IAuditLog auditLog,
    IGridContextAccessor gridContextAccessor,
    ILogger<DefaultAuthorizationPolicy> logger) : IAuthorizationPolicy
{
    private const int AuditContextMaxBytes = 4096;

    private readonly ITelemetryActivityFactory _telemetryFactory = telemetryFactory ?? throw new ArgumentNullException(nameof(telemetryFactory));
    private readonly IAuditLog _auditLog = auditLog ?? throw new ArgumentNullException(nameof(auditLog));
    private readonly IGridContextAccessor _gridContextAccessor = gridContextAccessor ?? throw new ArgumentNullException(nameof(gridContextAccessor));
    private readonly ILogger<DefaultAuthorizationPolicy> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

    /// <inheritdoc />
    public string PolicyName => "Default";

    /// <inheritdoc />
    public async Task<AuthorizationDecision> EvaluateAsync(
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
        var decision = AuthorizationPolicyEvaluator.Evaluate(identity, request);

        // Record telemetry and logging (side effects isolated here)
        RecordTelemetry(activity, decision);
        LogDecision(request, decision);

        await EmitAuthorizationAuditAsync(identity, request, decision, cancellationToken);

        return decision;
    }

    private static string CapContext(string json)
    {
        if (Encoding.UTF8.GetByteCount(json) <= AuditContextMaxBytes)
        {
            return json;
        }

        var bytes = Encoding.UTF8.GetBytes(json);
        return Encoding.UTF8.GetString(bytes, 0, AuditContextMaxBytes - 16) + "...[truncated]";
    }

    private static IReadOnlyDictionary<string, string> BuildAuthorizationMetadata(
        AuthorizationRequest request,
        AuthorizationDecision decision)
    {
        var metadata = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["policy"] = "Default",
            ["action"] = request.Action,
            ["resource"] = request.Resource,
        };

        if (decision.SatisfiedRequirements.Count > 0)
        {
            metadata["satisfiedRequirements"] = string.Join(",", decision.SatisfiedRequirements);
        }

        if (decision.DenyReasons.Count > 0)
        {
            metadata["denyReasonCodes"] = string.Join(",", decision.DenyReasons.Select(reason => reason.Code.ToString()));
        }

        var json = JsonSerializer.Serialize(metadata);
        return Encoding.UTF8.GetByteCount(json) <= AuditContextMaxBytes
            ? metadata
            : new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["context"] = CapContext(json),
                ["context.truncated"] = "true",
            };
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

    private async Task EmitAuthorizationAuditAsync(
        AuthenticatedIdentity? identity,
        AuthorizationRequest request,
        AuthorizationDecision decision,
        CancellationToken cancellationToken)
    {
        try
        {
            var gridContext = _gridContextAccessor.GridContext;
            await _auditLog.AppendAsync(
                new AuditEntry(
                    AuditEntryId.Empty,
                    DateTimeOffset.UtcNow,
                    identity?.SubjectId ?? "anonymous",
                    $"auth.authorize.{request.Action}",
                    AuditCategory.Security,
                    decision.IsAllowed ? AuditOutcome.Succeeded : AuditOutcome.Denied,
                    new AuditTarget("auth.resource", request.Resource),
                    gridContext.TenantId,
                    gridContext.CorrelationId,
                    Metadata: BuildAuthorizationMetadata(request, decision),
                    Reason: decision.IsAllowed ? null : string.Join(",", decision.DenyReasons.Select(reason => reason.Code.ToString()))),
                cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Audit emission failed for authorization decision; authorization outcome is unchanged");
        }
    }

    private void LogDecision(AuthorizationRequest request, AuthorizationDecision decision)
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
