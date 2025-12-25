# 📋 Abstractions - Core Contracts and Types

[← Back to File Guide](FILE_GUIDE.md)

---

## Table of Contents

- [Overview](#overview)
- [Design Constraints](#design-constraints)
- [IAuthenticationProvider.cs](#iauthenticationprovidercs)
- [IAuthorizationPolicy.cs](#iauthorizationpolicycs)
- [AuthCredential.cs](#authcredentialcs)
- [AuthenticatedIdentity.cs](#authenticatedidentitycs)
- [AuthenticationResult.cs](#authenticationresultcs)
- [AuthorizationRequest.cs](#authorizationrequestcs)
- [AuthorizationDecision.cs](#authorizationdecisioncs)
- [AuthScheme.cs](#authschemecs)
- [AuthClaimTypes.cs](#authclaimtypescs)
- [AuthenticationFailureCode.cs](#authenticationfailurecodecs)
- [AuthorizationDenyCode.cs](#authorizationdenycodecs)
- [DenyReason.cs](#denyreasoncs)

---

## Overview

Core abstractions and contracts for the HoneyDrunk authentication and authorization system. This package has **no external dependencies**, making it ideal for defining contracts in shared libraries or domain projects.

**Location:** `HoneyDrunk.Auth.Abstractions/`

The separation of abstractions allows consuming projects to depend only on contracts without pulling in runtime dependencies like JWT libraries or Vault clients.

---

## Design Constraints

### Bearer is the Canonical Scheme

Auth is a **JWT Bearer token validation engine**, not a general-purpose authentication platform.

| Scheme | Status | Notes |
|--------|--------|-------|
| `Bearer` | ✅ Canonical | The only officially supported scheme |
| Others | ⚠️ Exceptional | For specialized internal uses only; should be rare |

> **Warning:** The abstractions allow other schemes for flexibility, but this is not an invitation to invent credential types. Different nodes accepting different schemes would break Grid consistency. When in doubt, use Bearer.

### Identity Represents Subjects, Not Users

`AuthenticatedIdentity` represents a **subject**, which may be:
- Human users
- Services
- Agents
- Grid nodes

Do not assume human interaction patterns. The identity model works identically regardless of subject type.

### Effectively Immutable Types

All abstraction types are designed to be **effectively immutable**:
- Properties are read-only after construction
- Collections are exposed as `IReadOnlyList` or `IReadOnlyDictionary`
- No mutation methods are provided

> **Note:** Types are classes (not records) for serialization compatibility, but should be treated as immutable. Do not attempt to modify instances after construction.

[↑ Back to top](#table-of-contents)

---

## IAuthenticationProvider.cs

```csharp
public interface IAuthenticationProvider
{
    IReadOnlyList<string> SupportedSchemes { get; }
    
    Task<AuthenticationResult> AuthenticateAsync(
        AuthCredential credential,
        CancellationToken cancellationToken = default);
}
```

### Purpose

Defines the contract for authentication providers. Implementations validate credentials and produce authenticated identities.

### SupportedSchemes

The `SupportedSchemes` property returns the schemes this provider can handle.

> **Note:** Most providers should support exactly **one** scheme. The list type exists for future extensibility, not to encourage multi-scheme providers. The canonical implementation (`BearerTokenAuthenticationProvider`) supports only `Bearer`.

### Usage Example

```csharp
public class CustomAuthProvider : IAuthenticationProvider
{
    public IReadOnlyList<string> SupportedSchemes => ["Bearer", "ApiKey"];
    
    public async Task<AuthenticationResult> AuthenticateAsync(
        AuthCredential credential,
        CancellationToken cancellationToken = default)
    {
        if (credential.Scheme == "Bearer")
        {
            // Validate JWT token
            var identity = await ValidateJwtAsync(credential.Value, cancellationToken);
            return AuthenticationResult.Success(identity);
        }
        
        return AuthenticationResult.Fail(
            AuthenticationFailureCode.UnsupportedScheme,
            $"Scheme '{credential.Scheme}' is not supported");
    }
}
```

[↑ Back to top](#table-of-contents)

---

## IAuthorizationPolicy.cs

```csharp
public interface IAuthorizationPolicy
{
    string PolicyName { get; }
    
    Task<AuthorizationDecision> EvaluateAsync(
        AuthenticatedIdentity? identity,
        AuthorizationRequest request,
        CancellationToken cancellationToken = default);
}
```

### Purpose

Defines the contract for authorization policy evaluation. Implementations evaluate authorization requests against authenticated identities.

### Usage Example

```csharp
public class TenantIsolationPolicy : IAuthorizationPolicy
{
    public string PolicyName => "TenantIsolation";
    
    public Task<AuthorizationDecision> EvaluateAsync(
        AuthenticatedIdentity? identity,
        AuthorizationRequest request,
        CancellationToken cancellationToken = default)
    {
        if (identity is null)
        {
            return Task.FromResult(AuthorizationDecision.Deny(
                AuthorizationDenyCode.NotAuthenticated,
                "Authentication required"));
        }
        
        var tenantId = identity.GetClaimValue(AuthClaimTypes.TenantId);
        var resourceTenant = ExtractTenantFromResource(request.Resource);
        
        if (tenantId != resourceTenant)
        {
            return Task.FromResult(AuthorizationDecision.Deny(
                AuthorizationDenyCode.ResourceNotAccessible,
                "Resource belongs to a different tenant"));
        }
        
        return Task.FromResult(AuthorizationDecision.Allow(["tenant-match"]));
    }
}
```

[↑ Back to top](#table-of-contents)

---

## AuthCredential.cs

```csharp
public sealed class AuthCredential
{
    public AuthCredential(string scheme, string value);
    
    public string Scheme { get; }
    public string Value { get; }
    
    public static AuthCredential Bearer(string token);
}
```

### Purpose

Represents an authentication credential for validation. The static `Bearer` factory method is the preferred way to create credentials.

> **Guidance:** Use `AuthCredential.Bearer(token)` in almost all cases. The general constructor exists for specialized scenarios, not general use.

### Usage Example

```csharp
// Preferred: Use the Bearer factory method
var credential = AuthCredential.Bearer("eyJhbGciOiJIUzI1NiIs...");

// Access properties
Console.WriteLine($"Scheme: {credential.Scheme}");  // "Bearer"
Console.WriteLine($"Value: {credential.Value}");    // "eyJhbGciOi..."
```

[↑ Back to top](#table-of-contents)

---

## AuthenticatedIdentity.cs

```csharp
public sealed class AuthenticatedIdentity
{
    public AuthenticatedIdentity(
        string subjectId,
        string scheme,
        string? displayName = null,
        IEnumerable<KeyValuePair<string, IReadOnlyList<string>>>? claims = null);
    
    public string SubjectId { get; }
    public string Scheme { get; }
    public string? DisplayName { get; }
    public IReadOnlyDictionary<string, IReadOnlyList<string>> Claims { get; }
    
    public string? GetClaimValue(string claimType);
    public IReadOnlyList<string> GetClaimValues(string claimType);
    public bool HasClaim(string claimType);
    public bool HasClaim(string claimType, string value);
}
```

### Purpose

Represents an authenticated identity with claims and attributes. This is a neutral representation independent of any specific identity framework (like `ClaimsPrincipal`).

### Usage Example

```csharp
// Create an identity
var claims = new Dictionary<string, IReadOnlyList<string>>
{
    [AuthClaimTypes.Role] = ["admin", "user"],
    [AuthClaimTypes.Scope] = ["read", "write"],
    [AuthClaimTypes.TenantId] = ["tenant-123"]
};

var identity = new AuthenticatedIdentity(
    subjectId: "user-456",
    scheme: AuthScheme.Bearer,
    displayName: "John Doe",
    claims: claims);

// Access identity properties
Console.WriteLine(identity.SubjectId);      // "user-456"
Console.WriteLine(identity.DisplayName);    // "John Doe"

// Check claims
if (identity.HasClaim(AuthClaimTypes.Role, "admin"))
{
    Console.WriteLine("User is an admin");
}

// Get all roles
var roles = identity.GetClaimValues(AuthClaimTypes.Role);
// ["admin", "user"]

// Get single claim value
var tenantId = identity.GetClaimValue(AuthClaimTypes.TenantId);
// "tenant-123"
```

[↑ Back to top](#table-of-contents)

---

## AuthenticationResult.cs

```csharp
public sealed class AuthenticationResult
{
    public bool IsAuthenticated { get; }
    public AuthenticatedIdentity? Identity { get; }
    public AuthenticationFailureCode FailureCode { get; }
    public string? FailureMessage { get; }
    
    public static AuthenticationResult Success(AuthenticatedIdentity identity);
    public static AuthenticationResult Fail(
        AuthenticationFailureCode code,
        string? message = null);
}
```

### Purpose

Represents the result of an authentication operation. Encapsulates both success (with identity) and failure (with code and message) outcomes.

### Usage Example

```csharp
// Successful authentication
var identity = new AuthenticatedIdentity("user-123", AuthScheme.Bearer);
var success = AuthenticationResult.Success(identity);

if (success.IsAuthenticated)
{
    Console.WriteLine($"Welcome, {success.Identity!.SubjectId}");
}

// Failed authentication
var failure = AuthenticationResult.Fail(
    AuthenticationFailureCode.TokenExpired,
    "Token expired at 2024-01-15T10:30:00Z");

if (!failure.IsAuthenticated)
{
    Console.WriteLine($"Auth failed: {failure.FailureCode} - {failure.FailureMessage}");
}
```

[↑ Back to top](#table-of-contents)

---

## AuthorizationRequest.cs

```csharp
public sealed class AuthorizationRequest
{
    public AuthorizationRequest(
        string action,
        string resource,
        IEnumerable<string>? requiredScopes = null,
        IEnumerable<string>? requiredRoles = null,
        string? resourceOwnerId = null);
    
    public string Action { get; }
    public string Resource { get; }
    public IReadOnlyList<string> RequiredScopes { get; }
    public IReadOnlyList<string> RequiredRoles { get; }
    public string? ResourceOwnerId { get; }
}
```

### Purpose

Represents a request for authorization evaluation. Captures the action, resource, and requirements for access control decisions.

### Usage Example

```csharp
// Simple action/resource check
var readRequest = new AuthorizationRequest(
    action: "read",
    resource: "projects/123");

// With required scopes (ALL must be present)
var writeRequest = new AuthorizationRequest(
    action: "write",
    resource: "documents",
    requiredScopes: ["documents:write", "documents:read"]);

// With required roles (ANY is sufficient)
var adminRequest = new AuthorizationRequest(
    action: "delete",
    resource: "users/456",
    requiredRoles: ["admin", "superuser"]);

// With resource ownership check
var ownerRequest = new AuthorizationRequest(
    action: "update",
    resource: "profiles/789",
    resourceOwnerId: "user-789");  // Must match identity.SubjectId
```

[↑ Back to top](#table-of-contents)

---

## AuthorizationDecision.cs

```csharp
public sealed class AuthorizationDecision
{
    public bool IsAllowed { get; }
    public IReadOnlyList<DenyReason> DenyReasons { get; }
    public IReadOnlyList<string> SatisfiedRequirements { get; }
    
    public static AuthorizationDecision Allow(
        IEnumerable<string>? satisfiedRequirements = null);
    
    public static AuthorizationDecision Deny(
        IEnumerable<DenyReason> denyReasons,
        IEnumerable<string>? satisfiedRequirements = null);
    
    public static AuthorizationDecision Deny(
        AuthorizationDenyCode code,
        string message);
}
```

### Purpose

Represents the result of an authorization evaluation. Includes both the decision and audit information about which requirements were satisfied or denied.

### Usage Example

```csharp
// Allow with satisfied requirements (for audit logging)
var allowed = AuthorizationDecision.Allow(["authenticated", "role:admin", "scope:write"]);

if (allowed.IsAllowed)
{
    Console.WriteLine("Access granted");
    Console.WriteLine($"Satisfied: {string.Join(", ", allowed.SatisfiedRequirements)}");
}

// Deny with single reason
var denied = AuthorizationDecision.Deny(
    AuthorizationDenyCode.MissingRole,
    "Required role 'admin' is missing");

// Deny with multiple reasons
var multiDeny = AuthorizationDecision.Deny(
    [
        new DenyReason(AuthorizationDenyCode.MissingScope, "Missing scope 'write'"),
        new DenyReason(AuthorizationDenyCode.MissingRole, "Missing role 'editor'")
    ],
    satisfiedRequirements: ["authenticated"]);

foreach (var reason in multiDeny.DenyReasons)
{
    Console.WriteLine($"Denied: {reason.Code} - {reason.Message}");
}
```

### SatisfiedRequirements Guidelines

The `SatisfiedRequirements` collection is for audit logging and debugging. Keep entries **category-like**, not identifier-like:

| ✅ Good | ❌ Avoid |
|---------|----------|
| `"authenticated"` | `"user-12345"` |
| `"role:admin"` | `"role:admin:user-12345"` |
| `"scope:write"` | `"document-abc-123"` |
| `"owner"` | `"owner-of-resource-xyz"` |

> **Rule:** Requirements should describe *what was satisfied*, not *who or what specific resource*. High-cardinality values belong in logs, not in decision metadata.

[↑ Back to top](#table-of-contents)

---

## AuthScheme.cs

```csharp
public static class AuthScheme
{
    public const string Bearer = "Bearer";
}
```

### Purpose

Defines the authentication schemes supported by the Auth system. Currently supports Bearer token authentication.

### Usage Example

```csharp
// Use the constant for scheme comparison
if (credential.Scheme == AuthScheme.Bearer)
{
    // Handle Bearer token
}

// Create Bearer credential
var credential = new AuthCredential(AuthScheme.Bearer, token);
```

[↑ Back to top](#table-of-contents)

---

## AuthClaimTypes.cs

```csharp
public static class AuthClaimTypes
{
    // Identity claims (public contract - safe to depend on)
    public const string Subject = "sub";
    public const string Name = "name";
    public const string Email = "email";
    public const string Role = "role";
    public const string Scope = "scope";
    public const string TenantId = "tenant_id";
    public const string ProjectId = "project_id";
    
    // Token metadata claims (internal - may not always be surfaced)
    public const string Issuer = "iss";
    public const string Audience = "aud";
    public const string ExpirationTime = "exp";
    public const string NotBefore = "nbf";
    public const string IssuedAt = "iat";
    public const string JwtId = "jti";
}
```

### Purpose

Defines standard claim types used by the Auth system. Uses JWT standard claim names for consistency.

### Claim Categories

| Category | Claims | Stability |
|----------|--------|-----------|
| **Identity claims** | `sub`, `name`, `email`, `role`, `scope`, `tenant_id`, `project_id` | ✅ Public contract - safe to depend on in application code |
| **Token metadata** | `iss`, `aud`, `exp`, `nbf`, `iat`, `jti` | ⚠️ Internal - used for validation; may not be surfaced in `AuthenticatedIdentity` |

> **Guidance:** Application code should primarily use identity claims. Token metadata claims are for advanced scenarios and may not always be present in the identity's claims dictionary.

### Usage Example

```csharp
// Identity claims - safe to use in application logic
if (identity.HasClaim(AuthClaimTypes.Role, "admin"))
{
    // Subject has admin role
}

// Get tenant context
var tenantId = identity.GetClaimValue(AuthClaimTypes.TenantId);
var projectId = identity.GetClaimValue(AuthClaimTypes.ProjectId);

// Get all scopes
var scopes = identity.GetClaimValues(AuthClaimTypes.Scope);

// Token metadata - advanced use only
var jti = identity.GetClaimValue(AuthClaimTypes.JwtId);  // May be null
```

[↑ Back to top](#table-of-contents)

---

## AuthenticationFailureCode.cs

```csharp
public enum AuthenticationFailureCode
{
    None = 0,
    MissingCredential = 1,
    MalformedCredential = 2,
    UnsupportedScheme = 3,
    InvalidSignature = 4,
    TokenExpired = 5,
    TokenNotYetValid = 6,
    InvalidIssuer = 7,
    InvalidAudience = 8,
    MissingClaim = 9,
    InternalError = 99
}
```

### Purpose

Defines stable failure reason codes for authentication operations. Designed to be versionable and suitable for API responses and audit logs.

### Usage Example

```csharp
var result = await authProvider.AuthenticateAsync(credential, ct);

if (!result.IsAuthenticated)
{
    switch (result.FailureCode)
    {
        case AuthenticationFailureCode.TokenExpired:
            // Suggest token refresh
            return Results.Unauthorized();
            
        case AuthenticationFailureCode.InvalidSignature:
            // Log potential security issue
            logger.LogWarning("Invalid token signature detected");
            return Results.Unauthorized();
            
        case AuthenticationFailureCode.InternalError:
            // Server-side issue
            return Results.StatusCode(500);
            
        default:
            return Results.Unauthorized();
    }
}
```

[↑ Back to top](#table-of-contents)

---

## AuthorizationDenyCode.cs

```csharp
public enum AuthorizationDenyCode
{
    None = 0,
    NotAuthenticated = 1,
    MissingScope = 2,
    MissingRole = 3,
    ResourceOwnershipDenied = 4,
    ActionNotPermitted = 5,
    ResourceNotAccessible = 6,
    PolicyNotSatisfied = 7,
    InternalError = 99
}
```

### Purpose

Defines stable deny reason codes for authorization operations. Designed to be versionable and suitable for API responses and audit logs.

### Usage Example

```csharp
var decision = await policy.EvaluateAsync(identity, request, ct);

if (!decision.IsAllowed)
{
    var primaryReason = decision.DenyReasons.FirstOrDefault();
    
    switch (primaryReason.Code)
    {
        case AuthorizationDenyCode.NotAuthenticated:
            return Results.Unauthorized();
            
        case AuthorizationDenyCode.MissingRole:
        case AuthorizationDenyCode.MissingScope:
            return Results.Forbid();
            
        case AuthorizationDenyCode.ResourceOwnershipDenied:
            return Results.Forbid();  // Or 404 to hide resource existence
                        
        default:
            return Results.Forbid();
    }
}
```

[↑ Back to top](#table-of-contents)

---

## DenyReason.cs

```csharp
public readonly record struct DenyReason(
    AuthorizationDenyCode Code,
    string Message);
```

### Purpose

Represents a deny reason from an authorization decision. Pairs a structured code with a human-readable message for debugging and audit logging.

### Usage Example

```csharp
// Create deny reasons
var reasons = new List<DenyReason>
{
    new(AuthorizationDenyCode.MissingScope, "Required scope 'write' is missing"),
    new(AuthorizationDenyCode.MissingRole, "None of the required roles [admin, editor] are present")
};

// Use in authorization decision
var decision = AuthorizationDecision.Deny(reasons, satisfiedRequirements: ["authenticated"]);

// Log deny reasons
foreach (var reason in decision.DenyReasons)
{
    logger.LogWarning(
        "Authorization denied: {Code} - {Message}",
        reason.Code,
        reason.Message);
}
```

[↑ Back to top](#table-of-contents)

---

## Summary

The Abstractions package provides the foundational contracts for the entire Auth system. By having zero dependencies, it can be safely referenced by any project that needs to work with authentication and authorization concepts without pulling in runtime implementations.

Key design decisions:
- **Zero dependencies** - Can be referenced by any project
- **Bearer-canonical** - Bearer is the official scheme; others are exceptional
- **Subject-based identity** - Works for users, services, agents, and	nodes
- **Effectively immutable** - All types are read-only after construction
- **Result types over exceptions** - `AuthenticationResult` and `AuthorizationDecision` encapsulate outcomes
- **Structured codes** - Enums provide stable, versionable error codes
- **Claim categories** - Identity claims (public) vs token metadata (internal)

---

[← Back to File Guide](FILE_GUIDE.md) | [↑ Back to top](#table-of-contents)
