using HoneyDrunk.Kernel.Abstractions.Context;
using HoneyDrunk.Kernel.Abstractions.Telemetry;
using System.Diagnostics;

namespace HoneyDrunk.Auth.Canary.Fakes;

/// <summary>
/// No-op telemetry factory that does nothing but allows Auth to function.
/// </summary>
internal sealed class NoOpTelemetryActivityFactory : ITelemetryActivityFactory
{
    public static NoOpTelemetryActivityFactory Instance { get; } = new();

    public Activity? Start(string name, IReadOnlyDictionary<string, object?>? tags = null)
    {
        return null;
    }

    public Activity? StartExplicit(string name, IGridContext gridContext, IOperationContext? operationContext = null, IReadOnlyDictionary<string, object?>? tags = null)
    {
        return null;
    }
}
