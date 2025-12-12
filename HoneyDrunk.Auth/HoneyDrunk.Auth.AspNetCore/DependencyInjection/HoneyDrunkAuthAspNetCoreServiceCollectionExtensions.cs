using HoneyDrunk.Auth.DependencyInjection;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace HoneyDrunk.Auth.AspNetCore.DependencyInjection;

/// <summary>
/// Extension methods for registering HoneyDrunk Auth ASP.NET Core services.
/// </summary>
public static class HoneyDrunkAuthAspNetCoreServiceCollectionExtensions
{
    /// <summary>
    /// Adds HoneyDrunk Auth ASP.NET Core services to the service collection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    /// <remarks>
    /// This registers:
    /// <list type="bullet">
    /// <item>Core Auth services via <see cref="HoneyDrunkAuthServiceCollectionExtensions.AddHoneyDrunkAuth"/></item>
    /// <item><see cref="IAuthenticatedIdentityAccessor"/> - HTTP context-based identity accessor</item>
    /// <item><see cref="IHttpContextAccessor"/> - if not already registered</item>
    /// </list>
    /// </remarks>
    public static IServiceCollection AddHoneyDrunkAuthAspNetCore(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        // Add core Auth services
        services.AddHoneyDrunkAuth();

        // Add HTTP context accessor if not already registered
        services.TryAddSingleton<IHttpContextAccessor, HttpContextAccessor>();

        // Add identity accessor
        services.TryAddSingleton<IAuthenticatedIdentityAccessor, HttpContextIdentityAccessor>();

        return services;
    }
}
