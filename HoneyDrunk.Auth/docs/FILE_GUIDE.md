# 📦 HoneyDrunk.Auth - Complete File Guide

## Overview

**Think of this library as a security checkpoint for your application**

Just like how an airport security checkpoint verifies your identity (authentication) and checks if you're allowed to board a specific flight (authorization), this library validates Bearer tokens and evaluates access permissions. It provides a minimal, deterministic AuthN/AuthZ engine with Vault-backed secret management and Kernel integration for telemetry and lifecycle.

**Key Concepts:**
- **Authentication**: Validating "who you are" via JWT Bearer tokens
- **Authorization**: Evaluating "what you can do" via policies (scopes, roles, ownership)
- **Identity**: The authenticated subject representation with claims (subjects may be services, agents, nodes, or humans)
- **Credential**: The raw authentication input (e.g., Bearer token)
- **Vault Integration**: Secure retrieval of signing keys and configuration
- **Kernel Integration**: Telemetry, health checks, and lifecycle hooks (required runtime dependency)

---

## 📚 Documentation Structure

This guide is organized into focused documents by domain:

### 🏛️ Architecture

| Document | Description |
|----------|-------------|
| [Architecture](ARCHITECTURE.md) | **Dependency flow, layer responsibilities, and integration patterns** |

### 🔷 HoneyDrunk.Auth.Abstractions

| Domain | Document | Description |
|--------|----------|-------------|
| 📋 **Abstractions** | [Abstractions.md](Abstractions.md) | Core contracts and types (interfaces, identity, credentials, results, enums) |

### 🔷 HoneyDrunk.Auth (Core)

| Domain | Document | Description |
|--------|----------|-------------|
| 🔐 **Authentication** | [Authentication.md](Authentication.md) | JWT Bearer token validation (BearerTokenAuthenticationProvider) |
| 🛡️ **Authorization** | [Authorization.md](Authorization.md) | Policy-based authorization (DefaultAuthorizationPolicy) |
| 🔑 **Secrets** | [Secrets.md](Secrets.md) | Vault-backed signing key management (ISigningKeyProvider, VaultSigningKeyProvider) |
| ❤️ **Lifecycle** | [Lifecycle.md](Lifecycle.md) | Health, readiness, and startup hooks |
| 📈 **Telemetry** | [Telemetry.md](Telemetry.md) | OpenTelemetry activity constants |
| 🔌 **DI** | [DependencyInjection.md](DependencyInjection.md) | Service registration extensions |

### 🔸 HoneyDrunk.Auth.AspNetCore

| Document | Description |
|----------|-------------|
| [AspNetCore.md](AspNetCore.md) | ASP.NET Core middleware, identity accessor, and authorization helpers |

---

## 🔷 Quick Start

### Basic Concepts

**Authentication Flow:**

```
HTTP Request                    Auth Middleware                 Application
     ↓                              ↓                              ↓
Authorization: Bearer <token> → Extract Token → Validate JWT → Set Identity
     ↓                              ↓                              ↓
                              BearerTokenAuthenticationProvider    HttpContext.User
                              VaultSigningKeyProvider              IAuthenticatedIdentityAccessor
```

**Authorization Flow:**

```
Endpoint Handler              Authorization Policy              Decision
     ↓                              ↓                              ↓
AuthorizationRequest → Evaluate(identity, request) → Allow/Deny
     ↓                              ↓                              ↓
action: "write"              Check scopes/roles/ownership    IsAllowed: true
resource: "projects"                                          DenyReasons: []
requiredRoles: ["admin"]
```

**Vault Secret Structure:**

```
auth:issuer          → "https://auth.honeydrunk.io"
auth:audience        → "honeydrunk-grid"
auth:signing_keys    → [{"kid":"key1","alg":"HS256","key":"base64...","active":true}]
auth:clock_skew_seconds → 300 (optional)
```

### Installation

```sh
# Core abstractions only
dotnet add package HoneyDrunk.Auth.Abstractions

# Full runtime with Vault integration
dotnet add package HoneyDrunk.Auth

# ASP.NET Core integration
dotnet add package HoneyDrunk.Auth.AspNetCore
```

### Basic Usage

```csharp
// Program.cs - Setup
var builder = WebApplication.CreateBuilder(args);

// Step 1: Register Kernel (required)
builder.Services.AddHoneyDrunkNode(opts => { /* ... */ });

// Step 2: Register Vault (required for Auth)
builder.Services.AddVault(opts => { /* ... */ });

// Step 3: Register Auth with ASP.NET Core integration
builder.Services.AddHoneyDrunkAuthAspNetCore();

var app = builder.Build();

// Step 4: Configure middleware pipeline
app.UseGridContext();      // Grid context propagation
app.UseHoneyDrunkAuth();   // Authentication middleware

app.Run();
```

