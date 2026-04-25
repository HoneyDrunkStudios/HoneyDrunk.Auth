using HoneyDrunk.Kernel.Abstractions.Hosting;
using HoneyDrunk.Vault.Providers.AppConfiguration.Extensions;
using HoneyDrunk.Vault.Providers.AzureKeyVault.Extensions;

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

    /// <summary>
    /// Adds the ADR-0005 compliant Auth bootstrap: Key Vault from <c>AZURE_KEYVAULT_URI</c>,
    /// App Configuration from <c>AZURE_APPCONFIG_ENDPOINT</c>, and Auth services.
    /// </summary>
    /// <param name="builder">The HoneyDrunk builder.</param>
    /// <returns>The builder for chaining.</returns>
    public static IHoneyDrunkBuilder AddAuthBootstrap(this IHoneyDrunkBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.AddVaultWithAzureKeyVaultBootstrap();
        builder.AddAppConfiguration();
        builder.Services.AddHoneyDrunkAuth();

        return builder;
    }
}
