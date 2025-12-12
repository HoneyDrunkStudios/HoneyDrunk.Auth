# HoneyDrunk.Auth

Authentication and authorization engine for HoneyDrunk Grid nodes.

## What Auth Is

- A minimal, deterministic AuthN/AuthZ engine for validating Bearer tokens (JWT)
- Vault-backed secret management for signing keys and configuration
- Kernel-integrated for telemetry, context propagation, and lifecycle validation
- Designed for horizontal scaling with no local state

## What Auth Is NOT

- Not a user management system
- Not a session store or token issuer
- Not an OAuth server or identity provider
- Does not handle refresh tokens or persistence

## Projects

```
HoneyDrunk.Auth.Abstractions    Pure contracts, no dependencies
        ▲
        │
HoneyDrunk.Auth                 Core runtime (Kernel + Vault integration)
        ▲
        │
HoneyDrunk.Auth.AspNetCore      ASP.NET Core middleware and extensions
```

## Quick Start

### 1. Add NuGet Packages

```xml
<PackageReference Include="HoneyDrunk.Auth.AspNetCore" Version="..." />
```

### 2. Configure Services

```csharp
builder.Services
    .AddHoneyDrunkNode(opts => { /* ... */ })
    .AddVault(opts => { /* ... */ })
    .AddAuth();

// Or for ASP.NET Core:
builder.Services.AddHoneyDrunkAuthAspNetCore();
```

### 3. Configure Middleware

```csharp
app.UseGridContext();
app.UseHoneyDrunkAuth();
```

### 4. Configure Vault Secrets

Ensure the following secrets exist in Vault:

| Key | Description |
|-----|-------------|
| `auth:issuer` | JWT token issuer |
| `auth:audience` | JWT token audience |
| `auth:signing_keys` | JSON array of signing keys |
| `auth:clock_skew_seconds` | (optional) Clock skew tolerance |

See [docs/DEPENDENCIES.md](docs/DEPENDENCIES.md) for secret format details.

## Consuming Auth in Your Node

### Check Authentication

```csharp
app.MapGet("/profile", (IAuthenticatedIdentityAccessor identity) =>
{
    if (!identity.IsAuthenticated)
        return Results.Unauthorized();

    return Results.Ok(new { identity.Identity!.SubjectId });
});
```

### Authorize Requests

```csharp
app.MapPost("/admin/action", async (HttpContext ctx) =>
{
    var request = new AuthorizationRequest(
        action: "admin",
        resource: "system",
        requiredRoles: ["admin"]);

    if (!await ctx.AuthorizeOrForbidAsync(request))
        return Results.Empty;

    // Authorized - perform action
    return Results.Ok();
});
```

## Dependency Diagram

```
┌─────────────────────────────────────────────────────────────────┐
│                     HoneyDrunk.Auth.AspNetCore                   │
│  - Middleware                                                   │
│  - Authorization helpers                                        │
│  - No Vault reference                                           │
└────────────────────────────┬────────────────────────────────────┘
                             │
                             ▼
┌─────────────────────────────────────────────────────────────────┐
│                        HoneyDrunk.Auth                          │
│  - Token validation                                             │
│  - Authorization policy                                         │
│  - Kernel telemetry + context                                   │
│  - Vault secret access (ONLY Vault consumer)                    │
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

## Documentation

- [Architecture](docs/ARCHITECTURE.md) - Layering, Kernel/Vault integration
- [Dependencies](docs/DEPENDENCIES.md) - Runtime dependencies, Vault secrets
- [Integration Notes](docs/INTEGRATION_NOTES.md) - Kernel/Vault API details

## License

Copyright © HoneyDrunk Studios. All rights reserved.
