# 🔑 Secrets - Vault Integration and Signing Keys

[← Back to File Guide](FILE_GUIDE.md)

---

## Table of Contents

- [Overview](#overview)
- [Runtime Behavior](#runtime-behavior)
- [ISigningKeyProvider.cs](#isigningkeyprovidercs)
- [SigningKeyInfo.cs](#signingkeyinfocs)
- [VaultSigningKeyProvider.cs](#vaultsigningkeyprovidercs)

---

## Overview

Secret management components for retrieving signing keys and configuration from HoneyDrunk.Vault. The Auth system never stores secrets locally - all sensitive configuration comes from Vault.

**Location:** `HoneyDrunk.Auth/Secrets/`

**Vault Secrets Used:**

| Key | Type | Required | Description |
|-----|------|----------|-------------|
| `auth:issuer` | String | ✅ Yes | Expected JWT token issuer |
| `auth:audience` | String | ✅ Yes | Expected JWT token audience |
| `auth:signing_keys` | JSON Array | ✅ Yes | Signing keys for token validation |
| `auth:clock_skew_seconds` | Integer | ❌ No | Clock skew tolerance (default: 300) |

---

## Runtime Behavior

### Startup Loading

Signing keys and configuration are loaded from Vault **once at startup** by the `AuthStartupHook`. This ensures:

- **Fail-fast validation** - Missing or invalid secrets prevent the node from starting
- **No request-time I/O** - Authentication never calls Vault during request processing
- **Predictable performance** - Token validation is purely in-memory

```
Startup                          Request Time
   │                                  │
   ▼                                  ▼
┌─────────────────┐           ┌─────────────────┐
│ AuthStartupHook │           │ Authentication  │
│                 │           │                 │
│ Load secrets    │           │ Use cached keys │
│ Validate keys   │           │ (no Vault I/O)  │
│ Cache in memory │           │                 │
└─────────────────┘           └─────────────────┘
```

> **Invariant:** Authentication must not perform Vault I/O. All secrets are loaded during startup and reused for request-time validation.

### Supported Algorithms

The Auth system currently supports **symmetric signing algorithms only**:

| Algorithm | Supported | Key Type |
|-----------|-----------|----------|
| `HS256` | ✅ Yes (default) | Symmetric |
| `HS384` | ✅ Yes | Symmetric |
| `HS512` | ✅ Yes | Symmetric |
| `RS256` | ❌ No | Asymmetric |
| `ES256` | ❌ No | Asymmetric |

> **Why symmetric only?** Symmetric keys are simpler to manage, rotate, and store in Vault. Asymmetric key support may be added in the future if required by identity provider integrations.

[↑ Back to top](#table-of-contents)

---

## ISigningKeyProvider.cs

```csharp
public interface ISigningKeyProvider
{
    Task<IReadOnlyList<SecurityKey>> GetSigningKeysAsync(
        CancellationToken cancellationToken = default);
    
    Task<string> GetIssuerAsync(
        CancellationToken cancellationToken = default);
    
    Task<string> GetAudienceAsync(
        CancellationToken cancellationToken = default);
    
    Task<TimeSpan> GetClockSkewAsync(
        CancellationToken cancellationToken = default);
}
```

### Purpose

Provides signing keys and configuration for token validation. This abstraction allows for different secret sources (Vault, Azure Key Vault, local development, etc.).

### Methods

| Method | Returns | Description |
|--------|---------|-------------|
| `GetSigningKeysAsync` | `IReadOnlyList<SecurityKey>` | Active signing keys for validation |
| `GetIssuerAsync` | `string` | Expected token issuer |
| `GetAudienceAsync` | `string` | Expected token audience |
| `GetClockSkewAsync` | `TimeSpan` | Clock skew tolerance |

### Usage Example

```csharp
public class TokenValidator(ISigningKeyProvider keyProvider)
{
    public async Task<TokenValidationParameters> BuildParametersAsync(
        CancellationToken ct)
    {
        return new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = await keyProvider.GetIssuerAsync(ct),
            ValidateAudience = true,
            ValidAudience = await keyProvider.GetAudienceAsync(ct),
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKeys = await keyProvider.GetSigningKeysAsync(ct),
            ClockSkew = await keyProvider.GetClockSkewAsync(ct)
        };
    }
}
```

[↑ Back to top](#table-of-contents)

---

## SigningKeyInfo.cs

```csharp
public sealed record SigningKeyInfo(
    string KeyId,
    string Algorithm,
    string KeyMaterial,
    bool IsActive);
```

### Purpose

Represents a signing key retrieved from Vault. Used internally to parse and validate key data before creating `SecurityKey` instances.

### Properties

| Property | Type | Description |
|----------|------|-------------|
| `KeyId` | `string` | The key identifier (kid) for key matching |
| `Algorithm` | `string` | The signing algorithm (HS256, HS384, HS512) |
| `KeyMaterial` | `string` | Base64-encoded key bytes |
| `IsActive` | `bool` | Whether this key should be used for validation |

### Vault JSON Format

```json
[
  {
    "kid": "key-2024-01",
    "alg": "HS256",
    "key": "base64EncodedKeyMaterial==",
    "active": true
  },
  {
    "kid": "key-2023-12",
    "alg": "HS256",
    "key": "oldKeyMaterial==",
    "active": false
  }
]
```

### Key Rotation Support

Multiple keys can be stored in Vault to support key rotation:

1. Add new key with `active: true`
2. Old key remains `active: true` during transition
3. After token refresh cycle completes, set old key to `active: false`
4. Eventually remove old key from Vault

[↑ Back to top](#table-of-contents)

---

## VaultSigningKeyProvider.cs

```csharp
public sealed class VaultSigningKeyProvider : ISigningKeyProvider
{
    public VaultSigningKeyProvider(
        ISecretStore secretStore,
        IVaultClient vaultClient,
        ILogger<VaultSigningKeyProvider> logger);
    
    // ISigningKeyProvider implementation
}
```

### Purpose

Vault-backed implementation of `ISigningKeyProvider`. Retrieves signing keys and configuration from HoneyDrunk.Vault.

### Dependencies

| Dependency | Purpose |
|------------|---------|
| `ISecretStore` | Retrieves secret values from Vault |
| `IVaultClient` | Retrieves configuration values from Vault |
| `ILogger<T>` | Logs key retrieval operations |

### Vault Key Mappings

```csharp
private const string IssuerKey = "auth:issuer";
private const string AudienceKey = "auth:audience";
private const string SigningKeysKey = "auth:signing_keys";
private const string ClockSkewKey = "auth:clock_skew_seconds";
private const int DefaultClockSkewSeconds = 300;
```

### Key Parsing

The provider parses the `auth:signing_keys` JSON array:

```csharp
private List<SigningKeyInfo> ParseSigningKeys(string json)
{
    var keys = JsonSerializer.Deserialize<List<SigningKeyInfoDto>>(json);
    
    return keys
        .Where(k => !string.IsNullOrWhiteSpace(k.Kid) && !string.IsNullOrWhiteSpace(k.Key))
        .Select(k => new SigningKeyInfo(k.Kid!, k.Alg ?? "HS256", k.Key!, k.Active ?? true))
        .Where(IsValidBase64Key)
        .ToList();
}
```

### Security Key Creation

```csharp
private static SecurityKey CreateSecurityKey(SigningKeyInfo keyInfo)
{
    var keyBytes = Convert.FromBase64String(keyInfo.KeyMaterial);
    return new SymmetricSecurityKey(keyBytes) { KeyId = keyInfo.KeyId };
}
```

### Error Handling

The provider fails explicitly if secrets are missing or invalid:

```csharp
public async Task<IReadOnlyList<SecurityKey>> GetSigningKeysAsync(CancellationToken ct)
{
    var secretResult = await _secretStore.TryGetSecretAsync(
        new SecretIdentifier(SigningKeysKey), ct);

    if (!secretResult.IsSuccess || secretResult.Value is null)
    {
        _logger.LogError("Failed to retrieve signing keys from Vault: {Error}", 
            secretResult.ErrorMessage);
        throw new InvalidOperationException(
            $"Failed to retrieve signing keys from Vault: {secretResult.ErrorMessage}");
    }
    
    // Parse and return keys...
}
```

### Registration

```csharp
// Registered automatically via AddHoneyDrunkAuth()
services.AddHoneyDrunkAuth();

// Or register manually
services.AddSingleton<ISigningKeyProvider, VaultSigningKeyProvider>();
```

### Vault Setup Example

```bash
# Set issuer
vault kv put secret/auth issuer="https://auth.honeydrunk.io"

# Set audience
vault kv patch secret/auth audience="honeydrunk-grid"

# Set signing keys (JSON array)
vault kv patch secret/auth signing_keys='[{"kid":"key-1","alg":"HS256","key":"base64key==","active":true}]'

# Set optional clock skew
vault kv patch secret/auth clock_skew_seconds=300
```

### Development/Testing

For local development without Vault, you can implement a mock provider:

> ⚠️ **Warning:** Development providers are **escape hatches for local testing only**. They must never be used in production, staging, or any shared environment. Never commit hardcoded key material to source control.

```csharp
public class DevelopmentSigningKeyProvider : ISigningKeyProvider
{
    // WARNING: Local development only - never use in production
    private readonly SymmetricSecurityKey _devKey = new(
        Encoding.UTF8.GetBytes("development-signing-key-at-least-32-bytes!"));
    
    public Task<IReadOnlyList<SecurityKey>> GetSigningKeysAsync(CancellationToken ct)
        => Task.FromResult<IReadOnlyList<SecurityKey>>([_devKey]);
    
    public Task<string> GetIssuerAsync(CancellationToken ct)
        => Task.FromResult("dev-issuer");
    
    public Task<string> GetAudienceAsync(CancellationToken ct)
        => Task.FromResult("dev-audience");
    
    public Task<TimeSpan> GetClockSkewAsync(CancellationToken ct)
        => Task.FromResult(TimeSpan.FromMinutes(5));
}

// Register for local development ONLY
if (env.IsDevelopment())
{
    services.AddSingleton<ISigningKeyProvider, DevelopmentSigningKeyProvider>();
}
```

**When to use:**
- Local development without Vault access
- Unit tests that need predictable key material
- Integration tests in isolated environments

**Never use for:**
- Production deployments
- Staging or pre-production environments
- Any environment with real user data

[↑ Back to top](#table-of-contents)

---

## Summary

The Secrets components provide secure, centralized management of authentication configuration. By integrating with HoneyDrunk.Vault, the Auth system:

- **Never stores secrets locally** - All secrets come from Vault
- **Loads secrets at startup** - No Vault I/O during request processing
- **Supports key rotation** - Multiple keys with active/inactive states
- **Fails fast** - Missing secrets cause explicit errors at startup
- **Abstracts the source** - Different providers for production vs development

Key design decisions:
- **Interface-based** - `ISigningKeyProvider` allows different implementations
- **Symmetric keys only** - HS256/HS384/HS512 supported; asymmetric keys not currently supported
- **Record types** - `SigningKeyInfo` is immutable and simple
- **Explicit errors** - No silent fallbacks for missing configuration

---

[← Back to File Guide](FILE_GUIDE.md) | [↑ Back to top](#table-of-contents)