```csharp
// Checking Authentication
app.MapGet("/profile", (IAuthenticatedIdentityAccessor identity) =>
{
    if (!identity.IsAuthenticated)
        return Results.Unauthorized();

    return Results.Ok(new
    {
        identity.Identity!.SubjectId,
        identity.Identity.DisplayName,
        Roles = identity.Identity.GetClaimValues(AuthClaimTypes.Role)
    });
});
```

```csharp
// Authorizing Requests
app.MapPost("/admin/action", async (HttpContext ctx) =>
{
    var request = new AuthorizationRequest(
        action: "admin",
        resource: "system",
        requiredRoles: ["admin"]);

    if (!await ctx.AuthorizeOrForbidAsync(request))
        return Results.Empty;  // 403 already sent

    // Authorized - perform action
    return Results.Ok();
});
```

---

## 🔷 Design Philosophy

### Core Principles

1. **Minimal and deterministic** - No magic, predictable behavior
2. **Validation only** - Not a token issuer or user management system
3. **Vault-backed secrets** - No local key storage, secrets from Vault
4. **Kernel-integrated** - Full telemetry, health, and lifecycle support (Kernel is a required host runtime, not an optional integration)
5. **Framework-agnostic abstractions** - `HoneyDrunk.Auth.Abstractions` has no dependencies; runtime requires Kernel
6. **Fail-fast startup** - Validate secrets at application start

### What Auth Is

- A minimal, deterministic AuthN/AuthZ engine for validating Bearer tokens (JWT)
- A Grid node component requiring Kernel as its host runtime
- Vault-backed secret management for signing keys and configuration
- Kernel-integrated for telemetry, context propagation, and lifecycle validation
- Designed for horizontal scaling with no local state

### What Auth Is NOT

- Not a user management system (identity represents subjects, not necessarily human users)
- Not a session store or token issuer
- Not an OAuth server or identity provider
- Not a drop-in JWT library (requires Kernel host runtime)
- Does not handle refresh tokens or persistence

### Why These Patterns?

**Separation of Abstractions:**
- `HoneyDrunk.Auth.Abstractions` has zero dependencies
- Can be referenced by domain projects for contract definitions
- Runtime implementations live in `HoneyDrunk.Auth`

**Vault Integration:**
- Signing keys never stored locally
- Configuration changes don't require redeployment
- Key rotation handled transparently

**Kernel Lifecycle:**
- Startup validation ensures secrets are present before accepting traffic
- Health contributors report key availability
- Readiness checks verify complete configuration

---

## 📦 Project Structure

```
HoneyDrunk.Auth/
├── HoneyDrunk.Auth.Abstractions/      # Pure contracts, no dependencies
│   ├── IAuthenticationProvider.cs     # Authentication contract
│   ├── IAuthorizationPolicy.cs        # Authorization contract
│   ├── AuthCredential.cs              # Credential representation
│   ├── AuthenticatedIdentity.cs       # Identity with claims
│   ├── AuthenticationResult.cs        # Authentication outcome
│   ├── AuthorizationRequest.cs        # Authorization request
│   ├── AuthorizationDecision.cs       # Authorization outcome
│   ├── AuthScheme.cs                  # Scheme constants
│   ├── AuthClaimTypes.cs              # Claim type constants
│   ├── AuthenticationFailureCode.cs   # Authentication error codes
│   ├── AuthorizationDenyCode.cs       # Authorization denial codes
│   └── DenyReason.cs                  # Denial reason record
│
├── HoneyDrunk.Auth/                   # Core runtime (Kernel + Vault)
│   ├── Authentication/
│   │   └── BearerTokenAuthenticationProvider.cs  # JWT validation
│   ├── Authorization/
│   │   └── DefaultAuthorizationPolicy.cs         # Role/scope/owner checks
│   ├── Secrets/
│   │   ├── ISigningKeyProvider.cs     # Signing key contract
│   │   ├── SigningKeyInfo.cs          # Key metadata record
│   │   └── VaultSigningKeyProvider.cs # Vault implementation
│   ├── Lifecycle/
│   │   ├── AuthStartupHook.cs         # Startup validation
│   │   ├── AuthHealthContributor.cs   # Health checks
│   │   └── AuthReadinessContributor.cs # Readiness checks
│   ├── Telemetry/
│   │   └── AuthTelemetry.cs           # Activity/tag constants
│   └── DependencyInjection/
│       ├── HoneyDrunkAuthServiceCollectionExtensions.cs
│       └── HoneyDrunkAuthBuilderExtensions.cs
│
├── HoneyDrunk.Auth.AspNetCore/        # ASP.NET Core integration
│   ├── Middleware/
│   │   └── HoneyDrunkAuthMiddleware.cs # Request authentication
│   ├── Authorization/
│   │   └── AuthorizationEndpointExtensions.cs # Endpoint helpers
│   ├── IAuthenticatedIdentityAccessor.cs  # Identity accessor contract
│   ├── HttpContextIdentityAccessor.cs     # HttpContext implementation
│   └── DependencyInjection/
│       ├── HoneyDrunkAuthAspNetCoreServiceCollectionExtensions.cs
│       └── HoneyDrunkAuthApplicationBuilderExtensions.cs
│
└── HoneyDrunk.Auth.Tests/             # Unit tests
```

