using HoneyDrunk.Auth.Secrets;
using HoneyDrunk.Vault.Abstractions;
using HoneyDrunk.Vault.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace HoneyDrunk.Auth.Tests.Secrets;

/// <summary>
/// Tests for <see cref="VaultSigningKeyProvider"/>.
/// </summary>
public sealed class VaultSigningKeyProviderTests
{
    /// <summary>
    /// Verifies that signing keys are read only through ISecretStore with the ADR-0005 secret name.
    /// </summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Fact]
    public async Task GetSigningKeysAsync_ReadsJwtSigningKeysThroughSecretStore()
    {
        // Arrange
        var store = new RecordingSecretStore(CreateSigningKeysJson());
        var provider = CreateProvider(store);

        // Act
        var keys = await provider.GetSigningKeysAsync();

        // Assert
        Assert.Single(keys);
        Assert.Equal("auth-key-1", keys[0].KeyId);
        Assert.Equal(["Jwt--SigningKeys"], store.RequestedSecretNames);
        Assert.All(store.RequestedIdentifiers, identifier => Assert.Null(identifier.Version));
    }

    /// <summary>
    /// Verifies that non-secret settings are read from configuration rather than Vault secrets.
    /// </summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Fact]
    public async Task NonSecretSettings_ReadFromConfiguration()
    {
        // Arrange
        var store = new RecordingSecretStore(CreateSigningKeysJson());
        var provider = CreateProvider(
            store,
            new Dictionary<string, string?>
            {
                ["Auth:Issuer"] = "https://issuer.example.com",
                ["Auth:Audience"] = "api://honeydrunk-auth",
                ["Auth:ClockSkewSeconds"] = "42",
            });

        // Act
        var issuer = await provider.GetIssuerAsync();
        var audience = await provider.GetAudienceAsync();
        var clockSkew = await provider.GetClockSkewAsync();

        // Assert
        Assert.Equal("https://issuer.example.com", issuer);
        Assert.Equal("api://honeydrunk-auth", audience);
        Assert.Equal(TimeSpan.FromSeconds(42), clockSkew);
        Assert.Empty(store.RequestedSecretNames);
    }

    private static VaultSigningKeyProvider CreateProvider(
        ISecretStore secretStore,
        IReadOnlyDictionary<string, string?>? configurationValues = null)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configurationValues ?? new Dictionary<string, string?>
            {
                ["Auth:Issuer"] = "https://issuer.example.com",
                ["Auth:Audience"] = "api://honeydrunk-auth",
            })
            .Build();

        return new VaultSigningKeyProvider(
            secretStore,
            configuration,
            NullLogger<VaultSigningKeyProvider>.Instance);
    }

    private static string CreateSigningKeysJson()
    {
        var keyMaterial = Convert.ToBase64String(new byte[32]
        {
            1, 2, 3, 4, 5, 6, 7, 8,
            9, 10, 11, 12, 13, 14, 15, 16,
            17, 18, 19, 20, 21, 22, 23, 24,
            25, 26, 27, 28, 29, 30, 31, 32,
        });

        return $$"""[{"kid":"auth-key-1","alg":"HS256","key":"{{keyMaterial}}","active":true}]""";
    }

    private sealed class RecordingSecretStore(string signingKeysJson) : ISecretStore
    {
        public List<SecretIdentifier> RequestedIdentifiers { get; } = [];

        public List<string> RequestedSecretNames { get; } = [];

        public Task<SecretValue> GetSecretAsync(SecretIdentifier identifier, CancellationToken cancellationToken = default)
        {
            RequestedIdentifiers.Add(identifier);
            RequestedSecretNames.Add(identifier.Name);
            return Task.FromResult(new SecretValue(identifier, signingKeysJson, "v1"));
        }

        public Task<VaultResult<SecretValue>> TryGetSecretAsync(SecretIdentifier identifier, CancellationToken cancellationToken = default)
        {
            RequestedIdentifiers.Add(identifier);
            RequestedSecretNames.Add(identifier.Name);
            return Task.FromResult(VaultResult.Success(new SecretValue(identifier, signingKeysJson, "v1")));
        }

        public Task<IReadOnlyList<SecretVersion>> ListSecretVersionsAsync(string secretName, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<SecretVersion>>([]);
        }
    }
}
