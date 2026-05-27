# Changelog

All notable changes to HoneyDrunk.Auth.AspNetCore will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

## [0.6.0] - 2026-05-27

### Changed

- Aligned ASP.NET Core integration with the HoneyDrunk.Auth 0.6.0 Sonar follow-up release (ADR-0011 D11). No ASP.NET Core contract changes.
- Bumped `HoneyDrunk.Vault.EventGrid` `0.5.0 → 0.7.0` (Vault's 0.6.0 SonarCloud onboarding + 0.7.0 DIM promotion).
- Refreshed HoneyDrunk.Standards to 0.2.9 for ADR-0047 testing tooling alignment.

## [0.5.0] - 2026-05-21

### Changed

- Package version aligned with Auth `0.5.0` audit-emitter release.
- No ASP.NET Core contract changes.

## [0.4.0] - 2026-05-18

### Changed

- Updated HoneyDrunk.Vault.EventGrid dependency to `0.5.0`.
- Removed unused direct Kernel runtime dependency; ASP.NET Core integration continues to compose through Auth runtime and Vault Event Grid.

## [0.3.0] - 2026-04-25

### Added

- Registered HoneyDrunk.Vault.EventGrid invalidation services for ASP.NET Core integration.
- Added `MapHoneyDrunkAuthVaultInvalidationWebhook()` for `/internal/vault/invalidate`.

## [0.2.0] - 2026-02-14

### Changed

- Updated HoneyDrunk.Kernel from 0.3.0 to 0.4.0 for scope-based GridContext support

### Fixed

- Fixed XML doc `cref` on `AddHoneyDrunkAuthAspNetCore` to reference correct overload

## [0.1.0] - 2025-12-12

### Added

- Initial release of ASP.NET Core integration
- `HoneyDrunkAuthMiddleware` for Bearer token authentication in the request pipeline
- `IAuthenticatedIdentityAccessor` interface for accessing authenticated identity
- `HttpContextIdentityAccessor` for HttpContext-based identity access
- `AuthorizationEndpointExtensions` with `AuthorizeOrForbidAsync` helper method
- Dependency injection extensions via `HoneyDrunkAuthAspNetCoreServiceCollectionExtensions`
- Application builder extensions via `HoneyDrunkAuthApplicationBuilderExtensions`
- Seamless integration with ASP.NET Core's authentication pipeline
