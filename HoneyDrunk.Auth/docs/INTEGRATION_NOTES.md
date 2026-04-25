# Integration Notes: HoneyDrunk.Kernel and HoneyDrunk.Vault APIs

This document lists the exact types and methods from HoneyDrunk.Kernel (v0.4.0) and HoneyDrunk.Vault that Auth integrates with.

## HoneyDrunk.Kernel Integration Points (v0.4.0)

### Context Access

> **Important (v0.4.0):** GridContext is now created by DI scope and initialized by middleware.
> Auth **must not** create its own GridContext instances. Always access the ambient context via `IGridContextAccessor`.

| Type | Usage |
|------|-------|
| `IGridContextAccessor` | Access ambient Grid context (read-only in v0.4.0) |
| `IGridContext` | Read `CorrelationId`, `CausationId`, `TenantId`, `ProjectId`, `IsInitialized`; call `AddBaggage()` |
| `IOperationContextAccessor` | Access ambient Operation context for current request/operation |
| `IOperationContext` | Read `OperationName`, `OperationId`, `GridContext`; call `AddMetadata()` |
| `INodeContext` | Read Node-level identifiers (`NodeId`, `StudioId`, `Environment`) |

### v0.4.0 Breaking Changes

**IGridContextAccessor:**
- Changed from `IGridContext? GridContext { get; set; }` to `IGridContext GridContext { get; }` (read-only)
- Accessor reads from `HttpContext.RequestServices`, not independent `AsyncLocal`

**IGridContext:**
- Removed `BeginScope()` method entirely
- Removed `WithBaggage()` method - replaced with `AddBaggage()` (void, mutates in place)
- Added `IsInitialized` property to check initialization state
- Throws `InvalidOperationException` if accessed before initialization
- Throws `ObjectDisposedException` if accessed after scope ends

**IGridContextFactory:**
- Removed `CreateRoot()` method - root contexts are created by DI only
- `CreateChild()` remains for cross-node propagation scenarios

**Context Mappers (Now Static):**
- `HttpContextMapper` is now static with `ExtractFromHttpContext()` and `InitializeFromHttpContext()` methods
- Auth should never call these directly - use `UseGridContext()` middleware

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
| `Auth:Issuer` | String | JWT token issuer (e.g., `https://auth.honeydrunk.io`) |
| `Auth:Audience` | String | JWT token audience (e.g., `api://honeydrunk`) |
| `Jwt--SigningKeys` | JSON Array | Array of signing key objects (see format below) |
| `Auth:ClockSkewSeconds` | Integer (optional) | Token validation clock skew tolerance (default: 300) |

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
