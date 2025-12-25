# ❤️ Lifecycle - Health, Readiness, and Startup Hooks

[← Back to File Guide](FILE_GUIDE.md)

---

## Table of Contents

- [Overview](#overview)
- [Runtime Behavior](#runtime-behavior)
- [AuthStartupHook.cs](#authstartuphookcs)
- [AuthHealthContributor.cs](#authhealthcontributorcs)
- [AuthReadinessContributor.cs](#authreadinesscontributorcs)
- [Kubernetes Integration](#kubernetes-integration)

---

## Overview

Lifecycle components that integrate Auth with HoneyDrunk.Kernel's lifecycle management. These components ensure the Auth system is properly configured at startup and can report its health status for Kubernetes probes.

**Location:** `HoneyDrunk.Auth/Lifecycle/`

**Lifecycle Flow:**

```
Application Start
      ↓
AuthStartupHook.ExecuteAsync()
  - Validate auth:issuer
  - Validate auth:audience
  - Validate auth:signing_keys
  - Cache validated configuration
  - Throw if any missing → Fail fast
      ↓
Application Running
      ↓
Health Probes (ongoing)
  - AuthHealthContributor → Signing key availability (from cache)
  - AuthReadinessContributor → Complete configuration (from cache)
```

---

## Runtime Behavior

### Startup vs Probe Separation

Auth lifecycle follows a strict separation between startup validation and runtime probes:

| Phase | Vault I/O | Failure Effect |
|-------|-----------|----------------|
| **Startup** | ✅ Yes (once) | Node does not start |
| **Health probes** | ❌ No | Pod restart (if critical) |
| **Readiness probes** | ❌ No | Remove from load balancer |

> **Invariant:** Health and readiness checks must not perform Vault I/O. They reflect the last known validated configuration loaded at startup. This prevents cascading failures when Vault experiences latency or outages.

### Why This Separation Matters

- **Startup** enforces invariants once, blocking the node until Auth is fully configured
- **Health** verifies Auth is still functioning (signing keys accessible in memory)
- **Readiness** verifies Auth hasn't drifted out of a valid state since startup

If health/readiness probes called Vault on every check:
- Vault latency would cause health check timeouts
- Kubernetes would restart healthy pods
- Cascading failures across the cluster

### Priority Semantics

Kernel lifecycle hooks and contributors use priority values to control ordering:

| Component | Priority | Rationale |
|-----------|----------|-----------|
| `AuthStartupHook` | 100 | Runs **late** in startup sequence, after Vault is available |
| `AuthHealthContributor` | 50 | Mid-range; no specific ordering requirements |
| `AuthReadinessContributor` | 50 | Mid-range; no specific ordering requirements |

> **Note:** Lower priority values run earlier. Auth startup runs at priority 100 to ensure Vault startup hooks (typically lower priority) have completed first.

[↑ Back to top](#table-of-contents)

---

## AuthStartupHook.cs

```csharp
public sealed class AuthStartupHook(
    ISigningKeyProvider keyProvider,
    ILogger<AuthStartupHook> logger) : IStartupHook
{
    public int Priority => 100;
    
    public Task ExecuteAsync(CancellationToken cancellationToken);
}
```

### Purpose

Startup hook that validates required Auth secrets are available in Vault. Implements fail-fast behavior by checking for issuer, audience, and signing keys at startup.

This is the **only** point where Auth performs Vault I/O. After successful validation, configuration is cached for request-time use.

### Priority

The hook runs with `Priority = 100`, which is relatively late in the startup sequence to ensure Vault is available.

### Validation Steps

The hook validates three critical secrets:

1. **Issuer** (`auth:issuer`)
   - Must be present and non-empty
   - Used for JWT token validation

2. **Audience** (`auth:audience`)
   - Must be present and non-empty
   - Used for JWT token validation

3. **Signing Keys** (`auth:signing_keys`)
   - Must contain at least one active key
   - Used for JWT signature validation

### Error Handling

If any validation fails, the hook throws `InvalidOperationException` with details:

```csharp
if (errors.Count > 0)
{
    var errorMessage = string.Join(Environment.NewLine, errors);
    _logger.LogCritical("Auth startup validation failed:{NewLine}{Errors}", 
        Environment.NewLine, errorMessage);
    throw new InvalidOperationException(
        $"Auth startup validation failed: {errorMessage}");
}
```

### Logging Output

```
info: HoneyDrunk.Auth.Lifecycle.AuthStartupHook
      Validating Auth secrets in Vault...
debug: HoneyDrunk.Auth.Lifecycle.AuthStartupHook
      Validated auth:issuer = https://auth.honeydrunk.io
debug: HoneyDrunk.Auth.Lifecycle.AuthStartupHook
      Validated auth:audience = honeydrunk-grid
debug: HoneyDrunk.Auth.Lifecycle.AuthStartupHook
      Validated 2 active signing keys
info: HoneyDrunk.Auth.Lifecycle.AuthStartupHook
      Auth secrets validation completed successfully
```

### Registration

```csharp
// Registered automatically via AddHoneyDrunkAuth()
services.AddSingleton<IStartupHook, AuthStartupHook>();
```

[↑ Back to top](#table-of-contents)

---

## AuthHealthContributor.cs

```csharp
public sealed class AuthHealthContributor(
    ISigningKeyProvider keyProvider,
    ILogger<AuthHealthContributor> logger) : IHealthContributor
{
    public string Name => "Auth";
    public int Priority => 50;
    public bool IsCritical => true;
    
    public Task<(HealthStatus status, string? message)> CheckHealthAsync(
        CancellationToken cancellationToken);
}
```

### Purpose

Health contributor that checks Auth system health by verifying signing key availability. Integrates with Kernel health aggregation for Kubernetes liveness probes.

> **Note:** This check reads from the cached signing key provider, not Vault directly. It verifies that Auth's in-memory state is valid, not that Vault is reachable.

### Properties

| Property | Value | Description |
|----------|-------|-------------|
| `Name` | "Auth" | Identifies this contributor in health reports |
| `Priority` | 50 | Mid-range priority for health check ordering |
| `IsCritical` | true | Unhealthy status affects overall node health |

### Health Check Logic

```csharp
public async Task<(HealthStatus status, string? message)> CheckHealthAsync(
    CancellationToken cancellationToken)
{
    try
    {
        var signingKeys = await _keyProvider.GetSigningKeysAsync(cancellationToken);

        if (signingKeys.Count == 0)
        {
            return (HealthStatus.Unhealthy, "No signing keys available");
        }

        return (HealthStatus.Healthy, $"{signingKeys.Count} signing key(s) available");
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Auth health check failed");
        return (HealthStatus.Unhealthy, $"Failed to retrieve signing keys: {ex.Message}");
    }
}
```

### Health Status Mapping

| Condition | Status | Message |
|-----------|--------|---------|
| Keys available | Healthy | "2 signing key(s) available" |
| No keys | Unhealthy | "No signing keys available" |
| Exception | Unhealthy | "Failed to retrieve signing keys: {error}" |

### Registration

```csharp
// Registered automatically via AddHoneyDrunkAuth()
services.AddSingleton<IHealthContributor, AuthHealthContributor>();
```

[↑ Back to top](#table-of-contents)

---

## AuthReadinessContributor.cs

```csharp
public sealed class AuthReadinessContributor(
    ISigningKeyProvider keyProvider,
    ILogger<AuthReadinessContributor> logger) : IReadinessContributor
{
    public string Name => "Auth";
    public int Priority => 50;
    public bool IsRequired => true;
    
    public Task<(bool isReady, string? reason)> CheckReadinessAsync(
        CancellationToken cancellationToken);
}
```

### Purpose

Readiness contributor that checks if the Auth system is ready to process requests. Integrates with Kernel readiness aggregation for Kubernetes readiness probes.

> **Note:** This check reads from the cached signing key provider, not Vault directly. It verifies that Auth hasn't drifted out of a valid state since startup.

### Why Readiness Duplicates Startup Checks

The readiness contributor checks the same conditions as the startup hook (issuer, audience, signing keys). This is intentional:

- **Startup** enforces invariants once at boot
- **Readiness** continuously verifies those invariants remain satisfied

This catches edge cases where configuration could become invalid after startup (e.g., cache expiration, provider reinitialization).

### Properties

| Property | Value | Description |
|----------|-------|-------------|
| `Name` | "Auth" | Identifies this contributor in readiness reports |
| `Priority` | 50 | Mid-range priority for readiness check ordering |
| `IsRequired` | true | Not-ready status prevents traffic from being accepted |

### Readiness Check Logic

```csharp
public async Task<(bool isReady, string? reason)> CheckReadinessAsync(
    CancellationToken cancellationToken)
{
    try
    {
        var issuer = await _keyProvider.GetIssuerAsync(cancellationToken);
        var audience = await _keyProvider.GetAudienceAsync(cancellationToken);
        var signingKeys = await _keyProvider.GetSigningKeysAsync(cancellationToken);

        if (string.IsNullOrWhiteSpace(issuer) || 
            string.IsNullOrWhiteSpace(audience) || 
            signingKeys.Count == 0)
        {
            return (false, "Auth secrets not fully configured");
        }

        return (true, "Auth system ready");
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Auth readiness check failed");
        return (false, $"Failed to verify Auth secrets: {ex.Message}");
    }
}
```

### Readiness vs Health

| Aspect | Health | Readiness |
|--------|--------|-----------|
| Purpose | Is the system working? | Can it accept traffic? |
| Checks | Signing key availability | Full configuration |
| Kubernetes | Liveness probe | Readiness probe |
| Failure | Pod restart | Remove from load balancer |

### Registration

```csharp
// Registered automatically via AddHoneyDrunkAuth()
services.AddSingleton<IReadinessContributor, AuthReadinessContributor>();
```

[↑ Back to top](#table-of-contents)

---

## Kubernetes Integration

The lifecycle components integrate with Kubernetes probes via Kernel's health endpoints:

### Liveness Probe

```yaml
livenessProbe:
  httpGet:
    path: /health/live
    port: 8080
  initialDelaySeconds: 10
  periodSeconds: 30
```

The Auth health contributor reports to this endpoint. If signing keys become unavailable, the pod will be restarted.

### Readiness Probe

```yaml
readinessProbe:
  httpGet:
    path: /health/ready
    port: 8080
  initialDelaySeconds: 5
  periodSeconds: 10
```

The Auth readiness contributor reports to this endpoint. During startup or Vault issues, the pod is removed from the load balancer.

### Startup Probe

```yaml
startupProbe:
  httpGet:
    path: /health/ready
    port: 8080
  initialDelaySeconds: 0
  periodSeconds: 5
  failureThreshold: 30
```

The startup hook ensures secrets are validated before the application accepts traffic. Combined with the startup probe, this prevents traffic during initialization.

---

## Summary

The Lifecycle components ensure the Auth system operates reliably in production:

1. **AuthStartupHook** - Fail fast if secrets are missing (only Vault I/O point)
2. **AuthHealthContributor** - Report ongoing health from cached state
3. **AuthReadinessContributor** - Report readiness from cached state

Key design decisions:
- **Critical by default** - Auth failures affect overall node health
- **Explicit validation** - All required secrets checked at startup
- **No runtime Vault I/O** - Health/readiness reflect cached configuration
- **Kubernetes-native** - Integrates with standard probe patterns
- **Graceful degradation** - Readiness failures don't cause restarts

---

[← Back to File Guide](FILE_GUIDE.md) | [↑ Back to top](#table-of-contents)
