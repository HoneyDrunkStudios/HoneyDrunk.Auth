using HoneyDrunk.Vault.EventGrid.Extensions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace HoneyDrunk.Auth.AspNetCore.DependencyInjection;

/// <summary>
/// Extension methods for mapping HoneyDrunk Auth endpoints.
/// </summary>
public static class HoneyDrunkAuthEndpointRouteBuilderExtensions
{
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
