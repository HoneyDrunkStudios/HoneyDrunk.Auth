# 🔌 Dependency Injection - Service Registration

[← Back to File Guide](FILE_GUIDE.md)

---

## Table of Contents

- [Overview](#overview)
- [Registration Patterns](#registration-patterns)
- [HoneyDrunkAuthServiceCollectionExtensions.cs](#honeydrunkauthorizationservicecollectionextensionscs)
- [HoneyDrunkAuthBuilderExtensions.cs](#honeydrunkauthorizationbuilderextensionscs)
- [HoneyDrunkAuthAspNetCoreServiceCollectionExtensions.cs](#honeydrunkauthorizationaspnetcoreservicecollectionextensionscs)
- [HoneyDrunkAuthApplicationBuilderExtensions.cs](#honeydrunkauthorizationapplicationbuilderextensionscs)

---

## Overview

Dependency injection extensions for registering HoneyDrunk.Auth services. Provides fluent APIs for both direct service collection registration and HoneyDrunk builder patterns.

**Locations:**
- `HoneyDrunk.Auth/DependencyInjection/`
- `HoneyDrunk.Auth.AspNetCore/DependencyInjection/`

**Registration Options:**

| Method | Package | Description |
|--------|---------|-------------|
| `AddHoneyDrunkAuth()` | HoneyDrunk.Auth | Core services only |
| `AddAuth()` | HoneyDrunk.Auth | Via IHoneyDrunkBuilder |
| `AddHoneyDrunkAuthAspNetCore()` | HoneyDrunk.Auth.AspNetCore | Core + ASP.NET Core |
| `UseHoneyDrunkAuth()` | HoneyDrunk.Auth.AspNetCore | Middleware registration |

---

## Registration Patterns

### Choose One Approach

There are two registration patterns. Choose **one**, not both:

| Pattern | When to Use | Example |
|---------|-------------|---------|
| **Builder pattern** | Grid services (Kernel + Vault + Auth) | `.AddHoneyDrunkNode().AddVault().AddAuth()` |
| **Direct ASP.NET Core** | ASP.NET Core apps | `AddHoneyDrunkAuthAspNetCore()` |

> **Note:** Both patterns internally call `AddHoneyDrunkAuth()`, and `TryAdd` prevents duplicate registrations. However, mixing patterns in the same codebase creates confusion. Pick one style and use it consistently.

### Singleton Lifetimes Are Intentional

All Auth services are registered as **singletons**. This is deliberate:

| Reason | Explanation |
|--------|-------------|
| **Stateless** | Auth services have no per-request state |
| **Thread-safe** | All implementations are thread-safe by design |
| **Configuration-driven** | Secrets loaded once at startup, reused for all requests |
| **Performance** | No per-request allocation overhead |

> **Warning:** Do not change Auth services to scoped lifetime "for HttpContext access." The `HttpContextIdentityAccessor` is specifically designed to access `IHttpContextAccessor` from a singleton. Scoped Auth services would break Kernel lifecycle integration and create subtle bugs.

### Policy Override Semantics

When overriding `IAuthorizationPolicy`:

- **Only one policy is active** - There is no automatic policy chaining
- **Register before `AddHoneyDrunkAuth()`** - Uses `TryAddSingleton`, so first registration wins
- **Full replacement** - Your policy replaces `DefaultAuthorizationPolicy` entirely

If you need multiple policies, implement composition within your custom policy.

[↑ Back to top](#table-of-contents)

---

## HoneyDrunkAuthServiceCollectionExtensions.cs

**Location:** `HoneyDrunk.Auth/DependencyInjection/`

```csharp
public static class HoneyDrunkAuthServiceCollectionExtensions
{
    public static IServiceCollection AddHoneyDrunkAuth(this IServiceCollection services);
}
```

### Purpose

Registers core HoneyDrunk.Auth services to the service collection. This is the primary registration method for the Auth system.

### Registered Services

```csharp
public static IServiceCollection AddHoneyDrunkAuth(this IServiceCollection services)
{
    ArgumentNullException.ThrowIfNull(services);

    // Vault-backed key provider
    services.TryAddSingleton<ISigningKeyProvider, VaultSigningKeyProvider>();

    // Authentication
    services.TryAddSingleton<IAuthenticationProvider, BearerTokenAuthenticationProvider>();

    // Authorization
    services.TryAddSingleton<IAuthorizationPolicy, DefaultAuthorizationPolicy>();

    // Lifecycle
    services.AddSingleton<IStartupHook, AuthStartupHook>();
    services.AddSingleton<IHealthContributor, AuthHealthContributor>();
    services.AddSingleton<IReadinessContributor, AuthReadinessContributor>();

    return services;
}
```

### Service Registration Details

| Service | Implementation | Method | Lifetime |
|---------|---------------|--------|----------|
| `ISigningKeyProvider` | `VaultSigningKeyProvider` | `TryAddSingleton` | Singleton |
| `IAuthenticationProvider` | `BearerTokenAuthenticationProvider` | `TryAddSingleton` | Singleton |
| `IAuthorizationPolicy` | `DefaultAuthorizationPolicy` | `TryAddSingleton` | Singleton |
| `IStartupHook` | `AuthStartupHook` | `AddSingleton` | Singleton |
| `IHealthContributor` | `AuthHealthContributor` | `AddSingleton` | Singleton |
| `IReadinessContributor` | `AuthReadinessContributor` | `AddSingleton` | Singleton |

### TryAdd vs Add

- **TryAddSingleton** - Allows overriding with custom implementations
- **AddSingleton** - Always adds (multiple hooks/contributors are collected)

### Usage

```csharp
// Direct registration
builder.Services.AddHoneyDrunkAuth();

// Custom signing key provider
builder.Services.AddSingleton<ISigningKeyProvider, CustomSigningKeyProvider>();
builder.Services.AddHoneyDrunkAuth();  // Won't override custom provider
```

### Prerequisites

The following services must be registered before calling `AddHoneyDrunkAuth()`:

| Prerequisite | Source |
|-------------|--------|
| `ISecretStore` | HoneyDrunk.Vault |
| `IVaultClient` | HoneyDrunk.Vault |
| `ITelemetryActivityFactory` | HoneyDrunk.Kernel |
| `ILogger<T>` | Microsoft.Extensions.Logging |

[↑ Back to top](#table-of-contents)

---

## HoneyDrunkAuthBuilderExtensions.cs

**Location:** `HoneyDrunk.Auth/DependencyInjection/`

```csharp
public static class HoneyDrunkAuthBuilderExtensions
{
    public static IHoneyDrunkBuilder AddAuth(this IHoneyDrunkBuilder builder);
}
```

### Purpose

Extension method for `IHoneyDrunkBuilder` to add Auth services in a fluent builder pattern.

### Implementation

```csharp
public static IHoneyDrunkBuilder AddAuth(this IHoneyDrunkBuilder builder)
{
    ArgumentNullException.ThrowIfNull(builder);
    builder.Services.AddHoneyDrunkAuth();
    return builder;
}
```

### Usage

```csharp
builder.Services
    .AddHoneyDrunkNode(opts => { /* ... */ })
    .AddVault(opts => { /* ... */ })
    .AddAuth();  // Fluent chaining
```

### Builder Pattern Benefits

- **Fluent API** - Chain multiple registrations
- **Consistent style** - Matches Kernel and Vault patterns
- **Discoverability** - Extensions appear on the builder

[↑ Back to top](#table-of-contents)

---

## HoneyDrunkAuthAspNetCoreServiceCollectionExtensions.cs

**Location:** `HoneyDrunk.Auth.AspNetCore/DependencyInjection/`

```csharp
public static class HoneyDrunkAuthAspNetCoreServiceCollectionExtensions
{
    public static IServiceCollection AddHoneyDrunkAuthAspNetCore(this IServiceCollection services);
}
```

### Purpose

Registers HoneyDrunk.Auth services plus ASP.NET Core integration components.

### Implementation

```csharp
public static IServiceCollection AddHoneyDrunkAuthAspNetCore(this IServiceCollection services)
{
    ArgumentNullException.ThrowIfNull(services);

    // Add core Auth services
    services.AddHoneyDrunkAuth();

    // Add HTTP context accessor if not already registered
    services.TryAddSingleton<IHttpContextAccessor, HttpContextAccessor>();

    // Add identity accessor
    services.TryAddSingleton<IAuthenticatedIdentityAccessor, HttpContextIdentityAccessor>();

    return services;
}
```

### Additional Services

| Service | Implementation | Method |
|---------|---------------|--------|
| `IHttpContextAccessor` | `HttpContextAccessor` | `TryAddSingleton` |
| `IAuthenticatedIdentityAccessor` | `HttpContextIdentityAccessor` | `TryAddSingleton` |

### Usage

```csharp
// For ASP.NET Core applications
builder.Services.AddHoneyDrunkAuthAspNetCore();

// Equivalent to:
builder.Services.AddHoneyDrunkAuth();
builder.Services.TryAddSingleton<IHttpContextAccessor, HttpContextAccessor>();
builder.Services.TryAddSingleton<IAuthenticatedIdentityAccessor, HttpContextIdentityAccessor>();
```

### When to Use Which

| Scenario | Method |
|----------|--------|
| Console app, background service | `AddHoneyDrunkAuth()` |
| ASP.NET Core web API | `AddHoneyDrunkAuthAspNetCore()` |
| ASP.NET Core with custom accessor | `AddHoneyDrunkAuth()` + custom registration |

[↑ Back to top](#table-of-contents)

---

## HoneyDrunkAuthApplicationBuilderExtensions.cs

**Location:** `HoneyDrunk.Auth.AspNetCore/DependencyInjection/`

```csharp
public static class HoneyDrunkAuthApplicationBuilderExtensions
{
    public static IApplicationBuilder UseHoneyDrunkAuth(this IApplicationBuilder app);
}
```

### Purpose

Adds the HoneyDrunk.Auth middleware to the ASP.NET Core request pipeline.

### Implementation

```csharp
public static IApplicationBuilder UseHoneyDrunkAuth(this IApplicationBuilder app)
{
    ArgumentNullException.ThrowIfNull(app);
    return app.UseMiddleware<HoneyDrunkAuthMiddleware>();
}
```

### Pipeline Position

```csharp
var app = builder.Build();

// Routing (if using controllers)
app.UseRouting();

// Grid context (if using Kernel)
app.UseGridContext();

// Authentication - BEFORE authorization
app.UseHoneyDrunkAuth();

// ASP.NET Core authorization (optional)
app.UseAuthorization();

// Endpoints
app.MapControllers();

app.Run();
```

### Middleware Order

| Position | Middleware | Purpose |
|----------|------------|---------|
| 1 | UseRouting | Route matching |
| 2 | UseGridContext | Grid context propagation |
| 3 | **UseHoneyDrunkAuth** | Bearer token authentication |
| 4 | UseAuthorization | ASP.NET Core authorization |
| 5 | MapControllers | Endpoint execution |

[↑ Back to top](#table-of-contents)

---

## Complete Registration Example

### Minimal API (Builder Pattern)

```csharp
var builder = WebApplication.CreateBuilder(args);

// Use builder pattern for Grid services
builder.Services
    .AddHoneyDrunkNode(opts =>
    {
        opts.NodeId = "api-node";
        opts.Name = "API Service";
    })
    .AddVault(opts =>
    {
        opts.Address = builder.Configuration["Vault:Address"];
        opts.Token = builder.Configuration["Vault:Token"];
    })
    .AddAuth();

// Add ASP.NET Core-specific services (identity accessor, etc.)
builder.Services.TryAddSingleton<IHttpContextAccessor, HttpContextAccessor>();
builder.Services.TryAddSingleton<IAuthenticatedIdentityAccessor, HttpContextIdentityAccessor>();

var app = builder.Build();

// Configure pipeline
app.UseGridContext();
app.UseHoneyDrunkAuth();

// Define endpoints
app.MapGet("/protected", (IAuthenticatedIdentityAccessor identity) =>
{
    if (!identity.IsAuthenticated)
        return Results.Unauthorized();
    
    return Results.Ok($"Hello, {identity.Identity!.DisplayName}!");
});

app.Run();
```

### Minimal API (Direct ASP.NET Core Pattern)

```csharp
var builder = WebApplication.CreateBuilder(args);

// Register Kernel and Vault first (required)
builder.Services.AddHoneyDrunkNode(opts => { /* ... */ });
builder.Services.AddVault(opts => { /* ... */ });

// Use direct ASP.NET Core registration (includes core Auth + ASP.NET Core services)
builder.Services.AddHoneyDrunkAuthAspNetCore();

var app = builder.Build();

// Configure pipeline
app.UseGridContext();
app.UseHoneyDrunkAuth();

app.MapGet("/protected", (IAuthenticatedIdentityAccessor identity) =>
{
    if (!identity.IsAuthenticated)
        return Results.Unauthorized();
    
    return Results.Ok($"Hello, {identity.Identity!.DisplayName}!");
});

app.Run();
```

### Controller-Based API

```csharp
var builder = WebApplication.CreateBuilder(args);

// Register services
builder.Services.AddControllers();
builder.Services
    .AddHoneyDrunkNode(opts => { /* ... */ })
    .AddVault(opts => { /* ... */ });
builder.Services.AddHoneyDrunkAuthAspNetCore();

var app = builder.Build();

// Configure pipeline
app.UseRouting();
app.UseGridContext();
app.UseHoneyDrunkAuth();
app.UseAuthorization();
app.MapControllers();

app.Run();
```

### Custom Implementations

```csharp
// Register custom signing key provider before AddHoneyDrunkAuth
builder.Services.AddSingleton<ISigningKeyProvider, AzureKeyVaultSigningKeyProvider>();

// Register custom authorization policy
builder.Services.AddSingleton<IAuthorizationPolicy, TenantAwareAuthorizationPolicy>();

// Now register Auth (TryAdd won't override)
builder.Services.AddHoneyDrunkAuth();
```

---

## Summary

The DI extensions provide flexible, consistent registration patterns:

1. **Core services** via `AddHoneyDrunkAuth()`
2. **Builder pattern** via `AddAuth()` on `IHoneyDrunkBuilder`
3. **ASP.NET Core** via `AddHoneyDrunkAuthAspNetCore()`
4. **Middleware** via `UseHoneyDrunkAuth()`

Key design decisions:
- **TryAdd for overridability** - Core services can be replaced
- **Add for collectors** - Multiple hooks/contributors are collected
- **Singleton lifetime** - All services are singletons by design
- **Single policy** - One authorization policy active at a time
- **Fluent chaining** - Builder pattern support
- **Framework conventions** - Follows ASP.NET Core patterns

---

[← Back to File Guide](FILE_GUIDE.md) | [↑ Back to top](#table-of-contents)
