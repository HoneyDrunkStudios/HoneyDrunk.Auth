# 🔐 Authentication - JWT Bearer Token Validation

[← Back to File Guide](FILE_GUIDE.md)

---

## Table of Contents

- [Overview](#overview)
- [BearerTokenAuthenticationProvider.cs](#bearertokenauthenticationprovidercs)

---

## Overview

Authentication components for validating JWT Bearer tokens. The `BearerTokenAuthenticationProvider` is the core implementation that validates tokens using signing keys retrieved from Vault.

**Location:** `HoneyDrunk.Auth/Authentication/`

The authentication flow:
1. Extract Bearer token from the `Authorization` header
2. Validate the JWT token against in-memory signing keys (loaded at startup from Vault)
3. Verify signature, lifetime, issuer, and audience claims
4. Create an `AuthenticatedIdentity` from the validated token

> **Note:** Signing keys, issuer, and audience are loaded from Vault at startup and cached in-memory. Authentication does **not** call Vault on every request. This keeps authentication fast and predictable.

---

## BearerTokenAuthenticationProvider.cs

```csharp
public sealed class BearerTokenAuthenticationProvider : IAuthenticationProvider
{
    public BearerTokenAuthenticationProvider(
        ISigningKeyProvider keyProvider,
        ITelemetryActivityFactory telemetryFactory,
        ILogger<BearerTokenAuthenticationProvider> logger);
    
    public IReadOnlyList<string> SupportedSchemes { get; }  // ["Bearer"]
    
    public Task<AuthenticationResult> AuthenticateAsync(
        AuthCredential credential,
        CancellationToken cancellationToken = default);
}
```

### Purpose

Bearer token authentication provider using JWT validation. Validates tokens against signing keys retrieved from Vault and produces `AuthenticatedIdentity` instances on success.

This provider is **intentionally Bearer-only**. Additional authentication schemes (e.g., API keys, mTLS) would require separate `IAuthenticationProvider` implementations.

### Dependencies

| Dependency | Purpose |
|------------|---------|
| `ISigningKeyProvider` | Provides signing keys, issuer, audience (loaded from Vault at startup) |
| `ITelemetryActivityFactory` | Creates OpenTelemetry spans for authentication |
| `ILogger<T>` | Logs authentication attempts and failures |

### Token Validation Parameters

The provider validates tokens with the following parameters:

| Parameter | Source | Description |
|-----------|--------|-------------|
| `ValidateIssuer` | `auth:issuer` | Token must be issued by trusted issuer |
| `ValidateAudience` | `auth:audience` | Token must be intended for this audience |
| `ValidateLifetime` | JWT exp/nbf claims | Token must not be expired or not-yet-valid |
| `ValidateIssuerSigningKey` | `auth:signing_keys` | Token signature must be valid |
| `ClockSkew` | `auth:clock_skew_seconds` | Tolerance for time differences (default: 5 min) |

### Claim Handling

The provider handles various claim formats:

- **Array claims**: Flattened into multiple values for the same key
- **Space-separated scopes**: Split into individual scope values (OAuth 2.0 convention)
- **Standard JWT claims**: Mapped to `AuthClaimTypes` constants

#### Claim Normalization Rules

| Rule | Behavior |
|------|----------|
| **Claim types** | Case-sensitive; `Role` and `role` are distinct claim types |
| **Claim values** | Preserved as-is; no normalization applied |
| **Scope splitting** | Only the `scope` claim is split on spaces; other claims are not |

> **Why case-sensitive?** JWT ecosystems are inconsistent about casing. Treating claim types as case-sensitive ensures no silent mismatches and makes behavior predictable across identity providers.

### Failure Code Mapping

| Exception Type | Failure Code |
|---------------|--------------|
| `SecurityTokenExpiredException` | `TokenExpired` |
| `SecurityTokenNotYetValidException` | `TokenNotYetValid` |
| `SecurityTokenInvalidSignatureException` | `InvalidSignature` |
| `SecurityTokenInvalidIssuerException` | `InvalidIssuer` |
| `SecurityTokenInvalidAudienceException` | `InvalidAudience` |
| `SecurityTokenMalformedException` | `MalformedCredential` |
| Other/null | `InvalidSignature` |

### Usage Example

```csharp
// Direct usage (typically via DI)
public class TokenValidationService(IAuthenticationProvider authProvider)
{
    public async Task<AuthenticatedIdentity?> ValidateTokenAsync(
        string bearerToken,
        CancellationToken ct)
    {
        var credential = AuthCredential.Bearer(bearerToken);
        var result = await authProvider.AuthenticateAsync(credential, ct);
        
        if (result.IsAuthenticated)
        {
            return result.Identity;
        }
        
        // Log failure for debugging
        Console.WriteLine($"Auth failed: {result.FailureCode} - {result.FailureMessage}");
        return null;
    }
}
```

### Telemetry

The provider creates an OpenTelemetry activity for each authentication attempt:

| Tag | Description |
|-----|-------------|
| `auth.scheme` | The authentication scheme used |
| `auth.result` | "success" or "fail" |
| `auth.failure_code` | The failure code (on failure) |

### Registration

```csharp
// Registered automatically via AddHoneyDrunkAuth()
services.AddHoneyDrunkAuth();

// Or register manually
services.AddSingleton<IAuthenticationProvider, BearerTokenAuthenticationProvider>();
```

### Integration with Middleware

The `HoneyDrunkAuthMiddleware` uses this provider automatically:

```csharp
// In HoneyDrunkAuthMiddleware.InvokeAsync
var credential = AuthCredential.Bearer(token);
var result = await authProvider.AuthenticateAsync(credential, context.RequestAborted);

if (result.IsAuthenticated && result.Identity is not null)
{
    context.Items[HttpContextIdentityAccessor.IdentityKey] = result.Identity;
    context.User = CreateClaimsPrincipal(result.Identity);
}
```

[↑ Back to top](#table-of-contents)

---

## Summary

The `BearerTokenAuthenticationProvider` is the heart of the Auth system's authentication capability. It leverages the industry-standard `Microsoft.IdentityModel.JsonWebTokens` library for JWT validation while integrating with HoneyDrunk's Vault for secret management and Kernel for telemetry.

Key features:
- **Vault-backed secrets** - No hardcoded keys
- **Full JWT validation** - Signature, lifetime, issuer, audience
- **Detailed failure codes** - Specific error types for debugging
- **OpenTelemetry integration** - Spans for distributed tracing
- **Multi-key support** - Key rotation without downtime

---

[← Back to File Guide](FILE_GUIDE.md) | [↑ Back to top](#table-of-contents)
