using HoneyDrunk.Auth.Secrets;
using HoneyDrunk.Kernel.Abstractions.Lifecycle;
using Microsoft.Extensions.Logging;

namespace HoneyDrunk.Auth.Lifecycle;

/// <summary>
/// Startup hook that validates required Auth secrets are available in Vault.
/// </summary>
/// <remarks>
/// Implements fail-fast behavior by checking for issuer, audience, and signing keys at startup.
/// </remarks>
/// <remarks>
/// Initializes a new instance of the <see cref="AuthStartupHook"/> class.
/// </remarks>
/// <param name="keyProvider">The signing key provider.</param>
/// <param name="logger">The logger.</param>
public sealed class AuthStartupHook(ISigningKeyProvider keyProvider, ILogger<AuthStartupHook> logger) : IStartupHook
{
    private readonly ISigningKeyProvider _keyProvider = keyProvider ?? throw new ArgumentNullException(nameof(keyProvider));
    private readonly ILogger<AuthStartupHook> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

    /// <inheritdoc />
    public int Priority => 100;

    /// <inheritdoc />
    public async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Validating Auth secrets in Vault...");

        var errors = new List<string>();

        // Validate issuer
        try
        {
            var issuer = await _keyProvider.GetIssuerAsync(cancellationToken);
            if (string.IsNullOrWhiteSpace(issuer))
            {
                errors.Add("auth:issuer is empty or missing");
            }
            else
            {
                _logger.LogDebug("Validated auth:issuer = {Issuer}", issuer);
            }
        }
        catch (Exception ex)
        {
            errors.Add($"Failed to retrieve auth:issuer: {ex.Message}");
        }

        // Validate audience
        try
        {
            var audience = await _keyProvider.GetAudienceAsync(cancellationToken);
            if (string.IsNullOrWhiteSpace(audience))
            {
                errors.Add("auth:audience is empty or missing");
            }
            else
            {
                _logger.LogDebug("Validated auth:audience = {Audience}", audience);
            }
        }
        catch (Exception ex)
        {
            errors.Add($"Failed to retrieve auth:audience: {ex.Message}");
        }

        // Validate signing keys
        try
        {
            var signingKeys = await _keyProvider.GetSigningKeysAsync(cancellationToken);
            if (signingKeys.Count == 0)
            {
                errors.Add("No active signing keys found in auth:signing_keys");
            }
            else
            {
                _logger.LogDebug("Validated {KeyCount} active signing keys", signingKeys.Count);
            }
        }
        catch (Exception ex)
        {
            errors.Add($"Failed to retrieve auth:signing_keys: {ex.Message}");
        }

        if (errors.Count > 0)
        {
            var errorMessage = string.Join(Environment.NewLine, errors);
            _logger.LogCritical("Auth startup validation failed:{NewLine}{Errors}", Environment.NewLine, errorMessage);
            throw new InvalidOperationException($"Auth startup validation failed: {errorMessage}");
        }

        _logger.LogInformation("Auth secrets validation completed successfully");
    }
}
