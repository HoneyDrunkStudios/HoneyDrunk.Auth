using HoneyDrunk.Auth.Secrets;
using HoneyDrunk.Kernel.Abstractions.Health;
using HoneyDrunk.Kernel.Abstractions.Lifecycle;
using Microsoft.Extensions.Logging;

namespace HoneyDrunk.Auth.Lifecycle;

/// <summary>
/// Health contributor that checks Auth system health.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="AuthHealthContributor"/> class.
/// </remarks>
/// <param name="keyProvider">The signing key provider.</param>
/// <param name="logger">The logger.</param>
public sealed class AuthHealthContributor(ISigningKeyProvider keyProvider, ILogger<AuthHealthContributor> logger) : IHealthContributor
{
    private readonly ISigningKeyProvider _keyProvider = keyProvider ?? throw new ArgumentNullException(nameof(keyProvider));
    private readonly ILogger<AuthHealthContributor> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

    /// <inheritdoc />
    public string Name => "Auth";

    /// <inheritdoc />
    public int Priority => 50;

    /// <inheritdoc />
    public bool IsCritical => true;

    /// <inheritdoc />
#pragma warning disable SA1316 // Tuple element names should use correct casing - interface uses lowercase
    public async Task<(HealthStatus status, string? message)> CheckHealthAsync(CancellationToken cancellationToken)
    {
        try
        {
            var signingKeys = await _keyProvider.GetSigningKeysAsync(cancellationToken);

            if (signingKeys.Count == 0)
            {
                return (HealthStatus.Unhealthy, "No signing keys available");
            }

            return (HealthStatus.Healthy, $"{signingKeys.Count} signing key(s) available");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Auth health check failed");
            return (HealthStatus.Unhealthy, $"Failed to retrieve signing keys: {ex.Message}");
        }
    }
#pragma warning restore SA1316
}
