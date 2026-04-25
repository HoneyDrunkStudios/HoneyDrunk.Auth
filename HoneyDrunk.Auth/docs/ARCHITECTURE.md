# 🏛️ Architecture

[← Back to File Guide](FILE_GUIDE.md)

---

## Table of Contents

- [Overview](#overview)
- [Design Principles](#design-principles)
- [Layering](#layering)
- [Dependency Flow](#dependency-flow)
- [Kernel Integration](#kernel-integration)
- [Vault Integration](#vault-integration)
- [Authorization Evaluation Model](#authorization-evaluation-model)
- [Identity Model](#identity-model)
- [Request Flow](#request-flow)
- [Intentionally Out of Scope](#intentionally-out-of-scope)

---

## Overview

HoneyDrunk.Auth provides authentication and authorization services for HoneyDrunk Grid nodes. It is designed as a minimal, stateless engine that validates JWT Bearer tokens and evaluates authorization policies.

**Key Characteristics:**
- **Grid node component** - Auth is not a standalone library; it requires Kernel as its host runtime
- **Validation only** - Validates tokens and evaluates policies; does not issue tokens or manage subjects
- **Stateless** - No local state; designed for horizontal scaling
- **Vault-backed** - All secrets retrieved from Vault; no local key storage

---

## Design Principles

### 1. Minimal and Deterministic

Auth provides predictable behavior with no magic. Given the same inputs (token, signing keys, policy), the output is always the same.

### 2. Kernel is Required

Kernel is a **required host runtime dependency**, not an optional integration. Auth is a Grid node component that relies on Kernel for:
- Telemetry and activity tracing
- Lifecycle hooks (startup, health, readiness)
- Grid context propagation

### 3. Vault-Backed Secrets

All signing keys and configuration are retrieved from Vault:
- No local key storage
- Configuration changes don't require redeployment
- Key rotation handled transparently

### 4. Authorization is Local and Deterministic

Authorization evaluation must be:
- **Local** - No external calls during evaluation
- **Deterministic** - Same inputs produce same outputs
- **Side-effect free** - No state changes, no I/O

This constraint ensures authorization is fast, predictable, and auditable.

### 5. Fail-Fast Startup

Auth validates all required secrets at startup. If validation fails, the node does not start and does not accept traffic.

[↑ Back to top](#table-of-contents)

---

## Layering

### Layer 1: HoneyDrunk.Auth.Abstractions

**Purpose**: Pure contracts and models with zero dependencies.

**Contains**:
- `IAuthenticationProvider` - Contract for authenticating credentials
- `IAuthorizationPolicy` - Contract for evaluating authorization requests
- `AuthCredential` - Credential representation (Bearer token)
- `AuthenticatedIdentity` - Validated subject identity with claims
- `AuthenticationResult` / `AuthorizationDecision` - Operation results
- Failure/deny code enums for stable API responses

**Rules**:
- ❌ No Kernel references
- ❌ No Vault references
- ❌ No ASP.NET references
- ❌ No DI registrations
- ✅ Can be referenced by any project for contract definitions

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
- HoneyDrunk.Kernel (required - context, telemetry, lifecycle)
- HoneyDrunk.Vault (required - secrets)
- Microsoft.IdentityModel.JsonWebTokens (JWT handling)

**Rules**:
- ✅ Requires Kernel host runtime
- ✅ Directly accesses Vault for secrets
- ❌ No ASP.NET references

### Layer 3: HoneyDrunk.Auth.AspNetCore

**Purpose**: ASP.NET Core integration bridging HTTP to Auth runtime.

**Contains**:
- `HoneyDrunkAuthMiddleware` - Reads Authorization header, validates tokens
- `IAuthenticatedIdentityAccessor` - Access current identity from HttpContext
- `HttpContextIdentityAccessor` - HttpContext-based implementation
- Authorization endpoint helpers
- DI and pipeline extensions

**Dependencies**:
- HoneyDrunk.Auth (Auth runtime)
- HoneyDrunk.Auth.Abstractions (contracts)
- HoneyDrunk.Kernel (Grid context)
- Microsoft.AspNetCore.App (framework)

**Rules**:
- ❌ No direct Vault references (uses Auth core)
- ✅ Bridges HTTP concerns to Auth abstractions

[↑ Back to top](#table-of-contents)

---

## Dependency Flow

```
┌─────────────────────────────────────────────────────────────────────┐
│                        External Dependencies                         │
├─────────────────────────────────────────────────────────────────────┤
│  HoneyDrunk.Kernel    HoneyDrunk.Vault    Microsoft.IdentityModel   │
│  (required runtime)   (required secrets)   (JWT handling)           │
└─────────────────────────────────────────────────────────────────────┘
                              ▲
                              │
┌─────────────────────────────┴───────────────────────────────────────┐
│                     HoneyDrunk.Auth (Layer 2)                        │
│  Runtime implementation: JWT validation, policy evaluation,          │
│  Vault integration, lifecycle hooks                                  │
└─────────────────────────────┬───────────────────────────────────────┘
                              │
         ┌────────────────────┴────────────────────┐
         │                                         │
         ▼                                         ▼
┌─────────────────────────┐           ┌─────────────────────────────┐
│ HoneyDrunk.Auth         │           │ HoneyDrunk.Auth.AspNetCore  │
│ .Abstractions (Layer 1) │           │ (Layer 3)                   │
│                         │◄──────────│                             │
│ Pure contracts          │           │ ASP.NET Core integration    │
│ Zero dependencies       │           │ Middleware, DI extensions   │
└─────────────────────────┘           └─────────────────────────────┘
         ▲
         │
┌────────┴────────┐
│ Domain Projects │
│ (contracts only)│
└─────────────────┘
```

**Key Points:**
- Abstractions can be referenced without pulling in runtime dependencies
- Runtime requires both Kernel and Vault
- ASP.NET Core layer never directly accesses Vault
- Domain projects can reference Abstractions for contract definitions

[↑ Back to top](#table-of-contents)

---

## Kernel Integration

### Kernel as Required Runtime

Auth is not a drop-in JWT library. It is a Grid node component that requires Kernel as its host runtime.

**Why Kernel is Required:**
- Telemetry activities for authentication/authorization tracing
- Lifecycle hooks for startup validation and health reporting
- Grid context propagation for correlation and tenant context

### Context Propagation

Auth captures correlation identifiers from Kernel's operation context when authenticating/authorizing:

```
GridContext
├── CorrelationId    → Attached to telemetry activities
├── CausationId      → Attached to telemetry activities
├── TenantId         → Available for authorization decisions
└── ProjectId        → Available for authorization decisions
```

**Access Pattern:**
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

#### Startup Validation Invariants

The startup hook enforces the following invariants before the node accepts traffic:

| Invariant | Requirement |
|-----------|-------------|
| **Signing Keys** | At least one active signing key must be available from Vault |
| **Issuer** | The `Auth:Issuer` App Configuration value must be present and non-empty |
| **Audience** | The `Auth:Audience` App Configuration value must be present and non-empty |
| **Failure Behavior** | If any invariant fails, the startup hook throws, preventing the node from starting |
| **Empty Data** | If Vault is reachable but returns empty data, startup fails (empty is not valid) |

[↑ Back to top](#table-of-contents)

---

## Vault Integration

### Secret Access Pattern

Only `HoneyDrunk.Auth` directly accesses Vault. The ASP.NET Core layer uses Auth core abstractions.

```csharp
// VaultSigningKeyProvider uses:
ISecretStore.TryGetSecretAsync(new SecretIdentifier("Jwt--SigningKeys"), ct);
configuration["Auth:Issuer"];
configuration["Auth:Audience"];
configuration.GetValue("Auth:ClockSkewSeconds", 300);
```

### Required Secrets

| Secret | Path | Required | Description |
|--------|------|----------|-------------|
| Signing Keys | `Jwt--SigningKeys` | ✅ Yes | Array of signing key objects |
| Issuer | `Auth:Issuer` | ✅ Yes | Token issuer URI |
| Audience | `Auth:Audience` | ✅ Yes | Expected audience |
| Clock Skew | `Auth:ClockSkewSeconds` | ❌ No | Token time tolerance (default: 300) |

### Signing Key Structure

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

### Key Rotation

Multiple signing keys can be active simultaneously:

1. **Add new key** - Add new key to `Jwt--SigningKeys` with `active: true`
2. **Start signing** - Begin signing new tokens with new key
3. **Grace period** - Old tokens continue to validate until expiration
4. **Remove old key** - Remove old key from array when no longer needed

**Why This Works:**
- Token validation tries all active keys
- No downtime during rotation
- Old tokens remain valid until natural expiration

[↑ Back to top](#table-of-contents)

---

## Authorization Evaluation Model

### Evaluation Constraints

Authorization evaluation must be:

| Constraint | Description |
|------------|-------------|
| **Local** | No external calls (no network, no database, no other services) |
| **Deterministic** | Same inputs always produce same outputs |
| **Side-effect free** | No state changes, no logging side-effects, no I/O |

**Why These Constraints:**
- Fast evaluation (microseconds, not milliseconds)
- Predictable behavior for testing and debugging
- Auditable decisions (inputs fully determine outputs)
- No hidden dependencies that could fail

### Evaluation Semantics

| Requirement Type | Semantics |
|------------------|-----------|
| **Scopes** | ALL required scopes must be present |
| **Roles** | ANY required role is sufficient |
| **Ownership** | Subject ID must match resource owner ID |

**Example:**
```csharp
var request = new AuthorizationRequest(
    action: "write",
    resource: "documents/123",
    requiredScopes: ["documents:read", "documents:write"],  // ALL required
    requiredRoles: ["admin", "editor"]);                    // ANY sufficient
```

- If identity has scopes `["documents:read"]` → **Denied** (missing `documents:write`)
- If identity has roles `["editor"]` → **Allowed** (has one of the required roles)
- If identity has roles `["viewer"]` → **Denied** (has none of the required roles)

[↑ Back to top](#table-of-contents)

---

## Identity Model

### Subject-Based Identity

Auth validates **subjects**, not users. Subjects may be:

| Subject Type | Description |
|--------------|-------------|
| **Human Users** | Interactive users authenticated via browser/app |
| **Services** | Backend services with service accounts |
| **Agents** | Automated agents performing tasks |
| **Nodes** | Grid nodes communicating with each other |

**Why This Matters:**
- HoneyDrunk Grid has service-to-service and agent communication
- Identity model must not assume human interaction patterns
- Claims and policies work identically regardless of subject type

### Identity Claims

The `AuthenticatedIdentity` carries claims extracted from the validated token:

| Claim | Description |
|-------|-------------|
| `sub` | Subject identifier (required) |
| `name` | Display name (optional) |
| `role` | Role assignments (multi-valued) |
| `scope` | Granted scopes (multi-valued) |
| `tenant_id` | Tenant context |
| `project_id` | Project context |

### Identity Independence

`AuthenticatedIdentity` is independent of ASP.NET Core's `ClaimsPrincipal`:
- Auth core has no ASP.NET dependencies
- ASP.NET Core layer bridges to `HttpContext.User` for compatibility
- Domain code should use `IAuthenticatedIdentityAccessor`, not `HttpContext.User`

[↑ Back to top](#table-of-contents)

---

## Request Flow

### Full Pipeline

```
HTTP Request
     │
     ▼
┌─────────────────────────┐
│ GridContextMiddleware   │  ← Establishes Grid/Operation context
│                         │  ← Sets CorrelationId, CausationId
└────────────┬────────────┘
             │
             ▼
┌─────────────────────────┐
│ HoneyDrunkAuthMiddleware│  ← Reads Authorization header
│                         │  ← Extracts Bearer token
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

### Authentication Flow

```
Authorization Header          BearerTokenAuthenticationProvider
        │                                   │
        ▼                                   │
"Bearer eyJhbGci..."                        │
        │                                   │
        ▼                                   ▼
┌───────────────┐                  ┌─────────────────┐
│ Extract Token │ ──────────────►  │ Validate JWT    │
└───────────────┘                  │ • Signature     │
                                   │ • Expiration    │
                                   │ • Issuer        │
                                   │ • Audience      │
                                   └────────┬────────┘
                                            │
                                            ▼
                                   ┌─────────────────┐
                                   │ Extract Claims  │
                                   │ Build Identity  │
                                   └────────┬────────┘
                                            │
                                            ▼
                                   AuthenticationResult
                                   (Success + Identity)
                                   or (Fail + Code)
```

### Authorization Flow

```
AuthorizationRequest              DefaultAuthorizationPolicy
        │                                   │
        ▼                                   ▼
┌───────────────┐                  ┌─────────────────┐
│ action        │                  │ Check Scopes    │
│ resource      │ ──────────────►  │ (ALL required)  │
│ requiredScopes│                  └────────┬────────┘
│ requiredRoles │                           │
│ resourceOwner │                           ▼
└───────────────┘                  ┌─────────────────┐
                                   │ Check Roles     │
                                   │ (ANY sufficient)│
                                   └────────┬────────┘
                                            │
                                            ▼
                                   ┌─────────────────┐
                                   │ Check Ownership │
                                   │ (if specified)  │
                                   └────────┬────────┘
                                            │
                                            ▼
                                   AuthorizationDecision
                                   (Allow + Satisfied)
                                   or (Deny + Reasons)
```

[↑ Back to top](#table-of-contents)

---

## Intentionally Out of Scope

Auth is deliberately constrained to validation. The following are **not** Auth's responsibility:

| Out of Scope | Rationale |
|--------------|-----------|
| **User management** | Auth validates subjects, not manages them. Subject lifecycle is an external concern. |
| **Token issuance** | Auth validates tokens, not creates them. Token issuance is an Identity Provider concern. |
| **Sessions/persistence** | Auth is stateless. Session management is an application concern. |
| **Refresh tokens** | Not handled. Token refresh is negotiated with the Identity Provider. |
| **OAuth flows** | Auth receives tokens, not negotiates them. OAuth is an Identity Provider concern. |
| **Data/Transport** | Auth has no persistence or messaging dependencies. |

**Why These Boundaries:**
- Clear responsibility separation
- No accidental scope creep
- Horizontal scaling without coordination
- Simple mental model for consumers

---

## Summary

HoneyDrunk.Auth is a minimal, deterministic authentication and authorization engine for Grid nodes.

**Key Takeaways:**
- Auth is a Grid node component requiring Kernel as its host runtime
- Identity represents subjects (services, agents, nodes, humans), not just users
- Authorization evaluation is local, deterministic, and side-effect free
- Startup validation enforces strict invariants before accepting traffic
- All secrets come from Vault with support for key rotation

---

[← Back to File Guide](FILE_GUIDE.md) | [↑ Back to top](#table-of-contents)
