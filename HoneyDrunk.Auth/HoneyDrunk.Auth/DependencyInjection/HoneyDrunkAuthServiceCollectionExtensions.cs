using HoneyDrunk.Auth.Abstractions;
using HoneyDrunk.Auth.Authentication;
using HoneyDrunk.Auth.Authorization;
using HoneyDrunk.Auth.Lifecycle;
using HoneyDrunk.Auth.Secrets;
using HoneyDrunk.Kernel.Abstractions.Lifecycle;
using HoneyDrunk.Kernel.Abstractions.Telemetry;
using HoneyDrunk.Vault.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace HoneyDrunk.Auth.DependencyInjection;

/// <summary>
/// Extension methods for registering HoneyDrunk Auth services.
/// </summary>
public static class HoneyDrunkAuthServiceCollectionExtensions
{
    /// <summary>
    /// Adds HoneyDrunk Auth services to the service collection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when required Kernel or Vault services are not registered.
    /// </exception>
    /// <remarks>
    /// <para>
    /// <b>Prerequisites:</b> This method requires HoneyDrunk.Kernel and HoneyDrunk.Vault
    /// services to be registered first. Call <c>AddHoneyDrunkNode()</c> and <c>AddVault()</c>
    /// before calling this method.
    /// </para>
    /// <para>This registers:</para>
    /// <list type="bullet">
    /// <item><see cref="ISigningKeyProvider"/> - Cached Vault-backed signing key provider</item>
    /// <item><see cref="IAuthenticationProvider"/> - Bearer token authentication provider</item>
    /// <item><see cref="IAuthorizationPolicy"/> - Default authorization policy</item>
    /// <item><see cref="IStartupHook"/> - Auth startup validation hook</item>
    /// <item><see cref="IHealthContributor"/> - Auth health contributor</item>
    /// <item><see cref="IReadinessContributor"/> - Auth readiness contributor</item>
    /// </list>
    /// </remarks>
    public static IServiceCollection AddHoneyDrunkAuth(this IServiceCollection services)
    {
        return AddHoneyDrunkAuth(services, _ => { });
    }

    /// <summary>
    /// Adds HoneyDrunk Auth services to the service collection with custom options.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Action to configure <see cref="AuthOptions"/>.</param>
    /// <returns>The service collection for chaining.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when required Kernel or Vault services are not registered.
    /// </exception>
    public static IServiceCollection AddHoneyDrunkAuth(
        this IServiceCollection services,
        Action<AuthOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        // Validate required dependencies are registered
        ValidateKernelServices(services);
        ValidateVaultServices(services);

        // Register options
        services.Configure(configure);

        // Vault-backed key provider (raw, no caching)
        services.TryAddSingleton<VaultSigningKeyProvider>();

        // Caching decorator as the public ISigningKeyProvider
        services.TryAddSingleton<ISigningKeyProvider>(sp =>
            new CachingSigningKeyProvider(
                sp.GetRequiredService<VaultSigningKeyProvider>(),
                sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<AuthOptions>>(),
                sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<CachingSigningKeyProvider>>()));

        // Authentication
        services.TryAddSingleton<IAuthenticationProvider, BearerTokenAuthenticationProvider>();

        // Authorization - pure evaluator wrapped with telemetry
        services.TryAddSingleton<AuthorizationPolicyEvaluator>();
        services.TryAddSingleton<IAuthorizationPolicy, DefaultAuthorizationPolicy>();

        // Lifecycle
        services.AddSingleton<IStartupHook, AuthStartupHook>();
        services.AddSingleton<IHealthContributor, AuthHealthContributor>();
        services.AddSingleton<IReadinessContributor, AuthReadinessContributor>();

        return services;
    }

    private static void ValidateKernelServices(IServiceCollection services)
    {
        var hasTelemetryFactory = services.Any(sd =>
            sd.ServiceType == typeof(ITelemetryActivityFactory));

        if (!hasTelemetryFactory)
        {
            throw new InvalidOperationException(
                "HoneyDrunk.Auth requires HoneyDrunk.Kernel services. " +
                "Call AddHoneyDrunkNode() before AddHoneyDrunkAuth().");
        }
    }

    private static void ValidateVaultServices(IServiceCollection services)
    {
        var hasSecretStore = services.Any(sd =>
            sd.ServiceType == typeof(ISecretStore));

        var hasVaultClient = services.Any(sd =>
            sd.ServiceType == typeof(IVaultClient));

        if (!hasSecretStore || !hasVaultClient)
        {
            throw new InvalidOperationException(
                "HoneyDrunk.Auth requires HoneyDrunk.Vault services. " +
                "Call AddVault() before AddHoneyDrunkAuth().");
        }
    }
}
