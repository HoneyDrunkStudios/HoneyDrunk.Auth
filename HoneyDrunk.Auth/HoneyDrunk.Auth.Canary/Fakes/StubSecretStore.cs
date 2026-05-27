using HoneyDrunk.Vault.Abstractions;
using HoneyDrunk.Vault.Models;

namespace HoneyDrunk.Auth.Canary.Fakes;

/// <summary>
/// Minimal stub ISecretStore that satisfies registration-time guards.
/// Does not need to actually work - just needs to be registered.
/// </summary>
internal sealed class StubSecretStore : ISecretStore
{
    public static StubSecretStore Instance { get; } = new();

    public Task<SecretValue> GetSecretAsync(SecretIdentifier identifier, CancellationToken cancellationToken = default)
        => throw new NotImplementedException("Stub - not intended for runtime use");

    public Task<VaultResult<SecretValue>> TryGetSecretAsync(SecretIdentifier identifier, CancellationToken cancellationToken = default)
        => throw new NotImplementedException("Stub - not intended for runtime use");

    public Task<IReadOnlyList<SecretVersion>> ListSecretVersionsAsync(string secretName, CancellationToken cancellationToken = default)
        => throw new NotImplementedException("Stub - not intended for runtime use");
}
