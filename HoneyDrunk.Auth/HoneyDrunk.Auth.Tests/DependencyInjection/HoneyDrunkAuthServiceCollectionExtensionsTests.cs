using HoneyDrunk.Audit.Abstractions;
using HoneyDrunk.Auth.Authentication;
using HoneyDrunk.Auth.DependencyInjection;
using HoneyDrunk.Kernel.Abstractions.Context;
using HoneyDrunk.Kernel.Abstractions.Telemetry;
using HoneyDrunk.Vault.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace HoneyDrunk.Auth.Tests.DependencyInjection;

/// <summary>
/// Tests for <see cref="HoneyDrunkAuthServiceCollectionExtensions" /> audit registration behavior.
/// </summary>
public sealed class HoneyDrunkAuthServiceCollectionExtensionsTests
{
    /// <summary>
    /// Tests that Auth registers the no-op audit sink when no host audit log exists.
    /// </summary>
    [Fact]
    public void AddHoneyDrunkAuth_NoAuditLogRegistered_RegistersNullAuditLog()
    {
        // Arrange
        var services = CreateServiceCollection();

        // Act
        services.AddHoneyDrunkAuth();
        using var provider = services.BuildServiceProvider();

        // Assert
        Assert.IsType<NullAuditLog>(provider.GetRequiredService<IAuditLog>());
    }

    /// <summary>
    /// Tests that host-registered audit logs take precedence over Auth's no-op fallback.
    /// </summary>
    [Fact]
    public void AddHoneyDrunkAuth_HostAuditLogRegistered_PreservesHostRegistration()
    {
        // Arrange
        var services = CreateServiceCollection();
        var auditLog = Substitute.For<IAuditLog>();
        services.AddSingleton(auditLog);

        // Act
        services.AddHoneyDrunkAuth();
        using var provider = services.BuildServiceProvider();

        // Assert
        Assert.Same(auditLog, provider.GetRequiredService<IAuditLog>());
    }

    private static ServiceCollection CreateServiceCollection()
    {
        var services = new ServiceCollection();
        services.AddSingleton(Substitute.For<IGridContextAccessor>());
        services.AddSingleton(Substitute.For<IOperationContextAccessor>());
        services.AddSingleton(Substitute.For<ITelemetryActivityFactory>());
        services.AddSingleton(Substitute.For<ISecretStore>());
        return services;
    }
}
