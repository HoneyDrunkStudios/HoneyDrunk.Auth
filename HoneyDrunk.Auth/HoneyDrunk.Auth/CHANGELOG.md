# Changelog

All notable changes to HoneyDrunk.Auth will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [0.5.0] - 2026-05-21

### Added

- Added `HoneyDrunk.Audit.Abstractions` `0.1.0` dependency for append-only security-event audit emission.
- `BearerTokenAuthenticationProvider` now appends `auth.token.validate` audit entries for successful and denied bearer token validation outcomes.
- `DefaultAuthorizationPolicy` now appends `auth.authorize.{action}` audit entries for authorization grants and denials.
- Registered a no-op `IAuditLog` fallback and startup warning for hosts that have not composed a durable Audit backing.

### Verified

- Auth emits through `IAuditLog` only and does not depend on `HoneyDrunk.Audit.Data`.
- Audit metadata excludes raw token material and subject claims.

## [0.4.0] - 2026-05-18

### Changed

- Updated HoneyDrunk.Kernel consumption to HoneyDrunk.Kernel.Abstractions `0.7.0`.
- Updated HoneyDrunk.Vault provider packages to `0.5.0`.
- Auth registration now validates that Kernel Grid and Operation context accessors are registered, preserving the `AddHoneyDrunkNode()` prerequisite.
- Updated Microsoft.Extensions.Configuration.Binder to `10.0.6` to match transitive Vault dependencies.

### Verified

- `VaultSigningKeyProvider` continues to read `Jwt--SigningKeys` only through `ISecretStore`.
- Bearer authentication remains token validation only.

## [0.3.0] - 2026-04-25

### Added

- `AddAuthBootstrap()` for ADR-0005 env-var-driven Key Vault and App Configuration bootstrap.
- Canary coverage proving secret values flow through `ISecretStore` and are not pinned to versions.

### Changed

- `VaultSigningKeyProvider` now reads `Jwt--SigningKeys` from `ISecretStore` and non-secret `Auth:*` settings from App Configuration via `IConfiguration`.
- Updated HoneyDrunk.Vault package usage to `0.3.0` provider bootstrap packages.

## [0.2.0] - 2026-02-14

### Added

- `AuthOptions` with configurable `RequiredClaims` and `CacheTtl` for signing-key cache duration
- `AddHoneyDrunkAuth(Action<AuthOptions>)` overload for custom options configuration
- `CachingSigningKeyProvider` decorator with automatic cache preloading at startup
- `AuthorizationPolicyEvaluator` for pure, side-effect-free policy evaluation
- Required-claims validation in `BearerTokenAuthenticationProvider`
- Vault pre-validation with typed `ConfigurationError` and `VaultUnavailable` failure codes
- Dependency guard in `AddHoneyDrunkAuth` ensuring Kernel and Vault services are registered
- Internal `AuthenticationException` for structured error propagation

### Changed

- `BearerTokenAuthenticationProvider` now requires `IOptions<AuthOptions>` (constructor change)
- `DefaultAuthorizationPolicy` now delegates to `AuthorizationPolicyEvaluator` (constructor change)
- `AuthStartupHook` preloads the caching key provider on startup
- Reduced log verbosity: subject IDs no longer logged at warning level
- Updated HoneyDrunk.Kernel from 0.3.0 to 0.4.0
- Updated HoneyDrunk.Vault from 0.1.0 to 0.2.0
- Updated Microsoft.IdentityModel.JsonWebTokens from 8.3.1 to 8.15.0

## [0.1.0] - 2025-12-12

### Added

- Initial release of the HoneyDrunk.Auth runtime
- `BearerTokenAuthenticationProvider` for JWT Bearer token validation
- `DefaultAuthorizationPolicy` for role-based authorization evaluation
- `VaultSigningKeyProvider` for Vault-backed signing key retrieval
- `ISigningKeyProvider` interface and `SigningKeyInfo` model
- `AuthTelemetry` for authentication and authorization metrics
- `AuthHealthContributor` for health check integration
- `AuthReadinessContributor` for readiness check integration
- `AuthStartupHook` for lifecycle management
- Dependency injection extensions via `HoneyDrunkAuthServiceCollectionExtensions`
- Builder extensions via `HoneyDrunkAuthBuilderExtensions`
- Full integration with HoneyDrunk.Kernel and HoneyDrunk.Vault
