# Changelog

All notable changes to the HoneyDrunk.Auth repository are documented in this file.
The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).

See the per-package CHANGELOGs for detailed, package-scoped history:

- [HoneyDrunk.Auth.Abstractions](HoneyDrunk.Auth/HoneyDrunk.Auth.Abstractions/CHANGELOG.md)
- [HoneyDrunk.Auth](HoneyDrunk.Auth/HoneyDrunk.Auth/CHANGELOG.md)
- [HoneyDrunk.Auth.AspNetCore](HoneyDrunk.Auth/HoneyDrunk.Auth.AspNetCore/CHANGELOG.md)

## Unreleased

## 0.6.0 - Sonar gate cleanup

### Changed

- `AuthorizationPolicyEvaluator` is now a `static class` (was `sealed`); `new AuthorizationPolicyEvaluator()` no longer compiles.
- `BearerAuthenticationException` promoted to a public top-level type in `HoneyDrunk.Auth.Authentication` (renamed from the nested `AuthenticationException`).
- Bumped `HoneyDrunk.Vault*` 0.5.0 to 0.7.0, `HoneyDrunk.Kernel.Abstractions` 0.7.0 to 0.8.0, `Microsoft.IdentityModel.JsonWebTokens` 8.17.0 to 8.18.0.

## 0.5.0 - Audit emitter

### Added

- Wired Auth as the first `IAuditLog` emitter using `HoneyDrunk.Audit.Abstractions` 0.1.0.
- Durable security audit entries for bearer-token validation outcomes and authorization allow/deny decisions.
- No-op fallback audit sink plus startup warning when hosts have not composed a durable `IAuditLog` backing.

## 0.4.0 - Kernel/Vault alignment

### Changed

- Aligned Auth packages with `HoneyDrunk.Kernel.Abstractions` 0.7.0 and `HoneyDrunk.Vault` 0.5.0.
- Tightened Auth DI guards to require Kernel Grid and Operation context accessors before Auth registration.

## 0.3.0 - ADR-0005/0006 bootstrap

### Added

- ADR-0005/0006 bootstrap support using env-var-driven Key Vault, App Configuration, and Event Grid invalidation packages.
- Deployment notes for `kv-hd-auth-{env}`, `honeydrunk-auth` App Configuration labels, and `/internal/vault/invalidate`.

## 0.2.0 - Caching and policy evaluation

### Added

- `AuthOptions` with configurable `RequiredClaims` and `CacheTtl` for signing-key cache duration.
- `CachingSigningKeyProvider` decorator with automatic cache preloading at startup.
- `AuthorizationPolicyEvaluator` for pure, side-effect-free policy evaluation.
- Vault pre-validation with typed `ConfigurationError` and `VaultUnavailable` failure codes.

## 0.1.0 - Initial release

### Added

- JWT Bearer token authentication via `BearerTokenAuthenticationProvider`.
- Role-based authorization via `DefaultAuthorizationPolicy`.
- Vault-backed signing key retrieval via `VaultSigningKeyProvider`.
- ASP.NET Core middleware integration (`HoneyDrunkAuthMiddleware`) and `IAuthenticatedIdentityAccessor`.
- Telemetry, health, and readiness contributors; full integration with HoneyDrunk.Kernel and HoneyDrunk.Vault.
