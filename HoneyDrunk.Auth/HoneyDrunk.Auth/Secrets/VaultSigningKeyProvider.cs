using HoneyDrunk.Vault.Abstractions;
using HoneyDrunk.Vault.Models;
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
/// <param name="vaultClient">The vault client.</param>
/// <param name="logger">The logger.</param>
public sealed class VaultSigningKeyProvider(
    ISecretStore secretStore,
    IVaultClient vaultClient,
    ILogger<VaultSigningKeyProvider> logger) : ISigningKeyProvider
{
    private const string IssuerKey = "auth:issuer";
    private const string AudienceKey = "auth:audience";
    private const string SigningKeysKey = "auth:signing_keys";
    private const string ClockSkewKey = "auth:clock_skew_seconds";
    private const int DefaultClockSkewSeconds = 300;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly ISecretStore _secretStore = secretStore ?? throw new ArgumentNullException(nameof(secretStore));
    private readonly IVaultClient _vaultClient = vaultClient ?? throw new ArgumentNullException(nameof(vaultClient));
    private readonly ILogger<VaultSigningKeyProvider> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

    /// <inheritdoc />
    public async Task<IReadOnlyList<SecurityKey>> GetSigningKeysAsync(CancellationToken cancellationToken = default)
    {
        var secretResult = await _secretStore.TryGetSecretAsync(new SecretIdentifier(SigningKeysKey), cancellationToken);

        if (!secretResult.IsSuccess || secretResult.Value is null)
        {
            _logger.LogError("Failed to retrieve signing keys from Vault: {Error}", secretResult.ErrorMessage);
            return [];
        }

        var signingKeys = ParseSigningKeys(secretResult.Value.Value);
        var activeKeys = signingKeys.Where(k => k.IsActive).ToList();

        _logger.LogDebug("Retrieved {KeyCount} active signing keys from Vault", activeKeys.Count);

        return [.. activeKeys.Select(CreateSecurityKey)];
    }

    /// <inheritdoc />
    public async Task<string> GetIssuerAsync(CancellationToken cancellationToken = default)
    {
        var issuer = await _vaultClient.GetConfigValueAsync(IssuerKey, cancellationToken);
        return issuer;
    }

    /// <inheritdoc />
    public async Task<string> GetAudienceAsync(CancellationToken cancellationToken = default)
    {
        var audience = await _vaultClient.GetConfigValueAsync(AudienceKey, cancellationToken);
        return audience;
    }

    /// <inheritdoc />
    public async Task<TimeSpan> GetClockSkewAsync(CancellationToken cancellationToken = default)
    {
        var clockSkewSeconds = await _vaultClient.TryGetConfigValueAsync(ClockSkewKey, DefaultClockSkewSeconds, cancellationToken);
        return TimeSpan.FromSeconds(clockSkewSeconds);
    }

    private static List<SigningKeyInfo> ParseSigningKeys(string json)
    {
        var keys = JsonSerializer.Deserialize<List<SigningKeyInfoDto>>(json, JsonOptions) ?? [];

        return [.. keys
            .Where(k => !string.IsNullOrWhiteSpace(k.Kid) && !string.IsNullOrWhiteSpace(k.Key))
            .Select(k => new SigningKeyInfo(k.Kid!, k.Alg ?? "HS256", k.Key!, k.Active ?? true))];
    }

    private static SecurityKey CreateSecurityKey(SigningKeyInfo keyInfo)
    {
        var keyBytes = Convert.FromBase64String(keyInfo.KeyMaterial);
        return new SymmetricSecurityKey(keyBytes) { KeyId = keyInfo.KeyId };
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
