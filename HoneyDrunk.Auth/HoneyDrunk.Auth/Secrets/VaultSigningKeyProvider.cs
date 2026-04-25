using HoneyDrunk.Vault.Abstractions;
using HoneyDrunk.Vault.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using System.Text.Json;

namespace HoneyDrunk.Auth.Secrets;

/// <summary>
/// Vault-backed implementation of <see cref="ISigningKeyProvider"/>.
/// </summary>
/// <remarks>
/// Retrieves signing keys and configuration from HoneyDrunk.Vault.
/// </remarks>
/// <remarks>
/// Initializes a new instance of the <see cref="VaultSigningKeyProvider"/> class.
/// </remarks>
/// <param name="secretStore">The secret store.</param>
/// <param name="configuration">The non-secret application configuration.</param>
/// <param name="logger">The logger.</param>
public sealed class VaultSigningKeyProvider(
    ISecretStore secretStore,
    IConfiguration configuration,
    ILogger<VaultSigningKeyProvider> logger) : ISigningKeyProvider
{
    private const string IssuerKey = "Auth:Issuer";
    private const string AudienceKey = "Auth:Audience";
    private const string SigningKeysKey = "Jwt--SigningKeys";
    private const string ClockSkewKey = "Auth:ClockSkewSeconds";
    private const int DefaultClockSkewSeconds = 300;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly ISecretStore _secretStore = secretStore ?? throw new ArgumentNullException(nameof(secretStore));
    private readonly IConfiguration _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
    private readonly ILogger<VaultSigningKeyProvider> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

    /// <inheritdoc />
    public async Task<IReadOnlyList<SecurityKey>> GetSigningKeysAsync(CancellationToken cancellationToken = default)
    {
        var secretResult = await _secretStore.TryGetSecretAsync(new SecretIdentifier(SigningKeysKey), cancellationToken);

        if (!secretResult.IsSuccess || secretResult.Value is null)
        {
            _logger.LogError("Failed to retrieve signing keys from Vault: {Error}", secretResult.ErrorMessage);
            throw new InvalidOperationException($"Failed to retrieve signing keys from Vault: {secretResult.ErrorMessage}");
        }

        var signingKeys = ParseSigningKeys(secretResult.Value.Value);
        if (signingKeys.Count == 0)
        {
            _logger.LogWarning("No valid signing keys found in Vault secret");
            return [];
        }

        var activeKeys = signingKeys.Where(k => k.IsActive).ToList();

        _logger.LogDebug("Retrieved {KeyCount} active signing keys from Vault", activeKeys.Count);

        return [.. activeKeys.Select(CreateSecurityKey)];
    }

    /// <inheritdoc />
    public Task<string> GetIssuerAsync(CancellationToken cancellationToken = default)
    {
        _ = cancellationToken;
        return Task.FromResult(_configuration[IssuerKey] ?? string.Empty);
    }

    /// <inheritdoc />
    public Task<string> GetAudienceAsync(CancellationToken cancellationToken = default)
    {
        _ = cancellationToken;
        return Task.FromResult(_configuration[AudienceKey] ?? string.Empty);
    }

    /// <inheritdoc />
    public Task<TimeSpan> GetClockSkewAsync(CancellationToken cancellationToken = default)
    {
        _ = cancellationToken;

        var clockSkewSeconds = _configuration.GetValue(ClockSkewKey, DefaultClockSkewSeconds);
        return Task.FromResult(TimeSpan.FromSeconds(clockSkewSeconds));
    }

    private static bool IsValidBase64Key(SigningKeyInfo keyInfo)
    {
        try
        {
            _ = Convert.FromBase64String(keyInfo.KeyMaterial);
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static SecurityKey CreateSecurityKey(SigningKeyInfo keyInfo)
    {
        var keyBytes = Convert.FromBase64String(keyInfo.KeyMaterial);
        return new SymmetricSecurityKey(keyBytes) { KeyId = keyInfo.KeyId };
    }

    private List<SigningKeyInfo> ParseSigningKeys(string json)
    {
        try
        {
            var keys = JsonSerializer.Deserialize<List<SigningKeyInfoDto>>(json, JsonOptions) ?? [];

            return [.. keys
                .Where(k => !string.IsNullOrWhiteSpace(k.Kid) && !string.IsNullOrWhiteSpace(k.Key))
                .Select(k => new SigningKeyInfo(k.Kid!, k.Alg ?? "HS256", k.Key!, k.Active ?? true))
                .Where(IsValidBase64Key)];
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to parse signing keys JSON from Vault");
            return [];
        }
    }

    /// <summary>
    /// DTO for deserializing signing key JSON from Vault.
    /// </summary>
#pragma warning disable CA1812 // Class is instantiated by JsonSerializer.Deserialize
    private sealed class SigningKeyInfoDto
    {
        public string? Kid { get; set; }

        public string? Alg { get; set; }

        public string? Key { get; set; }

        public bool? Active { get; set; }
    }
#pragma warning restore CA1812
}
