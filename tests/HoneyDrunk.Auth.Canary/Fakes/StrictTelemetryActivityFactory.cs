using HoneyDrunk.Kernel.Abstractions.Context;
using HoneyDrunk.Kernel.Abstractions.Telemetry;
using System.Diagnostics;

namespace HoneyDrunk.Auth.Canary.Fakes;

/// <summary>
/// Strict telemetry factory that throws if any telemetry is started.
/// Used to verify the pure evaluator does not invoke telemetry.
/// </summary>
internal sealed class StrictTelemetryActivityFactory : ITelemetryActivityFactory
{
    public static StrictTelemetryActivityFactory Instance { get; } = new();

    public Activity? Start(string name, IReadOnlyDictionary<string, object?>? additionalTags = null)
    {
        throw new InvalidOperationException($"PURITY VIOLATION: Telemetry activity '{name}' started in a context where side effects are forbidden.");
    }

    public Activity? StartExplicit(string name, IGridContext gridContext, IOperationContext? operationContext = null, IReadOnlyDictionary<string, object?>? additionalTags = null)
    {
        throw new InvalidOperationException($"PURITY VIOLATION: Telemetry activity '{name}' started explicitly in a context where side effects are forbidden.");
    }
}
