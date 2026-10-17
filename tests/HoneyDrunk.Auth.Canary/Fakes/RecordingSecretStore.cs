using HoneyDrunk.Vault.Abstractions;
using HoneyDrunk.Vault.Models;

namespace HoneyDrunk.Auth.Canary.Fakes;

/// <summary>
/// Secret store fake that records every requested secret identifier.
/// </summary>
internal sealed class RecordingSecretStore(string signingKeysJson) : ISecretStore
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
