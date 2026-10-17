using HoneyDrunk.Auth.AspNetCore.DependencyInjection;
using HoneyDrunk.Vault.Abstractions;
using HoneyDrunk.Vault.EventGrid.Constants;
using HoneyDrunk.Vault.EventGrid.Extensions;
using HoneyDrunk.Vault.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace HoneyDrunk.Auth.Tests.AspNetCore;

/// <summary>
/// Tests Auth's Event Grid Vault invalidation endpoint wiring.
/// </summary>
public sealed class VaultInvalidationWebhookTests
{
    /// <summary>
    /// Verifies that the Auth webhook endpoint accepts a synthetic Key Vault event and invalidates the named secret.
    /// </summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Fact]
    public async Task MapHoneyDrunkAuthVaultInvalidationWebhook_InvalidatesSyntheticEventGridEvent()
    {
        // Arrange
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();

        var invalidator = new RecordingSecretCacheInvalidator();
        builder.Services.AddRouting();
        builder.Services.AddLogging();
        builder.Services.AddSingleton<ISecretStore>(new SharedSecretStore());
        builder.Services.AddSingleton<ISecretCacheInvalidator>(invalidator);
        builder.Services.AddVaultEventGridInvalidation();

        await using var app = builder.Build();
        app.MapHoneyDrunkAuthVaultInvalidationWebhook();
        await app.StartAsync();

        var client = app.GetTestClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/internal/vault/invalidate");
        request.Headers.Add(
            VaultInvalidationWebhookConstants.SharedSecretHeaderName,
            SharedSecretStore.SharedSecretValue);
        request.Content = new StringContent(
            """
            [
              {
                "id": "evt-secret-1",
                "eventType": "Microsoft.KeyVault.SecretNewVersionCreated",
                "subject": "Jwt--SigningKeys",
                "eventTime": "2026-04-12T12:00:00Z",
                "data": {
                  "id": "https://vault.vault.azure.net/secrets/Jwt--SigningKeys/version1",
                  "vaultName": "kv-hd-auth-dev",
                  "objectType": "Secret",
                  "objectName": "Jwt--SigningKeys"
                },
                "dataVersion": "1",
                "metadataVersion": "1"
              }
            ]
            """);

        // Act
        var response = await client.SendAsync(request);

        // Assert
        Assert.True(response.IsSuccessStatusCode);
        Assert.Equal(["Jwt--SigningKeys"], invalidator.InvalidatedSecrets);
    }

    private sealed class RecordingSecretCacheInvalidator : ISecretCacheInvalidator
    {
        public List<string> InvalidatedSecrets { get; } = [];

        public void Invalidate(string secretName)
        {
            InvalidatedSecrets.Add(secretName);
        }

        public void InvalidateAll()
        {
            InvalidatedSecrets.Add("*");
        }
    }

    private sealed class SharedSecretStore : ISecretStore
    {
        public const string SharedSecretValue = "expected-shared-secret";

        public Task<SecretValue> GetSecretAsync(SecretIdentifier identifier, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new SecretValue(identifier, SharedSecretValue, "v1"));
        }

        public Task<VaultResult<SecretValue>> TryGetSecretAsync(SecretIdentifier identifier, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(VaultResult.Success(new SecretValue(identifier, SharedSecretValue, "v1")));
        }

        public Task<IReadOnlyList<SecretVersion>> ListSecretVersionsAsync(string secretName, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<SecretVersion>>([]);
        }
    }
}