---

## 🆕 Key Features

### JWT Bearer Token Validation
- Industry-standard JWT validation via `Microsoft.IdentityModel.JsonWebTokens`
- Configurable issuer, audience, and clock skew
- Multiple signing key support for key rotation
- Detailed failure codes for debugging

### Policy-Based Authorization
- Scope-based access control (all required scopes must be present)
- Role-based access control (any required role is sufficient)
- Resource ownership checks
- Detailed deny reasons for audit logging
- **Evaluation constraints**: Authorization evaluation is local, deterministic, and side-effect free (no external calls during evaluation)

### Vault-Backed Secrets
- Signing keys retrieved from `auth:signing_keys`
- Issuer/audience from `auth:issuer` and `auth:audience`
- Optional clock skew from `auth:clock_skew_seconds`
- No local key storage

### Kernel Integration
- `IStartupHook` - Fail-fast validation at startup
- `IHealthContributor` - Reports signing key availability
- `IReadinessContributor` - Validates complete configuration
- `ITelemetryActivityFactory` - OpenTelemetry spans

#### Startup Validation Invariants

The startup hook enforces the following invariants before the node accepts traffic:

- **Signing keys**: At least one active signing key must be available from Vault
- **Issuer**: The `auth:issuer` secret must be present and non-empty
- **Audience**: The `auth:audience` secret must be present and non-empty
- **Failure behavior**: If any invariant fails, the startup hook throws, preventing the node from starting
- **Vault availability**: If Vault is reachable but returns empty data, startup fails (empty is not valid)

### ASP.NET Core Integration
- Middleware for automatic Bearer token extraction and validation
- `IAuthenticatedIdentityAccessor` for clean identity access
- `HttpContext.User` population for compatibility
- Extension methods for endpoint authorization

---

## 🔗 Relationships

### Upstream Dependencies

**HoneyDrunk.Auth.Abstractions:**
- No external dependencies (pure contracts)

**HoneyDrunk.Auth:**
- `HoneyDrunk.Auth.Abstractions` - Core contracts
- `HoneyDrunk.Kernel` - Telemetry, lifecycle, hosting
- `HoneyDrunk.Vault` - Secret management
- `Microsoft.IdentityModel.JsonWebTokens` - JWT validation

**HoneyDrunk.Auth.AspNetCore:**
- `HoneyDrunk.Auth` - Core runtime
- `HoneyDrunk.Auth.Abstractions` - Core contracts
- `HoneyDrunk.Kernel` - Grid context
- `Microsoft.AspNetCore.App` - ASP.NET Core framework

### Downstream Consumers

Applications using HoneyDrunk.Auth:
- **API Services** - Protect endpoints with Bearer token authentication
- **Grid Nodes** - Secure inter-node communication
- **Service-to-Service** - Agent and service identity validation
- **Multi-tenant Services** - Tenant/project claim extraction

---

## 📖 Additional Resources

### Official Documentation
- [README.md](../README.md) - Project overview and quick start
- [CHANGELOG.md](../HoneyDrunk.Auth/CHANGELOG.md) - Version history

### Related Projects
- [HoneyDrunk.Kernel](https://github.com/HoneyDrunkStudios/HoneyDrunk.Kernel) - Core Grid primitives
- [HoneyDrunk.Vault](https://github.com/HoneyDrunkStudios/HoneyDrunk.Vault) - Secret management
- [HoneyDrunk.Standards](https://github.com/HoneyDrunkStudios/HoneyDrunk.Standards) - Analyzers and conventions

### External References
- [JWT.io](https://jwt.io/) - JWT debugger and introduction
- [RFC 7519](https://tools.ietf.org/html/rfc7519) - JSON Web Token specification
- [Microsoft.IdentityModel](https://github.com/AzureAD/azure-activedirectory-identitymodel-extensions-for-dotnet) - JWT library

---

## 💡 Motto

**"Verify trust, enforce access."** - Validate tokens, evaluate policies, protect resources.

---

*Last Updated: 2025-01-15*  
*Target Framework: .NET 10.0*
