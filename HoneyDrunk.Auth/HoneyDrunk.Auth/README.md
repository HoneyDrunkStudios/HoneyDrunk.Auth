# HoneyDrunk.Auth

[![NuGet](https://img.shields.io/nuget/v/HoneyDrunk.Auth.svg)](https://www.nuget.org/packages/HoneyDrunk.Auth)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)
[![.NET 10](https://img.shields.io/badge/.NET-10.0-512BD4)](https://dotnet.microsoft.com/download/dotnet/10.0)

> **Core authentication and authorization runtime** - JWT Bearer token validation, policy-based authorization, and Vault-backed signing key management.

## ?? What Is This?

This package provides the core runtime implementation for HoneyDrunk.Auth. It includes JWT Bearer token validation, policy-based authorization, and integrates with HoneyDrunk.Kernel for telemetry and lifecycle management, and HoneyDrunk.Vault for secure secret retrieval.

## ?? Installation

```sh
dotnet add package HoneyDrunk.Auth
```

```xml
<PackageReference Include="HoneyDrunk.Auth" Version="0.1.0" />
```

## ?? Key Components

### Authentication

| Component | Description |
|-----------|-------------|
| `BearerTokenAuthenticationProvider` | Validates JWT Bearer tokens using signing keys from Vault |

### Authorization

| Component | Description |
|-----------|-------------|
| `DefaultAuthorizationPolicy` | Evaluates role-based, scope-based, and ownership-based authorization |

### Secrets

| Component | Description |
|-----------|-------------|
| `ISigningKeyProvider` | Contract for retrieving signing keys |
| `VaultSigningKeyProvider` | Retrieves signing keys from HoneyDrunk.Vault |
| `SigningKeyInfo` | Signing key metadata record |

### Lifecycle

| Component | Description |
|-----------|-------------|
| `AuthStartupHook` | Validates Vault secrets at startup (fail-fast) |
| `AuthHealthContributor` | Reports signing key availability for health checks |
| `AuthReadinessContributor` | Reports complete configuration for readiness checks |

### Telemetry

| Component | Description |
|-----------|-------------|
| `AuthTelemetry` | OpenTelemetry activity and tag constants |

## ?? Usage

### Register Services

```csharp
// Via IHoneyDrunkBuilder (recommended)
builder.Services
    .AddHoneyDrunkNode(opts => { /* ... */ })
    .AddVault(opts => { /* ... */ })
    .AddAuth();

// Or directly
builder.Services.AddHoneyDrunkAuth();
```

### Configure Vault Secrets

Ensure the following secrets exist in Vault:

| Key | Description |
|-----|-------------|
| `auth:issuer` | JWT token issuer |
| `auth:audience` | JWT token audience |
| `auth:signing_keys` | JSON array of signing keys |
| `auth:clock_skew_seconds` | (optional) Clock skew tolerance |

### Authenticate Tokens

```csharp
var credential = AuthCredential.Bearer(token);
var result = await authProvider.AuthenticateAsync(credential, ct);

if (result.IsAuthenticated)
{
    var identity = result.Identity;
    Console.WriteLine($"Subject: {identity.SubjectId}");
}
else
{
    Console.WriteLine($"Failed: {result.FailureCode}");
}
```

### Evaluate Authorization

```csharp
var request = new AuthorizationRequest(
    action: "delete",
    resource: "users/123",
    requiredRoles: ["admin"]);

var decision = await policy.EvaluateAsync(identity, request, ct);

if (decision.IsAllowed)
{
    // Proceed with action
}
else
{
    // Check decision.DenyReasons
}
```

## ?? Dependencies

| Package | Purpose |
|---------|---------|
| `HoneyDrunk.Auth.Abstractions` | Core contracts |
| `HoneyDrunk.Kernel` | Telemetry and lifecycle |
| `HoneyDrunk.Vault` | Secret management |
| `Microsoft.IdentityModel.JsonWebTokens` | JWT validation |

## ?? Related Packages

| Package | Description |
|---------|-------------|
| **[HoneyDrunk.Auth.Abstractions](../HoneyDrunk.Auth.Abstractions/README.md)** | Core contracts (no dependencies) |
| **[HoneyDrunk.Auth.AspNetCore](../HoneyDrunk.Auth.AspNetCore/README.md)** | ASP.NET Core middleware and extensions |

## ?? Documentation

- **[Authentication Guide](../docs/Authentication.md)** - JWT Bearer token validation
- **[Authorization Guide](../docs/Authorization.md)** - Policy-based access control
- **[Secrets Guide](../docs/Secrets.md)** - Vault integration
- **[Lifecycle Guide](../docs/Lifecycle.md)** - Health and readiness
- **[FILE_GUIDE.md](../docs/FILE_GUIDE.md)** - Complete architecture reference

## ?? License

This project is licensed under the [MIT License](../LICENSE).

---

<div align="center">

**Built with ?? by HoneyDrunk Studios**

</div>
