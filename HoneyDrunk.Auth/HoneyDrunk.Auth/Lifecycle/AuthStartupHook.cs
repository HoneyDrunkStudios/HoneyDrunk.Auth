using HoneyDrunk.Audit.Abstractions;
using HoneyDrunk.Auth.Authentication;
using HoneyDrunk.Auth.Secrets;
using HoneyDrunk.Kernel.Abstractions.Lifecycle;
using Microsoft.Extensions.Logging;

namespace HoneyDrunk.Auth.Lifecycle;

/// <summary>
/// Startup hook that validates required Auth secrets/configuration are available and preloads the cache.
/// </summary>
/// <remarks>
/// <para>
/// Implements fail-fast behavior by checking for issuer, audience, and signing keys at startup.
/// If the key provider is the caching decorator, this will also preload the cache.
/// </para>
/// <para>
/// Initializes a new instance of the <see cref="AuthStartupHook"/> class.
/// </para>
/// </remarks>
/// <param name="keyProvider">The signing key provider.</param>
/// <param name="auditLog">The audit log registration used to detect no-op audit composition.</param>
/// <param name="logger">The logger.</param>
public sealed class AuthStartupHook(
    ISigningKeyProvider keyProvider,
    IAuditLog auditLog,
    ILogger<AuthStartupHook> logger) : IStartupHook
{
    private readonly ISigningKeyProvider _keyProvider = keyProvider ?? throw new ArgumentNullException(nameof(keyProvider));
    private readonly IAuditLog _auditLog = auditLog ?? throw new ArgumentNullException(nameof(auditLog));
    private readonly ILogger<AuthStartupHook> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

    /// <inheritdoc />
    public int Priority => 100;

    /// <inheritdoc />
    public async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Validating Auth secrets and configuration...");

        // If using caching provider, preload the cache
        if (_keyProvider is CachingSigningKeyProvider cachingProvider)
        {
            try
            {
                await cachingProvider.PreloadAsync(cancellationToken);
                _logger.LogDebug("Auth cache preloaded successfully");
            }
            catch (Exception ex)
            {
                _logger.LogCritical(ex, "Auth startup validation failed: unable to load Auth bootstrap values");
                throw new InvalidOperationException("Auth startup validation failed: unable to load Auth bootstrap values", ex);
            }
        }

        // Validate configuration values
        var errors = new List<string>();

        // Validate issuer
        try
        {
            var issuer = await _keyProvider.GetIssuerAsync(cancellationToken);
            if (string.IsNullOrWhiteSpace(issuer))
            {
                errors.Add("Auth:Issuer is empty or missing");
            }
            else
            {
                _logger.LogDebug("Validated Auth:Issuer");
            }
        }
        catch (Exception ex)
        {
            errors.Add($"Failed to retrieve Auth:Issuer: {ex.Message}");
        }

        // Validate audience
        try
        {
            var audience = await _keyProvider.GetAudienceAsync(cancellationToken);
            if (string.IsNullOrWhiteSpace(audience))
            {
                errors.Add("Auth:Audience is empty or missing");
            }
            else
            {
                _logger.LogDebug("Validated Auth:Audience");
            }
        }
        catch (Exception ex)
        {
            errors.Add($"Failed to retrieve Auth:Audience: {ex.Message}");
        }

        // Validate signing keys
        try
        {
            var signingKeys = await _keyProvider.GetSigningKeysAsync(cancellationToken);
            if (signingKeys.Count == 0)
            {
                errors.Add("No active signing keys found in Jwt--SigningKeys");
            }
            else
            {
                _logger.LogDebug("Validated {KeyCount} active signing keys", signingKeys.Count);
            }
        }
        catch (Exception ex)
        {
            errors.Add($"Failed to retrieve Jwt--SigningKeys: {ex.Message}");
        }

        if (errors.Count > 0)
        {
            var errorMessage = string.Join(Environment.NewLine, errors);
            _logger.LogCritical("Auth startup validation failed:{NewLine}{Errors}", Environment.NewLine, errorMessage);
            throw new InvalidOperationException($"Auth startup validation failed: {errorMessage}");
        }

        _logger.LogInformation("Auth secrets and configuration validation completed successfully");

        if (_auditLog is NullAuditLog)
        {
            _logger.LogWarning(
                "::warning:: HoneyDrunk.Audit.Abstractions.IAuditLog is not registered in the host container; security event audit emission is disabled (NullAuditLog stub active). Compose HoneyDrunk.Audit.Data (or another IAuditLog backing) in the host to enable durable security-event audit per the Grid's audit-emission boundary invariant. See https://github.com/HoneyDrunkStudios/HoneyDrunk.Audit#for-downstream-consumers---minimal-wiring.");
        }
    }
}
