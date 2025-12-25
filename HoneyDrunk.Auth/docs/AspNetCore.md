# 🌐 ASP.NET Core - Web Framework Integration

[← Back to File Guide](FILE_GUIDE.md)

---

## Table of Contents

- [Overview](#overview)
- [Design Constraints](#design-constraints)
- [HoneyDrunkAuthMiddleware.cs](#honeydrunkauthmiddlewarecs)
- [IAuthenticatedIdentityAccessor.cs](#iauthenticatedidentityaccessorcs)
- [HttpContextIdentityAccessor.cs](#httpcontextidentityaccessorcs)
- [AuthorizationEndpointExtensions.cs](#authorizationendpointextensionscs)
- [Service Registration](#service-registration)

---

## Overview

ASP.NET Core integration for HoneyDrunk.Auth. Provides middleware for automatic Bearer token authentication, identity accessors for clean dependency injection, and extension methods for endpoint authorization.

**Location:** `HoneyDrunk.Auth.AspNetCore/`

**Request Pipeline Flow:**

```
HTTP Request
     ↓
UseGridContext()          → Propagate Grid context
     ↓
UseHoneyDrunkAuth()       → Extract & validate Bearer token
     ↓                      Store identity in HttpContext.Items
     ↓                      Set HttpContext.User for compatibility
     ↓
Endpoint Handler          → Access identity via IAuthenticatedIdentityAccessor
     ↓                      Or use AuthorizeAsync/AuthorizeOrForbidAsync
     ↓
HTTP Response
```

---

## Design Constraints

### Non-Blocking Authentication

The middleware is **intentionally non-blocking**:

- Missing tokens do not prevent request processing
- Invalid tokens do not short-circuit the pipeline
- Public endpoints work naturally without special configuration

> **Warning:** Do not "harden" this by rejecting unauthenticated requests globally in middleware. Authentication populates identity; authorization decides access. This separation is by design.

### AuthenticatedIdentity is the Source of Truth

The middleware stores identity in two places:

| Location | Purpose | Authority |
|----------|---------|-----------|
| `HttpContext.Items` | Auth-native access via `IAuthenticatedIdentityAccessor` | ✅ Source of truth |
| `HttpContext.User` | ASP.NET Core compatibility (`ClaimsPrincipal`) | ❌ Derived projection |

> **Rule:** `ClaimsPrincipal` is a compatibility projection, never authoritative. Domain code should use `IAuthenticatedIdentityAccessor`, not `HttpContext.User`. Do not mutate `HttpContext.User` expecting Auth to reflect changes.

### Single Policy Model

Authorization extensions resolve a single `IAuthorizationPolicy` from DI:

- No automatic policy chaining
- No attribute-based policy selection
- Applications needing multiple policies should implement composition within their custom policy

This matches the core Auth architecture where one policy is active at a time.

[↑ Back to top](#table-of-contents)

---

## HoneyDrunkAuthMiddleware.cs

```csharp
public sealed class HoneyDrunkAuthMiddleware
{
    public HoneyDrunkAuthMiddleware(
        RequestDelegate next,
        ILogger<HoneyDrunkAuthMiddleware> logger);
    
    public Task InvokeAsync(
        HttpContext context,
        IAuthenticationProvider authProvider);
}
```

### Purpose

Middleware that authenticates requests using Bearer tokens. Reads the `Authorization` header, validates the token, and sets the authenticated identity for downstream access.

### Authentication Flow

1. **Extract Header**: Read `Authorization` header
2. **Check Prefix**: Verify `Bearer ` prefix
3. **Extract Token**: Get token string after prefix
4. **Validate Token**: Call `IAuthenticationProvider.AuthenticateAsync`
5. **Store Identity**: Set in `HttpContext.Items` and `HttpContext.User`

### Code Flow

```csharp
public async Task InvokeAsync(HttpContext context, IAuthenticationProvider authProvider)
{
    var authorizationHeader = context.Request.Headers[HeaderNames.Authorization].ToString();

    // No token - continue without authentication
    if (string.IsNullOrEmpty(authorizationHeader) ||
        !authorizationHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
    {
        await _next(context);
        return;
    }

    var token = authorizationHeader["Bearer ".Length..].Trim();
    
    if (string.IsNullOrEmpty(token))
    {
        await _next(context);
        return;
    }

    var credential = AuthCredential.Bearer(token);
    var result = await authProvider.AuthenticateAsync(credential, context.RequestAborted);

    if (result.IsAuthenticated && result.Identity is not null)
    {
        // Store for IAuthenticatedIdentityAccessor
        context.Items[HttpContextIdentityAccessor.IdentityKey] = result.Identity;
        
        // Set for ASP.NET Core compatibility
        context.User = CreateClaimsPrincipal(result.Identity);
    }

    await _next(context);
}
```

### ClaimsPrincipal Mapping

The middleware maps `AuthenticatedIdentity` to `ClaimsPrincipal` for ASP.NET Core compatibility:

```csharp
private static ClaimsPrincipal CreateClaimsPrincipal(AuthenticatedIdentity identity)
{
    var claims = new List<Claim>
    {
        new(ClaimTypes.NameIdentifier, identity.SubjectId),
    };

    if (!string.IsNullOrEmpty(identity.DisplayName))
    {
        claims.Add(new Claim(ClaimTypes.Name, identity.DisplayName));
    }

    foreach (var claim in identity.Claims)
    {
        foreach (var value in claim.Value)
        {
            // Map standard claim types
            var claimType = claim.Key switch
            {
                AuthClaimTypes.Email => ClaimTypes.Email,
                AuthClaimTypes.Role => ClaimTypes.Role,
                _ => claim.Key,
            };

            claims.Add(new Claim(claimType, value));
        }
    }

    var claimsIdentity = new ClaimsIdentity(claims, identity.Scheme);
    return new ClaimsPrincipal(claimsIdentity);
}
```

### Pipeline Position

```csharp
var app = builder.Build();

app.UseGridContext();      // Grid context first
app.UseHoneyDrunkAuth();   // Auth middleware
app.UseAuthorization();    // Optional: ASP.NET Core authorization

app.MapControllers();
app.Run();
```

### Registration

```csharp
// In Program.cs or Startup.cs
app.UseHoneyDrunkAuth();

// Extension method in HoneyDrunkAuthApplicationBuilderExtensions
public static IApplicationBuilder UseHoneyDrunkAuth(this IApplicationBuilder app)
{
    return app.UseMiddleware<HoneyDrunkAuthMiddleware>();
}
```

[↑ Back to top](#table-of-contents)

---

## IAuthenticatedIdentityAccessor.cs

```csharp
public interface IAuthenticatedIdentityAccessor
{
    AuthenticatedIdentity? Identity { get; }
    bool IsAuthenticated { get; }
}
```

### Purpose

Provides access to the current authenticated identity. Allows clean dependency injection of identity access in services and handlers.

### Properties

| Property | Type | Description |
|----------|------|-------------|
| `Identity` | `AuthenticatedIdentity?` | The current identity, or null if not authenticated |
| `IsAuthenticated` | `bool` | True if an identity is available |

### Usage in Minimal APIs

```csharp
app.MapGet("/profile", (IAuthenticatedIdentityAccessor identity) =>
{
    if (!identity.IsAuthenticated)
        return Results.Unauthorized();

    return Results.Ok(new
    {
        identity.Identity!.SubjectId,
        identity.Identity.DisplayName,
        Roles = identity.Identity.GetClaimValues(AuthClaimTypes.Role)
    });
});
```

### Usage in Services

```csharp
public class OrderService(
    IAuthenticatedIdentityAccessor identityAccessor,
    IOrderRepository repository)
{
    public async Task<Order> CreateOrderAsync(CreateOrderRequest request, CancellationToken ct)
    {
        if (!identityAccessor.IsAuthenticated)
            throw new UnauthorizedAccessException("Authentication required");
        
        var order = new Order
        {
            CustomerId = identityAccessor.Identity!.SubjectId,
            Items = request.Items
        };
        
        return await repository.SaveAsync(order, ct);
    }
}
```

### Usage in Controllers

```csharp
[ApiController]
[Route("api/[controller]")]
public class ProfileController(IAuthenticatedIdentityAccessor identity) : ControllerBase
{
    [HttpGet]
    public IActionResult GetProfile()
    {
        if (!identity.IsAuthenticated)
            return Unauthorized();
        
        return Ok(new
        {
            SubjectId = identity.Identity!.SubjectId,
            Name = identity.Identity.DisplayName
        });
    }
}
```

[↑ Back to top](#table-of-contents)

---

## HttpContextIdentityAccessor.cs

```csharp
public sealed class HttpContextIdentityAccessor : IAuthenticatedIdentityAccessor
{
    public const string IdentityKey = "HoneyDrunk.Auth.Identity";
    
    public HttpContextIdentityAccessor(IHttpContextAccessor httpContextAccessor);
    
    public AuthenticatedIdentity? Identity { get; }
    public bool IsAuthenticated { get; }
}
```

### Purpose

Provides access to the authenticated identity via `HttpContext.Items`. This is the default implementation of `IAuthenticatedIdentityAccessor` for ASP.NET Core.

### Implementation

```csharp
public AuthenticatedIdentity? Identity
{
    get
    {
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext is null)
            return null;

        return httpContext.Items.TryGetValue(IdentityKey, out var identity)
            ? identity as AuthenticatedIdentity
            : null;
    }
}

public bool IsAuthenticated => Identity is not null;
```

### Thread Safety

The accessor is request-scoped via `IHttpContextAccessor`, so each request gets its own identity. The implementation is thread-safe for the async request context.

### Registration

```csharp
// Registered automatically via AddHoneyDrunkAuthAspNetCore()
services.AddHoneyDrunkAuthAspNetCore();

// Or register manually
services.TryAddSingleton<IHttpContextAccessor, HttpContextAccessor>();
services.TryAddSingleton<IAuthenticatedIdentityAccessor, HttpContextIdentityAccessor>();
```

[↑ Back to top](#table-of-contents)

---

## AuthorizationEndpointExtensions.cs

```csharp
public static class AuthorizationEndpointExtensions
{
    public static Task<AuthorizationDecision> AuthorizeAsync(
        this HttpContext context,
        AuthorizationRequest request,
        CancellationToken cancellationToken = default);
    
    public static Task<bool> AuthorizeOrForbidAsync(
        this HttpContext context,
        AuthorizationRequest request,
        CancellationToken cancellationToken = default);
    
    public static bool RequireAuthentication(this HttpContext context);
}
```

### Purpose

Extension methods for authorization in endpoint handlers. Provides convenient methods to check authorization and return appropriate HTTP responses.

> **Note:** These extensions resolve `IAuthorizationPolicy` from DI on each call. There is no policy caching or chaining—each call evaluates the single registered policy against the current identity and request.

### AuthorizeAsync

Evaluates an authorization request and returns the decision:

```csharp
app.MapPost("/projects", async (HttpContext ctx, CreateProjectRequest request) =>
{
    var authzRequest = new AuthorizationRequest(
        action: "create",
        resource: "projects",
        requiredScopes: ["projects:write"]);

    var decision = await ctx.AuthorizeAsync(authzRequest);
    
    if (!decision.IsAllowed)
    {
        // Custom handling of deny reasons
        return Results.Json(new
        {
            error = "Forbidden",
            reasons = decision.DenyReasons.Select(r => r.Message)
        }, statusCode: 403);
    }
    
    // Proceed with creation
    return Results.Created($"/projects/{newId}", project);
});
```

### AuthorizeOrForbidAsync

Evaluates authorization and automatically returns 403 if denied:

```csharp
app.MapDelete("/users/{id}", async (HttpContext ctx, string id) =>
{
    var request = new AuthorizationRequest(
        action: "delete",
        resource: $"users/{id}",
        requiredRoles: ["admin"]);

    if (!await ctx.AuthorizeOrForbidAsync(request))
        return Results.Empty;  // 403 already sent

    await DeleteUserAsync(id);
    return Results.NoContent();
});
```

### RequireAuthentication

Ensures the request is authenticated, returning 401 if not:

```csharp
app.MapGet("/protected", (HttpContext ctx) =>
{
    if (!ctx.RequireAuthentication())
        return Results.Empty;  // 401 already sent
    
    return Results.Ok("You are authenticated!");
});
```

### Implementation Details

```csharp
public static async Task<AuthorizationDecision> AuthorizeAsync(
    this HttpContext context,
    AuthorizationRequest request,
    CancellationToken cancellationToken = default)
{
    var policy = context.RequestServices.GetRequiredService<IAuthorizationPolicy>();
    var identityAccessor = context.RequestServices.GetRequiredService<IAuthenticatedIdentityAccessor>();

    return await policy.EvaluateAsync(identityAccessor.Identity, request, cancellationToken);
}

public static async Task<bool> AuthorizeOrForbidAsync(
    this HttpContext context,
    AuthorizationRequest request,
    CancellationToken cancellationToken = default)
{
    var decision = await context.AuthorizeAsync(request, cancellationToken);

    if (decision.IsAllowed)
        return true;

    context.Response.StatusCode = StatusCodes.Status403Forbidden;
    return false;
}

public static bool RequireAuthentication(this HttpContext context)
{
    var identityAccessor = context.RequestServices.GetRequiredService<IAuthenticatedIdentityAccessor>();

    if (identityAccessor.IsAuthenticated)
        return true;

    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
    return false;
}
```

[↑ Back to top](#table-of-contents)

---

## Service Registration

### HoneyDrunkAuthAspNetCoreServiceCollectionExtensions

```csharp
public static IServiceCollection AddHoneyDrunkAuthAspNetCore(this IServiceCollection services)
{
    // Add core Auth services
    services.AddHoneyDrunkAuth();

    // Add HTTP context accessor if not already registered
    services.TryAddSingleton<IHttpContextAccessor, HttpContextAccessor>();

    // Add identity accessor
    services.TryAddSingleton<IAuthenticatedIdentityAccessor, HttpContextIdentityAccessor>();

    return services;
}
```

### Complete Registration Example

```csharp
var builder = WebApplication.CreateBuilder(args);

// 1. Register Kernel
builder.Services.AddHoneyDrunkNode(opts => { /* ... */ });


// 2. Register Vault
builder.Services.AddVault(opts => { /* ... */ });


// 3. Register Auth with ASP.NET Core integration
builder.Services.AddHoneyDrunkAuthAspNetCore();

var app = builder.Build();

// 4. Configure middleware
app.UseGridContext();
app.UseHoneyDrunkAuth();

// 5. Define endpoints
app.MapGet("/", () => "Hello World!");

app.Run();
```

### What Gets Registered

| Service | Implementation | Lifetime |
|---------|---------------|----------|
| `ISigningKeyProvider` | `VaultSigningKeyProvider` | Singleton |
| `IAuthenticationProvider` | `BearerTokenAuthenticationProvider` | Singleton |
| `IAuthorizationPolicy` | `DefaultAuthorizationPolicy` | Singleton |
| `IStartupHook` | `AuthStartupHook` | Singleton |
| `IHealthContributor` | `AuthHealthContributor` | Singleton |
| `IReadinessContributor` | `AuthReadinessContributor` | Singleton |
| `IHttpContextAccessor` | `HttpContextAccessor` | Singleton |
| `IAuthenticatedIdentityAccessor` | `HttpContextIdentityAccessor` | Singleton |

[↑ Back to top](#table-of-contents)

---

## Summary

The ASP.NET Core integration provides a clean, idiomatic way to use HoneyDrunk.Auth in web applications:

1. **Middleware** - Automatic Bearer token authentication
2. **Identity Accessor** - Clean DI for identity access
3. **Extension Methods** - Convenient authorization helpers
4. **Service Registration** - One-line setup

Key design decisions:
- **Non-blocking authentication** - Missing tokens don't prevent request processing (by design)
- **Dual identity storage** - Both `HttpContext.Items` (authoritative) and `HttpContext.User` (compatibility)
- **Single policy model** - Extensions use the one registered policy, no chaining
- **Extension methods** - Fluent API for authorization checks
- **ASP.NET Core compatibility** - Works with existing authorization middleware
- **Auth-native preferred** - Steer domain code toward `IAuthenticatedIdentityAccessor`, not `ClaimsPrincipal`

---

[← Back to File Guide](FILE_GUIDE.md) | [↑ Back to top](#table-of-contents)
