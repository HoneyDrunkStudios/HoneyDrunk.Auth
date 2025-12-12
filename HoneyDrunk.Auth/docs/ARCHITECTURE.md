# Architecture

## Overview

HoneyDrunk.Auth provides authentication and authorization services for HoneyDrunk Grid nodes. It is designed as a minimal, stateless engine that validates JWT Bearer tokens and evaluates authorization policies.

## Layering

### Layer 1: HoneyDrunk.Auth.Abstractions

**Purpose**: Pure contracts and models with zero dependencies.

**Contains**:
- `IAuthenticationProvider` - Contract for authenticating credentials
- `IAuthorizationPolicy` - Contract for evaluating authorization requests
- `AuthCredential` - Credential representation (Bearer token)
- `AuthenticatedIdentity` - Validated identity with claims
- `AuthenticationResult` / `AuthorizationDecision` - Operation results
- Failure/deny code enums for stable API responses

**Rules**:
- ❌ No Kernel references
- ❌ No Vault references
- ❌ No ASP.NET references
- ❌ No DI registrations

### Layer 2: HoneyDrunk.Auth

**Purpose**: Core runtime implementation with Kernel and Vault integration.

**Contains**:
- `BearerTokenAuthenticationProvider` - JWT validation using Vault signing keys
- `DefaultAuthorizationPolicy` - Scope, role, and ownership evaluation
- `VaultSigningKeyProvider` - Retrieves signing keys from Vault
- `AuthStartupHook` - Fail-fast validation at startup
- `AuthHealthContributor` / `AuthReadinessContributor` - Health/readiness checks
- DI registration extensions

**Dependencies**:
- HoneyDrunk.Kernel (context, telemetry, lifecycle)
- HoneyDrunk.Vault (secrets)
- Microsoft.IdentityModel.JsonWebTokens (JWT handling)

### Layer 3: HoneyDrunk.Auth.AspNetCore

**Purpose**: ASP.NET Core integration bridging HTTP to Auth runtime.

**Contains**:
- `HoneyDrunkAuthMiddleware` - Reads Authorization header, validates tokens
- `IAuthenticatedIdentityAccessor` - Access current identity from HttpContext
- Authorization endpoint helpers
- DI and pipeline extensions

**Rules**:
- ❌ No direct Vault references (uses Auth core)

## Kernel Integration

### Context Propagation

Auth captures correlation identifiers from Kernel's operation context when authenticating/authorizing:

```
GridContext
├── CorrelationId    → Attached to telemetry activities
├── CausationId      → Attached to telemetry activities
├── TenantId         → Available for authorization decisions
└── ProjectId        → Available for authorization decisions
```

Access pattern:
```csharp
IGridContextAccessor gridAccessor;
var context = gridAccessor.GridContext;
var correlationId = context?.CorrelationId;
```

### Telemetry

Auth emits telemetry using Kernel's `ITelemetryActivityFactory`:

| Activity | Tags |
|----------|------|
| `authn.validate` | `auth.scheme`, `auth.result`, `auth.failure_code` |
| `authz.evaluate` | `authz.result`, `authz.policy`, `authz.failure_code` |

### Lifecycle

Auth registers with Kernel's lifecycle system:

| Hook | Purpose |
|------|---------|
| `IStartupHook` | Validate Vault secrets exist at startup (fail-fast) |
| `IHealthContributor` | Report Auth system health |
| `IReadinessContributor` | Report Auth readiness for traffic |

## Vault Integration

### Secret Access Pattern

Only `HoneyDrunk.Auth` directly accesses Vault. The ASP.NET Core layer uses Auth core abstractions.

```csharp
// VaultSigningKeyProvider uses:
ISecretStore.TryGetSecretAsync(new SecretIdentifier("auth:signing_keys"), ct);
IVaultClient.GetConfigValueAsync("auth:issuer", ct);
IVaultClient.GetConfigValueAsync("auth:audience", ct);
IVaultClient.TryGetConfigValueAsync("auth:clock_skew_seconds", 300, ct);
```

### Key Rotation

Multiple signing keys can be active simultaneously:
1. Add new key to `auth:signing_keys` with `active: true`
2. Start signing new tokens with new key
3. Old tokens continue to validate until expiration
4. Remove old key from array when no longer needed

## Intentionally Out of Scope

- **User management**: Auth validates identities, not manages them
- **Token issuance**: Auth validates tokens, not creates them
- **Sessions/persistence**: Auth is stateless
- **Refresh tokens**: Not handled (external concern)
- **OAuth flows**: Auth receives tokens, not negotiates them
- **Data/Transport dependencies**: Auth has no persistence or messaging

## Request Flow

```
HTTP Request
     │
     ▼
┌─────────────────────────┐
│ GridContextMiddleware   │  ← Establishes Grid/Operation context
└────────────┬────────────┘
             │
             ▼
┌─────────────────────────┐
│ HoneyDrunkAuthMiddleware│  ← Reads Authorization header
│                         │  ← Calls IAuthenticationProvider
│                         │  ← Sets identity in HttpContext
└────────────┬────────────┘
             │
             ▼
┌─────────────────────────┐
│ Your Endpoint           │  ← Access via IAuthenticatedIdentityAccessor
│                         │  ← Call IAuthorizationPolicy if needed
└─────────────────────────┘
```
