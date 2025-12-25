# Changelog

All notable changes to HoneyDrunk.Auth will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

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
