using HoneyDrunk.Kernel.Abstractions.Hosting;

namespace HoneyDrunk.Auth.DependencyInjection;

/// <summary>
/// Extension methods for <see cref="IHoneyDrunkBuilder"/> to add Auth services.
/// </summary>
public static class HoneyDrunkAuthBuilderExtensions
{
    /// <summary>
    /// Adds HoneyDrunk Auth services to the HoneyDrunk node.
    /// </summary>
    /// <param name="builder">The HoneyDrunk builder.</param>
    /// <returns>The builder for chaining.</returns>
    public static IHoneyDrunkBuilder AddAuth(this IHoneyDrunkBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.Services.AddHoneyDrunkAuth();
        return builder;
    }
}
