using HoneyDrunk.Kernel.Abstractions.Context;

namespace HoneyDrunk.Auth.Canary.Fakes;

internal sealed class CanaryGridContextAccessor : IGridContextAccessor
{
    public static CanaryGridContextAccessor Instance { get; } = new();

    public IGridContext GridContext { get; } = new CanaryGridContext();
}
