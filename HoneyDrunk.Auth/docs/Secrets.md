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

Secret management components for retrieving signing keys from HoneyDrunk.Vault and non-secret token validation settings from App Configuration. The Auth system never stores secrets locally.

**Location:** `HoneyDrunk.Auth/Secrets/`

**Secrets and Configuration Used:**

| Key | Source | Type | Required | Description |
|-----|--------|------|----------|-------------|
| `Jwt--SigningKeys` | Key Vault | JSON Array | Yes | Signing keys for token validation |
| `VaultInvalidationWebhookSecret` | Key Vault | String | Yes | Shared secret for Event Grid invalidation |
| `Auth:Issuer` | App Configuration | String | Yes | Expected JWT token issuer |
| `Auth:Audience` | App Configuration | String | Yes | Expected JWT token audience |
| `Auth:ClockSkewSeconds` | App Configuration | Integer | No | Clock skew tolerance (default: 300) |

---

## Runtime Behavior

### Startup Loading

Signing keys and configuration are loaded **once at startup** by the `AuthStartupHook`. This ensures:

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
        IConfiguration configuration,
        ILogger<VaultSigningKeyProvider> logger);
    
    // ISigningKeyProvider implementation
}
```

### Purpose

Vault-backed implementation of `ISigningKeyProvider`. Retrieves signing keys from HoneyDrunk.Vault and non-secret validation settings from App Configuration.

### Dependencies

| Dependency | Purpose |
|------------|---------|
| `ISecretStore` | Retrieves secret values from Vault |
| `IConfiguration` | Retrieves non-secret App Configuration values |
| `ILogger<T>` | Logs key retrieval operations |

### Key Mappings

```csharp
private const string IssuerKey = "Auth:Issuer";
private const string AudienceKey = "Auth:Audience";
private const string SigningKeysKey = "Jwt--SigningKeys";
private const string ClockSkewKey = "Auth:ClockSkewSeconds";
private const int DefaultClockSkewSeconds = 300;
```

### Key Parsing

The provider parses the `Jwt--SigningKeys` JSON array:

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

### Azure Setup Example

```bash
# Key Vault secrets
az keyvault secret set --vault-name kv-hd-auth-dev --name Jwt--SigningKeys --value '[{"kid":"key-1","alg":"HS256","key":"base64key==","active":true}]'
az keyvault secret set --vault-name kv-hd-auth-dev --name VaultInvalidationWebhookSecret --value "<shared-webhook-secret>"

# Shared App Configuration values under the Auth label
az appconfig kv set --name appcs-hd-shared-dev --key Auth:Issuer --label honeydrunk-auth --value "https://auth.honeydrunk.io"
az appconfig kv set --name appcs-hd-shared-dev --key Auth:Audience --label honeydrunk-auth --value "honeydrunk-grid"
az appconfig kv set --name appcs-hd-shared-dev --key Auth:ClockSkewSeconds --label honeydrunk-auth --value "300"
```

### Development/Testing

For local development without Vault, you can implement a mock provider:

> ⚠️ **Warning:** Development providers are **escape hatches for local testing only**. They must never be used in production, staging, or any shared environment. Never commit hardcoded key material to source control.

```csharp
public class DevelopmentSigningKeyProvider : ISigningKeyProvider
{
    // WARNING: Local development only - never use in production
    private readonly SymmetricSecurityKey _devKey = new(
        Encoding.UTF8.GetBytes("REPLACE-WITH-YOUR-DEV-KEY-32-BYTES-MINIMUM"));
    
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
