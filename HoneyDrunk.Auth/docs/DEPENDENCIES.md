# Dependencies

## Runtime Dependencies by Project

### HoneyDrunk.Auth.Abstractions

| Dependency | Version | Purpose |
|------------|---------|---------|
| (none) | - | Pure contracts only |

### HoneyDrunk.Auth

| Dependency | Version | Purpose |
|------------|---------|---------|
| HoneyDrunk.Kernel | 0.3.0 | Context, telemetry, lifecycle |
| HoneyDrunk.Vault | 0.1.0 | Secret management |
| Microsoft.IdentityModel.JsonWebTokens | 8.3.1 | JWT validation |

### HoneyDrunk.Auth.AspNetCore

| Dependency | Version | Purpose |
|------------|---------|---------|
| HoneyDrunk.Kernel | 0.3.0 | Context, telemetry |
| Microsoft.AspNetCore.App | (framework) | ASP.NET Core integration |

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
- ❌ `HoneyDrunk.Auth.AspNetCore` must NOT reference `HoneyDrunk.Vault`
- ❌ `HoneyDrunk.Auth.Abstractions` must NOT reference `HoneyDrunk.Vault`

## Vault Secret Keys

Auth requires the following secrets in Vault:

### auth:issuer

**Type**: String
**Required**: Yes
**Description**: The expected JWT issuer claim value.

**Example**:
```
https://auth.honeydrunk.io
```

### auth:audience

**Type**: String
**Required**: Yes
**Description**: The expected JWT audience claim value.

**Example**:
```
api://honeydrunk
```

### auth:signing_keys

**Type**: JSON Array
**Required**: Yes (at least one active key)
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

### auth:clock_skew_seconds

**Type**: Integer
**Required**: No
**Default**: 300 (5 minutes)
**Description**: Tolerance for clock skew when validating token expiration.

**Example**:
```
300
```

## Startup Validation

At startup, Auth validates:

1. `auth:issuer` exists and is non-empty
2. `auth:audience` exists and is non-empty
3. `auth:signing_keys` contains at least one active key

If any validation fails, the Node will **fail to start** with a clear error message.

## Secret Access Patterns

Auth uses the following Vault APIs:

```csharp
// Get signing keys
ISecretStore.TryGetSecretAsync(new SecretIdentifier("auth:signing_keys"), ct);

// Get issuer
IVaultClient.GetConfigValueAsync("auth:issuer", ct);

// Get audience
IVaultClient.GetConfigValueAsync("auth:audience", ct);

// Get clock skew (with default)
IVaultClient.TryGetConfigValueAsync<int>("auth:clock_skew_seconds", 300, ct);
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
