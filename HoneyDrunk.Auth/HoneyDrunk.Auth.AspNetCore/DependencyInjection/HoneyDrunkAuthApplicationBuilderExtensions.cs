using HoneyDrunk.Auth.AspNetCore.Middleware;
using Microsoft.AspNetCore.Builder;

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
}
