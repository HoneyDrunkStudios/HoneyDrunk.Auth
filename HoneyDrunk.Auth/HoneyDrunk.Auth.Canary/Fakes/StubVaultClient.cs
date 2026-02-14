using HoneyDrunk.Vault.Abstractions;
using HoneyDrunk.Vault.Models;

namespace HoneyDrunk.Auth.Canary.Fakes;

/// <summary>
/// Minimal stub IVaultClient that satisfies registration-time guards.
/// Does not need to actually work - just needs to be registered.
/// </summary>
internal sealed class StubVaultClient : IVaultClient
{
    public static StubVaultClient Instance { get; } = new();

    public Task<SecretValue> GetSecretAsync(SecretIdentifier identifier, CancellationToken cancellationToken = default)
        => throw new NotImplementedException("Stub - not intended for runtime use");

    public Task<VaultResult<SecretValue>> TryGetSecretAsync(SecretIdentifier identifier, CancellationToken cancellationToken = default)
        => throw new NotImplementedException("Stub - not intended for runtime use");

    public Task<string> GetConfigValueAsync(string key, CancellationToken cancellationToken = default)
        => throw new NotImplementedException("Stub - not intended for runtime use");

    public Task<T> GetConfigValueAsync<T>(string key, CancellationToken cancellationToken = default)
        => throw new NotImplementedException("Stub - not intended for runtime use");

    public Task<string?> TryGetConfigValueAsync(string key, CancellationToken cancellationToken = default)
        => throw new NotImplementedException("Stub - not intended for runtime use");

    public Task<T> TryGetConfigValueAsync<T>(string key, T defaultValue, CancellationToken cancellationToken = default)
        => throw new NotImplementedException("Stub - not intended for runtime use");

    public Task<IReadOnlyList<SecretVersion>> ListSecretVersionsAsync(string secretPath, CancellationToken cancellationToken = default)
        => throw new NotImplementedException("Stub - not intended for runtime use");
}
