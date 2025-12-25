# 📈 Telemetry - OpenTelemetry Integration

[← Back to File Guide](FILE_GUIDE.md)

---

## Table of Contents

- [Overview](#overview)
- [Design Constraints](#design-constraints)
- [AuthTelemetry.cs](#authtelemetrycs)

---

## Overview

Telemetry constants for Auth operations. The Auth system integrates with OpenTelemetry via HoneyDrunk.Kernel's `ITelemetryActivityFactory` for distributed tracing.

**Location:** `HoneyDrunk.Auth/Telemetry/`

**Telemetry Integration:**

```
HTTP Request → Auth Middleware → BearerTokenAuthenticationProvider
                                        ↓
                                 ITelemetryActivityFactory.Start("authn.validate")
                                        ↓
                                 Activity with tags:
                                   - auth.scheme = "Bearer"
                                   - auth.result = "success" | "fail"
                                   - auth.failure_code = "TokenExpired" (on failure)
```

---

## Design Constraints

### Telemetry is Observational Only

Auth telemetry must never influence authentication or authorization decisions. Telemetry is:

- **Emitted via Kernel** - Auth uses `ITelemetryActivityFactory`, not direct `ActivitySource` access
- **Fire-and-forget** - Telemetry failures do not affect auth outcomes
- **Side-effect free** - No state changes, no conditional logic based on telemetry

> **Invariant:** Auth does not own exporter configuration. Exporter setup (OTLP, Jaeger, etc.) is the responsibility of the host application, not Auth.

### Cardinality is Intentionally Constrained

Auth telemetry deliberately excludes high-cardinality data:

| Excluded | Reason |
|----------|--------|
| Subject IDs | High cardinality; use logs for identity correlation |
| Tenant IDs | High cardinality; use logs or domain events |
| Resource identifiers | Unbounded; would explode trace storage |
| Claim values | Security risk; may contain sensitive data |
| Token metadata | Security risk; never emit token contents |

> **Rule:** High-cardinality data belongs in structured logs or domain events, not traces. Traces answer "what happened and did it succeed?" Logs answer "who and what specifically?"

[↑ Back to top](#table-of-contents)

---

## AuthTelemetry.cs

```csharp
public static class AuthTelemetry
{
    // Activity names
    public const string AuthenticateActivityName = "authn.validate";
    public const string AuthorizeActivityName = "authz.evaluate";
    
    // Result values
    public const string ResultSuccess = "success";
    public const string ResultFail = "fail";
    public const string ResultAllow = "allow";
    public const string ResultDeny = "deny";
    
    // Tag names
    public static class Tags
    {
        public const string Scheme = "auth.scheme";
        public const string Result = "auth.result";
        public const string FailureCode = "auth.failure_code";
        public const string AuthzResult = "authz.result";
        public const string Policy = "authz.policy";
        public const string AuthzFailureCode = "authz.failure_code";
    }
}
```

### Purpose

Defines telemetry constants for Auth operations. Provides consistent naming for activities and tags across the Auth system.

### Activity Names

| Constant | Value | Description |
|----------|-------|-------------|
| `AuthenticateActivityName` | "authn.validate" | Span for authentication operations |
| `AuthorizeActivityName` | "authz.evaluate" | Span for authorization operations |

### Result Values

| Constant | Value | Used For |
|----------|-------|----------|
| `ResultSuccess` | "success" | Successful authentication |
| `ResultFail` | "fail" | Failed authentication |
| `ResultAllow` | "allow" | Allowed authorization |
| `ResultDeny` | "deny" | Denied authorization |

### Tag Names

| Constant | Value | Description |
|----------|-------|-------------|
| `Tags.Scheme` | "auth.scheme" | Authentication scheme (e.g., "Bearer") |
| `Tags.Result` | "auth.result" | Authentication result |
| `Tags.FailureCode` | "auth.failure_code" | Authentication failure code |
| `Tags.AuthzResult` | "authz.result" | Authorization result |
| `Tags.Policy` | "authz.policy" | Authorization policy name |
| `Tags.AuthzFailureCode` | "authz.failure_code" | Authorization failure code |

### Usage in BearerTokenAuthenticationProvider

```csharp
public async Task<AuthenticationResult> AuthenticateAsync(
    AuthCredential credential,
    CancellationToken cancellationToken = default)
{
    using var activity = _telemetryFactory.Start(
        AuthTelemetry.AuthenticateActivityName,
        new Dictionary<string, object?>
        {
            [AuthTelemetry.Tags.Scheme] = credential.Scheme,
        });

    try
    {
        // Validate token...
        
        // On success
        activity?.SetTag(AuthTelemetry.Tags.Result, AuthTelemetry.ResultSuccess);
        return AuthenticationResult.Success(identity);
    }
    catch
    {
        // On failure
        activity?.SetTag(AuthTelemetry.Tags.Result, AuthTelemetry.ResultFail);
        activity?.SetTag(AuthTelemetry.Tags.FailureCode, code.ToString());
        return AuthenticationResult.Fail(code, message);
    }
}
```

### Usage in DefaultAuthorizationPolicy

```csharp
public Task<AuthorizationDecision> EvaluateAsync(
    AuthenticatedIdentity? identity,
    AuthorizationRequest request,
    CancellationToken cancellationToken = default)
{
    using var activity = _telemetryFactory.Start(
        AuthTelemetry.AuthorizeActivityName,
        new Dictionary<string, object?>
        {
            [AuthTelemetry.Tags.Policy] = PolicyName,
        });

    // Evaluate authorization...
    
    // Record decision
    activity?.SetTag(
        AuthTelemetry.Tags.AuthzResult, 
        decision.IsAllowed ? AuthTelemetry.ResultAllow : AuthTelemetry.ResultDeny);
    
    if (!decision.IsAllowed && decision.DenyReasons.Count > 0)
    {
        activity?.SetTag(
            AuthTelemetry.Tags.AuthzFailureCode, 
            decision.DenyReasons[0].Code.ToString());
    }

    return Task.FromResult(decision);
}
```

### Trace Example

A typical authentication trace in a tracing UI (e.g., Jaeger, Zipkin):

```
HTTP POST /api/orders
├── authn.validate [2ms]
│   ├── auth.scheme: Bearer
│   └── auth.result: success
├── authz.evaluate [1ms]
│   ├── authz.policy: Default
│   └── authz.result: allow
└── OrderService.CreateOrder [50ms]
```

Failed authentication trace:

```
HTTP GET /api/admin/users
└── authn.validate [3ms]
    ├── auth.scheme: Bearer
    ├── auth.result: fail
    └── auth.failure_code: TokenExpired
```

### OpenTelemetry Configuration

To export Auth telemetry, configure OpenTelemetry in your **host application** (not in Auth itself):

```csharp
// In your application's Program.cs
builder.Services.AddOpenTelemetry()
    .WithTracing(tracing => tracing
        .AddSource("HoneyDrunk.Kernel")  // Kernel owns the ActivitySource
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddOtlpExporter());
```

> **Note:** Auth emits activities through Kernel's `ITelemetryActivityFactory`. The `ActivitySource` is owned by Kernel, not Auth. Do not configure exporters inside Auth libraries.

### Custom Metrics (Future)

The telemetry constants are designed to support future metrics:

```csharp
// Potential future metrics
counter.Add(1, new TagList
{
    { AuthTelemetry.Tags.Scheme, "Bearer" },
    { AuthTelemetry.Tags.Result, AuthTelemetry.ResultSuccess }
});
```

[↑ Back to top](#table-of-contents)

---

## Summary

The Telemetry components provide consistent observability for Auth operations:

- **Activity names** follow the `domain.action` convention
- **Tag names** follow the `domain.attribute` convention
- **Result values** are human-readable and searchable
- **Cardinality** is constrained to prevent trace explosion

Key benefits:
- **Distributed tracing** - See auth decisions in trace views
- **Consistent naming** - Easy to query and filter
- **Failure visibility** - Specific failure codes in traces
- **Audit trail** - Policy names and decisions recorded
- **Security safe** - No sensitive data in traces

---

[← Back to File Guide](FILE_GUIDE.md) | [↑ Back to top](#table-of-contents)
