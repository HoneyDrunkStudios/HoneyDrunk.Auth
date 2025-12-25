# HoneyDrunk.Auth.Abstractions

[![NuGet](https://img.shields.io/nuget/v/HoneyDrunk.Auth.Abstractions.svg)](https://www.nuget.org/packages/HoneyDrunk.Auth.Abstractions)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)
[![.NET 10](https://img.shields.io/badge/.NET-10.0-512BD4)](https://dotnet.microsoft.com/download/dotnet/10.0)

> **Pure contracts and abstractions for HoneyDrunk.Auth** - Zero dependencies, designed for shared libraries and domain projects.

## ?? What Is This?

This package provides the foundational interfaces and models used throughout the HoneyDrunk.Auth ecosystem. It has **no external dependencies**, making it ideal for defining contracts in shared libraries or domain projects without pulling in runtime implementations.

## ?? Installation

```sh
dotnet add package HoneyDrunk.Auth.Abstractions
```

```xml
<PackageReference Include="HoneyDrunk.Auth.Abstractions" Version="0.1.0" />
```

## ?? Key Types

### Interfaces

| Interface | Description |
|-----------|-------------|
| `IAuthenticationProvider` | Contract for validating credentials and producing authenticated identities |
| `IAuthorizationPolicy` | Contract for evaluating authorization decisions against a request |

### Models

| Type | Description |
|------|-------------|
| `AuthCredential` | Represents authentication credentials (e.g., Bearer token) |
| `AuthenticatedIdentity` | Represents a successfully authenticated user with claims |
| `AuthenticationResult` | Result of an authentication attempt (success or failure) |
| `AuthorizationRequest` | Describes an authorization check (action, resource, required roles) |
| `AuthorizationDecision` | Result of an authorization evaluation (allowed or denied) |
| `DenyReason` | Structured denial reason with code and message |

### Enums

| Enum | Description |
|------|-------------|
| `AuthScheme` | Supported authentication schemes (e.g., Bearer) |
| `AuthenticationFailureCode` | Failure codes for authentication errors |
| `AuthorizationDenyCode` | Denial codes for authorization failures |

### Constants

| Constant | Description |
|----------|-------------|
| `AuthClaimTypes` | Standard JWT claim type constants (sub, role, scope, etc.) |

## ?? Usage Example

```csharp
// Create an authorization request
var request = new AuthorizationRequest(
    action: "delete",
    resource: "users/123",
    requiredRoles: ["admin"]);

// Check claims on an identity
if (identity.HasClaim(AuthClaimTypes.Role, "admin"))
{
    // User has admin role
}

// Get tenant context
var tenantId = identity.GetClaimValue(AuthClaimTypes.TenantId);
```

## ?? Related Packages

| Package | Description |
|---------|-------------|
| **[HoneyDrunk.Auth](../HoneyDrunk.Auth/README.md)** | Core runtime with JWT validation and Vault integration |
| **[HoneyDrunk.Auth.AspNetCore](../HoneyDrunk.Auth.AspNetCore/README.md)** | ASP.NET Core middleware and extensions |

## ?? Documentation

- **[Abstractions Guide](../docs/Abstractions.md)** - Detailed documentation for all types
- **[FILE_GUIDE.md](../docs/FILE_GUIDE.md)** - Complete architecture reference

## ?? License

This project is licensed under the [MIT License](../LICENSE).

---

<div align="center">

**Built with ?? by HoneyDrunk Studios**

</div>
