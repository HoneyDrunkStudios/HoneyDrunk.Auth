# HoneyDrunk.Auth - Repository Changelog

All notable changes to the HoneyDrunk.Auth repository will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

**Note:** See individual package CHANGELOGs for detailed changes:
- [HoneyDrunk.Auth.Abstractions CHANGELOG](HoneyDrunk.Auth.Abstractions/CHANGELOG.md)
- [HoneyDrunk.Auth CHANGELOG](HoneyDrunk.Auth/CHANGELOG.md)
- [HoneyDrunk.Auth.AspNetCore CHANGELOG](HoneyDrunk.Auth.AspNetCore/CHANGELOG.md)

---

## [Unreleased]

### Internal

- Backfilled Auth test coverage above the Grid PR coverage gate floor and seeded the coverage baseline ratchet artifact.

## [0.4.0] - 2026-05-18

### Changed

- Aligned Auth packages with HoneyDrunk.Kernel.Abstractions `0.7.0` and HoneyDrunk.Vault `0.5.0` packages.
- Tightened Auth DI guards to require Kernel Grid and Operation context accessors from `AddHoneyDrunkNode()` before Auth registration.
- Reduced Auth runtime dependency coupling by consuming Kernel abstractions instead of the full Kernel runtime package where possible.

### Verified

- JWT signing keys still resolve through Vault `ISecretStore` using `Jwt--SigningKeys`.
- Auth remains validation-only; no token issuance behavior was added.

## [0.3.0] - 2026-04-25

### Added

- ADR-0005/0006 bootstrap support using env-var-driven Key Vault, App Configuration, and Event Grid invalidation packages.
- Deployment notes for `kv-hd-auth-{env}`, `honeydrunk-auth` App Configuration labels, and `/internal/vault/invalidate`.

### Changed

- Auth signing keys now use the provider-grouped `Jwt--SigningKeys` secret name.
- Issuer, audience, and clock skew are documented as non-secret App Configuration values instead of Vault secrets.

## [0.2.0] - 2026-02-14

### Added

- `AuthOptions` with configurable `RequiredClaims` and `CacheTtl` for signing-key cache duration
- `CachingSigningKeyProvider` decorator with automatic cache preloading at startup
- `AuthorizationPolicyEvaluator` for pure, side-effect-free policy evaluation
- Required-claims validation in `BearerTokenAuthenticationProvider`
- Vault pre-validation with typed `ConfigurationError` and `VaultUnavailable` failure codes
- `PolicyNotFound` authorization deny code for missing policy lookups
- Dependency guard in `AddHoneyDrunkAuth` ensuring Kernel and Vault services are registered

### Changed

- `BearerTokenAuthenticationProvider` now requires `IOptions<AuthOptions>` (constructor change)
- `DefaultAuthorizationPolicy` now delegates to `AuthorizationPolicyEvaluator` (constructor change)
- Updated HoneyDrunk.Kernel from 0.3.0 to 0.4.0
- Updated HoneyDrunk.Vault from 0.1.0 to 0.2.0
- Updated Microsoft.IdentityModel.JsonWebTokens from 8.3.1 to 8.15.0

### Fixed

- Fixed XML doc `cref` on `AddHoneyDrunkAuthAspNetCore` to reference correct overload

## [0.1.0] - 2025-12-12

### Added

- Initial release of HoneyDrunk.Auth
- JWT Bearer token authentication via `BearerTokenAuthenticationProvider`
- Role-based authorization via `DefaultAuthorizationPolicy`
- Vault-backed signing key retrieval via `VaultSigningKeyProvider`
- ASP.NET Core middleware integration (`HoneyDrunkAuthMiddleware`)
- `IAuthenticatedIdentityAccessor` for HttpContext-based identity access
- Telemetry, health, and readiness contributors
- Full integration with HoneyDrunk.Kernel and HoneyDrunk.Vault

[0.3.0]: https://github.com/HoneyDrunkStudios/HoneyDrunk.Auth/releases/tag/v0.3.0
[0.2.0]: https://github.com/HoneyDrunkStudios/HoneyDrunk.Auth/releases/tag/v0.2.0
[0.1.0]: https://github.com/HoneyDrunkStudios/HoneyDrunk.Auth/releases/tag/v0.1.0
