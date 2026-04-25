using HoneyDrunk.Auth.AspNetCore.Middleware;
using HoneyDrunk.Vault.EventGrid.Extensions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace HoneyDrunk.Auth.AspNetCore.DependencyInjection;

/// <summary>
/// Extension methods for configuring the HoneyDrunk Auth middleware in the ASP.NET Core pipeline.
/// </summary>
public static class HoneyDrunkAuthApplicationBuilderExtensions
{
    /// <summary>
    /// Adds the HoneyDrunk Auth middleware to the request pipeline.
    /// </summary>
    /// <param name="app">The application builder.</param>
    /// <returns>The application builder for chaining.</returns>
    /// <remarks>
    /// This middleware should be registered after UseRouting and before UseEndpoints.
    /// It reads the Authorization header and authenticates Bearer tokens.
    /// </remarks>
    public static IApplicationBuilder UseHoneyDrunkAuth(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);
        return app.UseMiddleware<HoneyDrunkAuthMiddleware>();
    }

    /// <summary>
    /// Maps the internal Vault invalidation webhook used by Event Grid secret rotation notifications.
    /// </summary>
    /// <param name="endpoints">The route builder.</param>
    /// <returns>The endpoint convention builder.</returns>
    public static IEndpointConventionBuilder MapHoneyDrunkAuthVaultInvalidationWebhook(
        this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        return endpoints.MapVaultInvalidationWebhook("/internal/vault/invalidate");
    }
}
