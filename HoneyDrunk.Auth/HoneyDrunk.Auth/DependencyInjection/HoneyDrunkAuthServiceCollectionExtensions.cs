using HoneyDrunk.Auth.Abstractions;
using HoneyDrunk.Auth.Authentication;
using HoneyDrunk.Auth.Authorization;
using HoneyDrunk.Auth.Lifecycle;
using HoneyDrunk.Auth.Secrets;
using HoneyDrunk.Kernel.Abstractions.Lifecycle;
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
    /// <remarks>
    /// This registers:
    /// <list type="bullet">
    /// <item><see cref="ISigningKeyProvider"/> - Vault-backed signing key provider</item>
    /// <item><see cref="IAuthenticationProvider"/> - Bearer token authentication provider</item>
    /// <item><see cref="IAuthorizationPolicy"/> - Default authorization policy</item>
    /// <item><see cref="IStartupHook"/> - Auth startup validation hook</item>
    /// <item><see cref="IHealthContributor"/> - Auth health contributor</item>
    /// <item><see cref="IReadinessContributor"/> - Auth readiness contributor</item>
    /// </list>
    /// </remarks>
    public static IServiceCollection AddHoneyDrunkAuth(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        // Vault-backed key provider
        services.TryAddSingleton<ISigningKeyProvider, VaultSigningKeyProvider>();

        // Authentication
        services.TryAddSingleton<IAuthenticationProvider, BearerTokenAuthenticationProvider>();

        // Authorization
        services.TryAddSingleton<IAuthorizationPolicy, DefaultAuthorizationPolicy>();

        // Lifecycle
        services.AddSingleton<IStartupHook, AuthStartupHook>();
        services.AddSingleton<IHealthContributor, AuthHealthContributor>();
        services.AddSingleton<IReadinessContributor, AuthReadinessContributor>();

        return services;
    }
}
