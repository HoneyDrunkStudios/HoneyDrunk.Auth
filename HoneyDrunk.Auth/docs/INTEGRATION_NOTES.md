# Integration Notes: HoneyDrunk.Kernel and HoneyDrunk.Vault APIs

This document lists the exact types and methods from HoneyDrunk.Kernel and HoneyDrunk.Vault that Auth integrates with.

## HoneyDrunk.Kernel Integration Points

### Context Access

| Type | Usage |
|------|-------|
| `IGridContextAccessor` | Access ambient Grid context containing correlation IDs, tenant/project identifiers |
| `IGridContext` | Read `CorrelationId`, `CausationId`, `TenantId`, `ProjectId`, `Baggage` |
| `IOperationContextAccessor` | Access ambient Operation context for current request/operation |
| `IOperationContext` | Read `OperationName`, `OperationId`, `GridContext`; call `AddMetadata()` |
| `INodeContext` | Read Node-level identifiers (`NodeId`, `StudioId`, `Environment`) |

### Telemetry

| Type | Method | Usage |
|------|--------|-------|
| `ITelemetryActivityFactory` | `Start(name, tags)` | Start telemetry activities with ambient context |
| `ITelemetryActivityFactory` | `StartExplicit(name, gridContext, opContext, tags)` | Start activities with explicit context |
| `HoneyDrunkTelemetry` | `StartActivity(name, grid, operation, enrichers, tags)` | Static helper for starting activities |
| `GridActivitySource` | `Instance` | Direct `ActivitySource` access for custom activities |
| `GridActivitySource` | `RecordException(activity, exception)` | Record exceptions on activities |
| `GridActivitySource` | `SetSuccess(activity)` | Mark activity as successful |
| `ITraceEnricher` | `Enrich(telemetryContext, tags)` | Custom trace enrichment |

### Lifecycle / Health / Readiness

| Type | Method | Usage |
|------|--------|-------|
| `IHealthContributor` | `CheckHealthAsync(ct)` | Implement health check for Auth secrets availability |
| `IReadinessContributor` | `CheckReadinessAsync(ct)` | Implement readiness check for Auth startup validation |
| `IStartupHook` | `ExecuteAsync(ct)` | Run startup validation (verify Vault secrets exist) |
| `HealthStatus` | Enum values | Return `Healthy`, `Degraded`, `Unhealthy` |
| `ReadinessStatus` | Enum values | Return `Ready`, `NotReady` |

### DI / Hosting Extensions

| Type | Method | Usage |
|------|--------|-------|
| `IHoneyDrunkBuilder` | Extension point | Chain Auth registration with `.AddAuth()` |
| `AddStartupHook<T>()` | Extension method | Register Auth startup validation hook |

## HoneyDrunk.Vault Integration Points

### Secret Access

| Type | Method | Usage |
|------|--------|-------|
| `ISecretStore` | `GetSecretAsync(SecretIdentifier, ct)` | Retrieve secret by identifier (throws if not found) |
| `ISecretStore` | `TryGetSecretAsync(SecretIdentifier, ct)` | Retrieve secret with `VaultResult<SecretValue>` |
| `ISecretStore` | `ListSecretVersionsAsync(name, ct)` | List versions for key rotation support |
| `IVaultClient` | `GetConfigValueAsync(key, ct)` | Read configuration values |
| `IVaultClient` | `TryGetConfigValueAsync(key, ct)` | Read config with null fallback |
| `IVaultClient` | `GetConfigValueAsync<T>(key, ct)` | Read typed configuration |
| `IVaultClient` | `TryGetConfigValueAsync<T>(key, default, ct)` | Read typed config with default |

### Models

| Type | Usage |
|------|-------|
| `SecretIdentifier` | Construct with `new SecretIdentifier(name)` or `new SecretIdentifier(name, version)` |
| `SecretValue` | Contains `Value` (the secret string), `Version`, `Identifier` |
| `VaultResult<T>` | Contains `IsSuccess`, `Value`, `ErrorMessage` |

### Exceptions

| Type | Usage |
|------|-------|
| `SecretNotFoundException` | Thrown when required secret is missing |
| `ConfigurationNotFoundException` | Thrown when required config is missing |
| `VaultOperationException` | General vault operation failure |

## Required Vault Secret Keys

Auth expects the following secrets in Vault:

| Key | Type | Description |
|-----|------|-------------|
| `auth:issuer` | String | JWT token issuer (e.g., `https://auth.honeydrunk.io`) |
| `auth:audience` | String | JWT token audience (e.g., `api://honeydrunk`) |
| `auth:signing_keys` | JSON Array | Array of signing key objects (see format below) |
| `auth:clock_skew_seconds` | Integer (optional) | Token validation clock skew tolerance (default: 300) |

### Signing Keys Format

```json
[
  {
    "kid": "key-2024-01",
    "alg": "HS256",
    "key": "base64-encoded-key-material",
    "active": true
  },
  {
    "kid": "key-2023-12",
    "alg": "HS256",
    "key": "base64-encoded-key-material",
    "active": true
  }
]
```

**Key Rotation Support**: Multiple keys with `active: true` are used for validation, allowing gradual key rotation. All active keys are tried when validating signatures.
