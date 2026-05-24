# Changelog

All notable changes to HoneyDrunk.Auth.Abstractions will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Changed

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
