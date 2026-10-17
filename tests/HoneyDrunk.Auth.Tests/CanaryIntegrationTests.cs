using System.Reflection;

namespace HoneyDrunk.Auth.Tests;

/// <summary>
/// Runs the standalone hosting canary in the instrumented test process.
/// </summary>
public sealed class CanaryIntegrationTests
{
    /// <summary>
    /// Verifies registration guards, JWT validation, key rotation and Vault failure behavior.
    /// </summary>
    [Fact]
    public void HostingCanaryPassesAllSecurityAndFailureScenarios()
    {
        var assembly = Assembly.Load("HoneyDrunk.Auth.Canary");
        var entryPoint = assembly.EntryPoint;
        Assert.NotNull(entryPoint);

        var result = entryPoint.Invoke(null, [Array.Empty<string>()]);

        Assert.Equal(0, Assert.IsType<int>(result));
    }
}
