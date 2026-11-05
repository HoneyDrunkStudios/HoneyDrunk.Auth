# Changelog

All notable changes to HoneyDrunk.Auth.AspNetCore will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [0.6.2] - 2026-11-05

### Changed

- Refresh NuGet dependencies and adopt upstream package releases in dependency order. Preserve target frameworks and public contracts.

| Dependency | Previous | Updated |
| --- | --- | --- |
| HoneyDrunk.Audit.Abstractions | 0.2.1 | 0.2.2 |
| HoneyDrunk.Kernel.Abstractions | 0.8.1 | 0.8.2 |
| HoneyDrunk.Standards | 0.3.0 | 0.4.3 |
| HoneyDrunk.Standards.Tests | 0.3.0 | 0.4.3 |
| HoneyDrunk.Vault | 0.8.1 | 0.8.2 |
| HoneyDrunk.Vault.EventGrid | 0.8.1 | 0.8.2 |
| HoneyDrunk.Vault.Providers.AppConfiguration | 0.8.1 | 0.8.2 |
| HoneyDrunk.Vault.Providers.AzureKeyVault | 0.8.1 | 0.8.2 |
| Microsoft.AspNetCore.TestHost | 10.0.12 | 10.0.14 |
| Microsoft.CodeAnalysis.NetAnalyzers | 10.0.401 | 10.0.403 |
| Microsoft.Extensions.Configuration | 10.0.12 | 10.0.14 |
| Microsoft.Extensions.Configuration.Binder | 10.0.12 | 10.0.14 |

## [0.6.1] - 2026-09-26

### Changed

- Refresh dependency and shared build-tooling versions; preserve target frameworks and existing public contracts. See the [repository dependency changes](../../CHANGELOG.md).

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
