# HoneyDrunk.Auth

[![Validate PR](https://github.com/HoneyDrunkStudios/HoneyDrunk.Auth/actions/workflows/validate-pr.yml/badge.svg)](https://github.com/HoneyDrunkStudios/HoneyDrunk.Auth/actions/workflows/validate-pr.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)
[![.NET 10](https://img.shields.io/badge/.NET-10.0-512BD4)](https://dotnet.microsoft.com/download/dotnet/10.0)

> **Authentication & Authorization Engine for HoneyDrunk Grid Nodes** - JWT Bearer token validation, policy-based authorization, and Vault-backed secret management.

## 📦 What Is This?

HoneyDrunk.Auth is the **security layer** of HoneyDrunk.OS. It provides a minimal, deterministic authentication and authorization engine that validates JWT Bearer tokens and evaluates access policies using secrets managed by Vault.

### Core Responsibilities

- ✅ **JWT Bearer Token Validation** - Industry-standard token validation with configurable issuers, audiences, and signing keys
- ✅ **Policy-Based Authorization** - Scope-based, role-based, and ownership-based access control
- ✅ **Vault Integration** - Signing keys and configuration retrieved securely from HoneyDrunk.Vault
- ✅ **Kernel Integration** - Full telemetry, health checks, and lifecycle management
- ✅ **ASP.NET Core Middleware** - Seamless integration with the request pipeline
- ✅ **Fail-Fast Startup** - Validates secrets at startup, preventing misconfigured deployments

### What Auth Is NOT

- ❌ Not a user management system
- ❌ Not a session store or token issuer
- ❌ Not an OAuth server or identity provider
- ❌ Does not handle refresh tokens or persistence

**Signal Quote:** *"Verify trust, enforce access."*

---

## 🚀 Quick Start

### Installation

```sh
# Full ASP.NET Core integration (recommended)
dotnet add package HoneyDrunk.Auth.AspNetCore

# Core runtime only (for non-web scenarios)
dotnet add package HoneyDrunk.Auth

# Abstractions only (for libraries)
dotnet add package HoneyDrunk.Auth.Abstractions
```

```xml
<ItemGroup>
  <!-- ASP.NET Core integration (recommended) -->
  <PackageReference Include="HoneyDrunk.Auth.AspNetCore" Version="0.1.0" />
  
  <!-- Core runtime only -->
  <PackageReference Include="HoneyDrunk.Auth" Version="0.1.0" />
  
  <!-- Abstractions only (for libraries) -->
  <PackageReference Include="HoneyDrunk.Auth.Abstractions" Version="0.1.0" />
</ItemGroup>
```

### Minimal Setup

```csharp
var builder = WebApplication.CreateBuilder(args);

// 1. Register Kernel and Vault (required)
builder.Services
    .AddHoneyDrunkNode(opts => { /* ... */ })
    .AddVault(opts => { /* ... */ })
    .AddAuth();

// 2. Add ASP.NET Core integration
builder.Services.AddHoneyDrunkAuthAspNetCore();

var app = builder.Build();

// 3. Configure middleware pipeline
app.UseGridContext();      // Grid context propagation
app.UseHoneyDrunkAuth();   // Authentication middleware

// 4. Use authentication in endpoints
app.MapGet("/profile", (IAuthenticatedIdentityAccessor identity) =>
{
    if (!identity.IsAuthenticated)
        return Results.Unauthorized();

    return Results.Ok(new { identity.Identity!.SubjectId });
});

app.Run();
```

### Configure Vault Secrets

Ensure the following secrets exist in Vault:

| Key | Description |
|-----|-------------|
| `auth:issuer` | JWT token issuer (e.g., `https://auth.honeydrunk.io`) |
| `auth:audience` | JWT token audience (e.g., `honeydrunk-grid`) |
| `auth:signing_keys` | JSON array of signing keys |
| `auth:clock_skew_seconds` | (optional) Clock skew tolerance (default: 300) |

**Signing Keys Format:**

```json
[
  {
    "kid": "key-2024-01",
    "alg": "HS256",
    "key": "base64EncodedKeyMaterial==",
    "active": true
  }
]
```

---

## 🎯 Key Features

### 🔐 JWT Bearer Token Authentication

```csharp
// Automatic via middleware - just add the header
// Authorization: Bearer <your-jwt-token>

// Or validate manually
var credential = AuthCredential.Bearer(token);
var result = await authProvider.AuthenticateAsync(credential, ct);

if (result.IsAuthenticated)
{
    var identity = result.Identity;
    Console.WriteLine($"Welcome, {identity.SubjectId}");
}
```

### 🛡️ Policy-Based Authorization

```csharp
// Role-based (ANY role is sufficient)
var request = new AuthorizationRequest(
    action: "delete",
    resource: "users",
    requiredRoles: ["admin", "superuser"]);

// Scope-based (ALL scopes required)
var request = new AuthorizationRequest(
    action: "write",
    resource: "documents",
    requiredScopes: ["documents:read", "documents:write"]);

// Ownership-based
var request = new AuthorizationRequest(
    action: "update",
    resource: $"profiles/{profileId}",
    resourceOwnerId: ownerId);

// Evaluate
var decision = await policy.EvaluateAsync(identity, request, ct);
if (!decision.IsAllowed)
{
    // Access denied - check decision.DenyReasons
}
```

### 🌐 ASP.NET Core Integration

```csharp
// Extension methods for endpoints
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

// Simple authentication check
app.MapGet("/protected", (HttpContext ctx) =>
{
    if (!ctx.RequireAuthentication())
        return Results.Empty;  // 401 already sent
    
    return Results.Ok("You are authenticated!");
});
```

---

## 🏗️ Project Structure

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
│   │   └── BearerTokenAuthenticationProvider.cs
│   ├── Authorization/
│   │   └── DefaultAuthorizationPolicy.cs
│   ├── Secrets/
│   │   ├── ISigningKeyProvider.cs
│   │   ├── SigningKeyInfo.cs
│   │   └── VaultSigningKeyProvider.cs
│   ├── Lifecycle/
│   │   ├── AuthStartupHook.cs
│   │   ├── AuthHealthContributor.cs
│   │   └── AuthReadinessContributor.cs
│   ├── Telemetry/
│   │   └── AuthTelemetry.cs
│   └── DependencyInjection/
│       ├── HoneyDrunkAuthServiceCollectionExtensions.cs
│       └── HoneyDrunkAuthBuilderExtensions.cs
│
├── HoneyDrunk.Auth.AspNetCore/        # ASP.NET Core integration
│   ├── Middleware/
│   │   └── HoneyDrunkAuthMiddleware.cs
│   ├── Authorization/
│   │   └── AuthorizationEndpointExtensions.cs
│   ├── IAuthenticatedIdentityAccessor.cs
│   ├── HttpContextIdentityAccessor.cs
│   └── DependencyInjection/
│       ├── HoneyDrunkAuthAspNetCoreServiceCollectionExtensions.cs
│       └── HoneyDrunkAuthApplicationBuilderExtensions.cs
│
├── HoneyDrunk.Auth.Tests/             # Unit tests
└── docs/                               # Complete documentation
```

### Dependency Diagram

```
┌─────────────────────────────────────────────────────────────────┐
│                     HoneyDrunk.Auth.AspNetCore                  │
│  - Middleware                                                   │
│  - Authorization helpers                                        │
│  - Identity accessor                                            │
└────────────────────────────┬────────────────────────────────────┘
                             │
                             ▼
┌─────────────────────────────────────────────────────────────────┐
│                        HoneyDrunk.Auth                          │
│  - Token validation                                             │
│  - Authorization policy                                         │
│  - Vault secret access                                          │
│  - Kernel telemetry + lifecycle                                 │
└────────────────────────────┬────────────────────────────────────┘
                             │
                             ▼
┌─────────────────────────────────────────────────────────────────┐
│                  HoneyDrunk.Auth.Abstractions                   │
│  - IAuthenticationProvider                                      │
│  - IAuthorizationPolicy                                         │
│  - Models: AuthCredential, AuthenticatedIdentity, etc.          │
│  - No external dependencies                                     │
└─────────────────────────────────────────────────────────────────┘
```

---

## 📖 Documentation

### Package Documentation

- **[HoneyDrunk.Auth.Abstractions README](HoneyDrunk.Auth.Abstractions/README.md)** - Contracts/abstractions package
- **[HoneyDrunk.Auth README](HoneyDrunk.Auth/README.md)** - Runtime implementations package
- **[HoneyDrunk.Auth.AspNetCore README](HoneyDrunk.Auth.AspNetCore/README.md)** - ASP.NET Core integration

### Architecture & Guides

**Core Documentation:**

- **[FILE_GUIDE.md](docs/FILE_GUIDE.md)** - Complete file structure and architecture reference (START HERE)
- **[ARCHITECTURE.md](docs/ARCHITECTURE.md)** - Layering, Kernel/Vault integration
- **[Abstractions.md](docs/Abstractions.md)** - Core contracts and types

**Component Guides:**

- **[Authentication.md](docs/Authentication.md)** - JWT Bearer token validation
- **[Authorization.md](docs/Authorization.md)** - Policy-based access control
- **[Secrets.md](docs/Secrets.md)** - Vault integration and signing keys
- **[Lifecycle.md](docs/Lifecycle.md)** - Health, readiness, and startup hooks
- **[Telemetry.md](docs/Telemetry.md)** - OpenTelemetry integration

**Integration:**

- **[AspNetCore.md](docs/AspNetCore.md)** - ASP.NET Core middleware and extensions
- **[DependencyInjection.md](docs/DependencyInjection.md)** - Service registration

---

## 🔗 Related Projects

**Dependencies:**

- **[HoneyDrunk.Kernel](https://github.com/HoneyDrunkStudios/HoneyDrunk.Kernel)** - Context propagation, lifecycle, telemetry
- **[HoneyDrunk.Vault](https://github.com/HoneyDrunkStudios/HoneyDrunk.Vault)** - Secret management

**Ecosystem:**

- **[HoneyDrunk.Standards](https://github.com/HoneyDrunkStudios/HoneyDrunk.Standards)** - Analyzers and coding conventions
- **HoneyDrunk.Transport** - Messaging infrastructure
- **HoneyDrunk.Data** - Data persistence conventions

---

## 📄 License

This project is licensed under the [MIT License](LICENSE).

---

<div align="center">

**Built with 🍯 by HoneyDrunk Studios**

[GitHub](https://github.com/HoneyDrunkStudios/HoneyDrunk.Auth) • [Documentation](docs/FILE_GUIDE.md) • [Issues](https://github.com/HoneyDrunkStudios/HoneyDrunk.Auth/issues)

</div>
