using HoneyDrunk.Kernel.Abstractions.Context;
using HoneyDrunk.Kernel.Abstractions.Identity;

namespace HoneyDrunk.Auth.Canary.Fakes;

internal sealed class CanaryGridContext : IGridContext
{
    public bool IsInitialized => true;

    public string CorrelationId => "canary-correlation";

    public string? CausationId => null;

    public string NodeId => "auth-canary";

    public string StudioId => "honeydrunk";

    public string Environment => "canary";

    public TenantId TenantId => TenantId.Internal;

    public string? ProjectId => null;

    public CancellationToken Cancellation => CancellationToken.None;

    public IReadOnlyDictionary<string, string> Baggage { get; } = new Dictionary<string, string>();

    public DateTimeOffset CreatedAtUtc { get; } = DateTimeOffset.UtcNow;

    public void AddBaggage(string key, string value)
    {
    }
}
