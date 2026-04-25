# Dependencies

## Runtime Dependencies by Project

### HoneyDrunk.Auth.Abstractions

| Dependency | Version | Purpose |
|------------|---------|---------|
| (none) | - | Pure contracts only |

### HoneyDrunk.Auth

| Dependency | Version | Purpose |
|------------|---------|---------|
| HoneyDrunk.Kernel | 0.4.0 | Context, telemetry, lifecycle |
| HoneyDrunk.Vault | 0.3.0 | Secret management |
| HoneyDrunk.Vault.Providers.AzureKeyVault | 0.3.0 | Env-var-driven Key Vault bootstrap |
| HoneyDrunk.Vault.Providers.AppConfiguration | 0.3.0 | Env-var-driven App Configuration bootstrap |
| Microsoft.IdentityModel.JsonWebTokens | 8.17.0 | JWT validation |

### HoneyDrunk.Auth.AspNetCore

| Dependency | Version | Purpose |
|------------|---------|---------|
| HoneyDrunk.Kernel | 0.4.0 | Context, telemetry |
| Microsoft.AspNetCore.App | (framework) | ASP.NET Core integration |
| HoneyDrunk.Vault.EventGrid | 0.3.0 | Event Grid cache invalidation endpoint |

## Forbidden Dependencies

The following dependencies are **not allowed** in Auth projects:

| Package Pattern | Reason |
|-----------------|--------|
| `HoneyDrunk.Data.*` | Auth has no persistence |
| `HoneyDrunk.Transport.*` | Auth has no messaging |
| `EntityFramework*` | Auth has no database |
| `*Redis*` / `*Caching*` | Auth is stateless |
| `*OAuth*` / `*OpenIdConnect*` | Auth validates, not issues |

### Vault Constraint

- ✅ `HoneyDrunk.Auth` may reference `HoneyDrunk.Vault`
- ✅ `HoneyDrunk.Auth.AspNetCore` may reference `HoneyDrunk.Vault.EventGrid` for webhook routing
- ❌ `HoneyDrunk.Auth.Abstractions` must NOT reference `HoneyDrunk.Vault`

## Secret and Configuration Keys

Auth requires signing keys in Key Vault and non-secret token validation settings in App Configuration.

### Auth:Issuer

**Type**: String
**Required**: Yes
**Source**: App Configuration label `honeydrunk-auth`
**Description**: The expected JWT issuer claim value.

**Example**:
```
https://auth.honeydrunk.io
```

### Auth:Audience

**Type**: String
**Required**: Yes
**Source**: App Configuration label `honeydrunk-auth`
**Description**: The expected JWT audience claim value.

**Example**:
```
api://honeydrunk
```

### Jwt--SigningKeys

**Type**: JSON Array
**Required**: Yes (at least one active key)
**Source**: Auth Key Vault `kv-hd-auth-{env}`
**Description**: Array of signing key objects for token validation.

**Format**:
```json
[
  {
    "kid": "key-2024-01",
    "alg": "HS256",
    "key": "base64-encoded-256-bit-key",
    "active": true
  },
  {
    "kid": "key-2023-12",
    "alg": "HS256",
    "key": "base64-encoded-256-bit-key",
    "active": true
  }
]
```

**Field Descriptions**:

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `kid` | string | Yes | Key identifier (matches JWT header) |
| `alg` | string | No | Algorithm (default: HS256) |
| `key` | string | Yes | Base64-encoded key material |
| `active` | boolean | No | Whether key is active (default: true) |

**Key Rotation**:
- Multiple keys with `active: true` can coexist
- All active keys are tried when validating signatures
- Remove old keys after tokens expire (typically after max token lifetime)

### Auth:ClockSkewSeconds

**Type**: Integer
**Required**: No
**Default**: 300 (5 minutes)
**Source**: App Configuration label `honeydrunk-auth`
**Description**: Tolerance for clock skew when validating token expiration.

**Example**:
```
300
```

## Startup Validation

At startup, Auth validates:

1. `Auth:Issuer` exists and is non-empty
2. `Auth:Audience` exists and is non-empty
3. `Jwt--SigningKeys` contains at least one active key

If any validation fails, the Node will **fail to start** with a clear error message.

## Secret Access Patterns

Auth uses `ISecretStore` for secrets and `IConfiguration` for non-secret App Configuration values:

```csharp
// Get signing keys
ISecretStore.TryGetSecretAsync(new SecretIdentifier("Jwt--SigningKeys"), ct);

// Get issuer
configuration["Auth:Issuer"];

// Get audience
configuration["Auth:Audience"];

// Get clock skew (with default)
configuration.GetValue("Auth:ClockSkewSeconds", 300);
```

## Generating Signing Keys

To generate a 256-bit key for HS256:

```powershell
# PowerShell
$bytes = New-Object byte[] 32
[System.Security.Cryptography.RandomNumberGenerator]::Fill($bytes)
[Convert]::ToBase64String($bytes)
```

```bash
# Bash
openssl rand -base64 32
```

Store the output in the `key` field of your signing key configuration.
