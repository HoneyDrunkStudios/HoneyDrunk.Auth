# Changelog

All notable changes to HoneyDrunk.Auth.Abstractions will be documented in this file.

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

- Package version aligned with the HoneyDrunk.Auth 0.6.0 Sonar follow-up release (ADR-0011 D11). No contract changes.
- Refreshed HoneyDrunk.Standards to 0.2.9 for ADR-0047 testing tooling alignment.

## [0.5.0] - 2026-05-21

### Changed

- Package version aligned with the Auth audit-emitter release.
- No contract changes.

## [0.4.0] - 2026-05-18

### Changed

- Package version aligned with the Auth Kernel/Vault dependency refresh.
- No contract changes.

## [0.3.0] - 2026-04-25

### Changed

- Package version aligned with the ADR-0005/0006 Auth bootstrap release.
- No contract changes.

## [0.2.0] - 2026-02-14

### Added

- `ConfigurationError` authentication failure code for invalid or missing auth configuration
- `VaultUnavailable` authentication failure code for Vault backend unavailability
- `PolicyNotFound` authorization deny code for missing policy lookups

### Changed

- Clarified `InternalError` XML doc as "unexpected internal error"

## [0.1.0] - 2025-12-12

### Added

- Initial release of core authentication and authorization abstractions
- `IAuthenticationProvider` interface for credential validation
- `IAuthorizationPolicy` interface for authorization evaluation
- `AuthCredential` model for representing authentication credentials
- `AuthenticatedIdentity` model for authenticated user information
- `AuthenticationResult` for authentication attempt results
- `AuthorizationRequest` for describing authorization checks
- `AuthorizationDecision` for authorization evaluation results
- `AuthScheme` enum for supported authentication schemes
- `AuthenticationFailureCode` enum for authentication error codes
- `AuthorizationDenyCode` enum for authorization denial codes
- `AuthClaimTypes` constants for standard claim types
- `DenyReason` model for detailed authorization denial information
