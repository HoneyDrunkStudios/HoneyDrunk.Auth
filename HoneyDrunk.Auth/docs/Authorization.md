# 🛡️ Authorization - Policy-Based Access Control

[← Back to File Guide](FILE_GUIDE.md)

---

## Table of Contents

- [Overview](#overview)
- [Evaluation Constraints](#evaluation-constraints)
- [DefaultAuthorizationPolicy.cs](#defaultauthorizationpolicycs)

---

## Overview

Authorization components for evaluating access control decisions. The `DefaultAuthorizationPolicy` provides scope-based, role-based, and ownership-based authorization checks.

**Location:** `HoneyDrunk.Auth/Authorization/`

The authorization evaluation flow:
1. Check if the request is authenticated
2. Verify required scopes (ALL must be present)
3. Verify required roles (ANY is sufficient)
4. Check resource ownership (if specified)
5. Return `AuthorizationDecision` with all satisfied requirements and deny reasons

---

## Evaluation Constraints

Authorization evaluation must adhere to strict constraints to ensure predictable, auditable behavior:

| Constraint | Description |
|------------|-------------|
| **Local** | No external calls (no network, no database, no service lookups) |
| **Deterministic** | Same inputs always produce same outputs |
| **Side-effect free** | No state changes; logging and telemetry are observational only |
| **Input-driven** | All data for decisions must be provided in the identity and request |

> **Why these constraints?** Authorization is called on every protected request. It must be fast (microseconds), predictable (testable), and auditable (inputs fully determine outputs). Hidden dependencies or I/O would break all three properties.

[↑ Back to top](#table-of-contents)

---

## DefaultAuthorizationPolicy.cs

```csharp
public sealed class DefaultAuthorizationPolicy : IAuthorizationPolicy
{
    public DefaultAuthorizationPolicy(
        ITelemetryActivityFactory telemetryFactory,
        ILogger<DefaultAuthorizationPolicy> logger);
    
    public string PolicyName { get; }  // "Default"
    
    public Task<AuthorizationDecision> EvaluateAsync(
        AuthenticatedIdentity? identity,
        AuthorizationRequest request,
        CancellationToken cancellationToken = default);
}
```

### Purpose

Default authorization policy implementation that evaluates scopes, roles, and ownership. Provides comprehensive access control with decision metadata for audit logging.

### Dependencies

| Dependency | Purpose |
|------------|---------|
| `ITelemetryActivityFactory` | Creates OpenTelemetry spans for authorization |
| `ILogger<T>` | Emits observational logs (does not influence decisions) |

> **Note:** Logging and telemetry are **observational only**. They record decisions for audit purposes but do not affect authorization outcomes. The policy's decision logic depends solely on the `identity` and `request` inputs.

### Evaluation Logic

The policy evaluates authorization requests in the following order:

#### 1. Authentication Check

```csharp
if (identity is null)
{
    return AuthorizationDecision.Deny(
        AuthorizationDenyCode.NotAuthenticated,
        "Request is not authenticated");
}
```

If the request is not authenticated, authorization is immediately denied.

#### 2. Scope Validation (ALL Required)

```csharp
foreach (var requiredScope in request.RequiredScopes)
{
    if (identity.HasClaim(AuthClaimTypes.Scope, requiredScope))
    {
        satisfiedRequirements.Add($"scope:{requiredScope}");
    }
    else
    {
        denyReasons.Add(new DenyReason(
            AuthorizationDenyCode.MissingScope,
            $"Required scope '{requiredScope}' is missing"));
    }
}
```

All required scopes must be present for the check to pass.

#### 3. Role Validation (ANY Sufficient)

```csharp
if (request.RequiredRoles.Count > 0)
{
    var hasAnyRole = false;
    foreach (var requiredRole in request.RequiredRoles)
    {
        if (identity.HasClaim(AuthClaimTypes.Role, requiredRole))
        {
            satisfiedRequirements.Add($"role:{requiredRole}");
            hasAnyRole = true;
            break;
        }
    }
    
    if (!hasAnyRole)
    {
        denyReasons.Add(new DenyReason(
            AuthorizationDenyCode.MissingRole,
            $"None of the required roles [...] are present"));
    }
}
```

Only one of the required roles needs to be present.

#### 4. Ownership Check

```csharp
if (!string.IsNullOrEmpty(request.ResourceOwnerId))
{
    if (identity.SubjectId == request.ResourceOwnerId)
    {
        satisfiedRequirements.Add("owner");
    }
    else
    {
        denyReasons.Add(new DenyReason(
            AuthorizationDenyCode.ResourceOwnershipDenied,
            "Identity does not own the requested resource"));
    }
}
```

If a resource owner ID is specified, the identity's subject ID must match.

> **Important:** Ownership is evaluated only against values provided in the `AuthorizationRequest`. Auth does **not** resolve or look up resource ownership. The calling code must supply the `ResourceOwnerId` when constructing the request. This keeps authorization local and side-effect free.

### Usage Examples

#### Simple Role Check

```csharp
var request = new AuthorizationRequest(
    action: "delete",
    resource: "users",
    requiredRoles: ["admin"]);

var decision = await policy.EvaluateAsync(identity, request, ct);

if (!decision.IsAllowed)
{
    // User lacks admin role
    return Results.Forbid();
}
```

#### Scope-Based API Access

```csharp
var request = new AuthorizationRequest(
    action: "write",
    resource: "documents",
    requiredScopes: ["documents:read", "documents:write"]);

var decision = await policy.EvaluateAsync(identity, request, ct);
// Both scopes must be present
```

#### Resource Ownership

```csharp
var request = new AuthorizationRequest(
    action: "update",
    resource: $"profiles/{profileId}",
    resourceOwnerId: profileOwnerId);

var decision = await policy.EvaluateAsync(identity, request, ct);
// Only the profile owner can update
```

#### Combined Requirements

```csharp
var request = new AuthorizationRequest(
    action: "publish",
    resource: "articles",
    requiredScopes: ["articles:write"],
    requiredRoles: ["author", "editor"]);

var decision = await policy.EvaluateAsync(identity, request, ct);
// Must have articles:write scope AND (author OR editor role)
```

### Audit Logging

The policy logs all authorization decisions:

```csharp
// On denial
logger.LogWarning(
    "Authorization denied for subject {SubjectId} on {Action} {Resource}: {Reasons}",
    identity.SubjectId,
    request.Action,
    request.Resource,
    string.Join("; ", denyReasons.Select(r => r.Message)));

// On success
logger.LogDebug(
    "Authorization allowed for subject {SubjectId} on {Action} {Resource}",
    identity.SubjectId,
    request.Action,
    request.Resource);
```

### Telemetry

The policy creates an OpenTelemetry activity for each authorization evaluation:

| Tag | Description |
|-----|-------------|
| `authz.policy` | The policy name ("Default") |
| `authz.result` | "allow" or "deny" |
| `authz.failure_code` | The primary failure code (on denial) |

### Registration

```csharp
// Registered automatically via AddHoneyDrunkAuth()
services.AddHoneyDrunkAuth();

// Or register manually
services.AddSingleton<IAuthorizationPolicy, DefaultAuthorizationPolicy>();
```

### Custom Policy Implementation

To create a custom policy, implement `IAuthorizationPolicy`:

```csharp
public class TenantAwarePolicy : IAuthorizationPolicy
{
    public string PolicyName => "TenantAware";
    
    public Task<AuthorizationDecision> EvaluateAsync(
        AuthenticatedIdentity? identity,
        AuthorizationRequest request,
        CancellationToken ct)
    {
        if (identity is null)
        {
            return Task.FromResult(AuthorizationDecision.Deny(
                AuthorizationDenyCode.NotAuthenticated,
                "Authentication required"));
        }
        
        // Tenant context comes from the identity claims (set during authentication)
        // NOT from database lookups or external calls
        var userTenant = identity.GetClaimValue(AuthClaimTypes.TenantId);
        
        // Resource tenant must be passed in the request by the calling code
        // For example: new AuthorizationRequest(...) with resource = "tenants/abc/documents/123"
        // The caller extracts "abc" BEFORE calling the policy
        var resourceTenant = request.Resource.Split('/')[1]; // Simple parsing only
        
        if (userTenant != resourceTenant)
        {
            return Task.FromResult(AuthorizationDecision.Deny(
                AuthorizationDenyCode.ResourceNotAccessible,
                "Cross-tenant access is not permitted"));
        }
        
        return Task.FromResult(AuthorizationDecision.Allow(["authenticated", "tenant-match"]));
    }
}
```

> **Custom Policy Rules:** Custom policies must follow the same [evaluation constraints](#evaluation-constraints) as the default policy. All data required for authorization decisions must come from the `identity` claims or the `request` parameters. Policies must **never** perform I/O, database queries, or external service calls.

[↑ Back to top](#table-of-contents)

---

## Summary

The `DefaultAuthorizationPolicy` provides a comprehensive yet straightforward authorization model suitable for most applications. It combines three common access control patterns:

1. **Scope-based** (OAuth 2.0 style) - Fine-grained permissions
2. **Role-based** (RBAC style) - Broad permission groups
3. **Ownership-based** - Resource-level access control

Key features:
- **Auditable decisions** - All decisions are logged with context
- **Detailed deny reasons** - Multiple reasons can be returned
- **Satisfied requirements tracking** - Shows what passed for debugging
- **OpenTelemetry integration** - Spans for distributed tracing

---

[← Back to File Guide](FILE_GUIDE.md) | [↑ Back to top](#table-of-contents)
