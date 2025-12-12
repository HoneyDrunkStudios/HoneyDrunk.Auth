using HoneyDrunk.Auth.Secrets;
using HoneyDrunk.Kernel.Abstractions.Lifecycle;
using Microsoft.Extensions.Logging;

namespace HoneyDrunk.Auth.Lifecycle;

/// <summary>
/// Readiness contributor that checks if Auth system is ready to process requests.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="AuthReadinessContributor"/> class.
/// </remarks>
/// <param name="keyProvider">The signing key provider.</param>
/// <param name="logger">The logger.</param>
public sealed class AuthReadinessContributor(ISigningKeyProvider keyProvider, ILogger<AuthReadinessContributor> logger) : IReadinessContributor
{
    private readonly ISigningKeyProvider _keyProvider = keyProvider ?? throw new ArgumentNullException(nameof(keyProvider));
    private readonly ILogger<AuthReadinessContributor> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

    /// <inheritdoc />
    public string Name => "Auth";

    /// <inheritdoc />
    public int Priority => 50;

    /// <inheritdoc />
    public bool IsRequired => true;

    /// <inheritdoc />
    public async Task<(bool isReady, string? reason)> CheckReadinessAsync(CancellationToken cancellationToken)
    {
        try
        {
            var issuer = await _keyProvider.GetIssuerAsync(cancellationToken);
            var audience = await _keyProvider.GetAudienceAsync(cancellationToken);
            var signingKeys = await _keyProvider.GetSigningKeysAsync(cancellationToken);

            if (string.IsNullOrWhiteSpace(issuer) || string.IsNullOrWhiteSpace(audience) || signingKeys.Count == 0)
            {
                return (false, "Auth secrets not fully configured");
            }

            return (true, "Auth system ready");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Auth readiness check failed");
            return (false, $"Failed to verify Auth secrets: {ex.Message}");
        }
    }
}
