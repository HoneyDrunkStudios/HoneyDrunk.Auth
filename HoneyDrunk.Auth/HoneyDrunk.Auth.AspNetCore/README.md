# HoneyDrunk.Auth.AspNetCore

[![NuGet](https://img.shields.io/nuget/v/HoneyDrunk.Auth.AspNetCore.svg)](https://www.nuget.org/packages/HoneyDrunk.Auth.AspNetCore)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)
[![.NET 10](https://img.shields.io/badge/.NET-10.0-512BD4)](https://dotnet.microsoft.com/download/dotnet/10.0)

> **ASP.NET Core integration for HoneyDrunk.Auth** - Middleware, identity accessors, and authorization endpoint helpers.

## 🌐 What Is This?

This package provides middleware and extensions for integrating HoneyDrunk.Auth with ASP.NET Core applications. It includes authentication middleware that automatically validates Bearer tokens, HttpContext-based identity accessors, and convenient authorization helpers for endpoints.

## 📦 Installation

```sh
dotnet add package HoneyDrunk.Auth.AspNetCore
```

```xml
<PackageReference Include="HoneyDrunk.Auth.AspNetCore" Version="0.1.0" />
```

## 🚀 Quick Start

### 1. Register Services

```csharp
builder.Services.AddHoneyDrunkAuthAspNetCore();
```

### 2. Configure Middleware

```csharp
app.UseGridContext();      // Grid context propagation
app.UseHoneyDrunkAuth();   // Authentication middleware
```

### 3. Access Identity

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

### 4. Authorize Requests

```csharp
app.MapPost("/admin/action", async (HttpContext ctx) =>
{
    var request = new AuthorizationRequest(
        action: "admin",
        resource: "system",
        requiredRoles: ["admin"]);

    if (!await ctx.AuthorizeOrForbidAsync(request))
        return Results.Empty;  // 403 already sent

    // Authorized - perform action
    return Results.Ok();
});
```

## 🔧 Key Components

### Middleware

| Component | Description |
|-----------|-------------|
| `HoneyDrunkAuthMiddleware` | Validates Bearer tokens and sets identity on HttpContext |
| `UseHoneyDrunkAuth()` | Extension method to add middleware to pipeline |

### Identity Access

| Component | Description |
|-----------|-------------|
| `IAuthenticatedIdentityAccessor` | Interface to access current authenticated identity |
| `HttpContextIdentityAccessor` | HttpContext-based implementation |

### Authorization Helpers

| Method | Description |
|--------|-------------|
| `AuthorizeAsync()` | Evaluates authorization, returns decision |
| `AuthorizeOrForbidAsync()` | Evaluates authorization, sends 403 if denied |
| `RequireAuthentication()` | Returns 401 if not authenticated |

## 💡 Usage Examples

### Check Authentication

```csharp
app.MapGet("/me", (IAuthenticatedIdentityAccessor identity) =>
{
    if (!identity.IsAuthenticated)
        return Results.Unauthorized();

    return Results.Ok(new { identity.Identity!.SubjectId });
});
```

### Role-Based Authorization

```csharp
app.MapDelete("/users/{id}", async (HttpContext ctx, string id) =>
{
    var request = new AuthorizationRequest(
        action: "delete",
        resource: $"users/{id}",
        requiredRoles: ["admin"]);

    if (!await ctx.AuthorizeOrForbidAsync(request))
        return Results.Empty;

    // Delete user...
    return Results.NoContent();
});
```

### Scope-Based Authorization

```csharp
app.MapPost("/documents", async (HttpContext ctx) =>
{
    var request = new AuthorizationRequest(
        action: "create",
        resource: "documents",
        requiredScopes: ["documents:write"]);

    var decision = await ctx.AuthorizeAsync(request);
    
    if (!decision.IsAllowed)
    {
        return Results.Json(new
        {
            error = "Forbidden",
            reasons = decision.DenyReasons.Select(r => r.Message)
        }, statusCode: 403);
    }

    // Create document...
    return Results.Created("/documents/123", new { id = "123" });
});
```

### Simple Authentication Check

```csharp
app.MapGet("/protected", (HttpContext ctx) =>
{
    if (!ctx.RequireAuthentication())
        return Results.Empty;  // 401 already sent
    
    return Results.Ok("You are authenticated!");
});
```

## 🔀 Middleware Pipeline

The middleware should be placed after `UseGridContext()` for proper context propagation:

```csharp
var app = builder.Build();

app.UseRouting();          // Route matching (if using controllers)
app.UseGridContext();      // Grid context propagation
app.UseHoneyDrunkAuth();   // Authentication middleware
app.UseAuthorization();    // Optional: ASP.NET Core authorization

app.MapControllers();
app.Run();
```

## 📋 What Gets Registered

When you call `AddHoneyDrunkAuthAspNetCore()`:

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

## 📚 Dependencies

| Package | Purpose |
|---------|---------|
| `HoneyDrunk.Auth` | Core authentication runtime |
| `HoneyDrunk.Auth.Abstractions` | Core contracts |
| `HoneyDrunk.Kernel` | Grid context and telemetry |

## 🔗 Related Packages

| Package | Description |
|---------|-------------|
| **[HoneyDrunk.Auth.Abstractions](../HoneyDrunk.Auth.Abstractions/README.md)** | Core contracts (no dependencies) |
| **[HoneyDrunk.Auth](../HoneyDrunk.Auth/README.md)** | Core runtime with JWT validation |

## 📖 Documentation

- **[AspNetCore Guide](../docs/AspNetCore.md)** - Detailed middleware and extension documentation
- **[DependencyInjection Guide](../docs/DependencyInjection.md)** - Service registration
- **[FILE_GUIDE.md](../docs/FILE_GUIDE.md)** - Complete architecture reference

## ⚖️ License

This project is licensed under the [MIT License](../LICENSE).

---

<div align="center">

**Built with ❤️ by HoneyDrunk Studios**

</div>
