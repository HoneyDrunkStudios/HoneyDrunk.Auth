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

## [0.6.0] - 2026-05-27

### Changed (breaking)

- **`AuthorizationPolicyEvaluator` is now a `static class`** (was `sealed`; Sonar S1118). The class only ever had a static `Evaluate` method, so this is a shape correction rather than a behavioral break — but `new AuthorizationPolicyEvaluator()` no longer compiles.
- **`AuthenticationException` is now a public top-level type** in `HoneyDrunk.Auth.Authentication` (was nested-private on `BearerTokenAuthenticationProvider`; Sonar S3871). Catchers must update.
- **Package versions bumped** to `HoneyDrunk.Auth* 0.6.0` per pre-1.0 semver (`0.x.0 → 0.(x+1).0` for breaks — the dep bumps below are also transitively breaking).

### Changed

- `BearerTokenAuthenticationProvider.ValidateTokenAsync` split into `LoadValidationConfigurationAsync`, `BuildValidationParameters`, `TryResolveSignatureFailureAsync`, and `BuildIdentityResult` helpers (Sonar S3776 — cognitive complexity 20 → under 15).
- `AuthHealthContributor.CheckHealthAsync` and `AuthReadinessContributor.CheckReadinessAsync` default the `CancellationToken` parameter to `default` (Sonar S1006).
- `VaultSigningKeyProvider.SigningKeyInfoDto` properties switched from `private set` to `init` accessors (Sonar S2376 / S3459).
- `BearerTokenAuthenticationProvider.GetAllowedAuditClaims` / `TryReadAllowedClaims` return `Dictionary<string, string>` instead of `IReadOnlyDictionary<string, string>` (IDE0306 / S6605 — private helpers).
- Canary fakes renamed `tags → additionalTags` (telemetry factories) and `secretPath → secretName` (stub secret store) to match the interface declarations (Sonar S927).
- Canary `NullLoggerProvider.NullLogger` nested class renamed to `InnerNullLogger` to stop shadowing the outer type's `Instance` (Sonar S3604).
- Canary `ToggleableSigningKeyProvider` extracts the `"Vault unavailable (simulated)"` literal to a `VaultUnavailableMessage` const (Sonar S1192).
- Canary `TokenMinter.GenerateKey` switched from `Random.Shared.NextBytes` to `RandomNumberGenerator.GetBytes(32)` (Sonar S2245 / S6781). Subject literal extracted to `DefaultSubject` const (S1192).
- Canary `Check8_PolicyNotFoundDistinguishable` drops the `policyNotFound == genericDeny` and `(int)policyNotFound != 8` checks — Sonar correctly proves both always-false at compile time of the canary; the round-trip-through-AuthorizationDecision check is retained and the enum identity invariant is covered separately by HoneyDrunk.Auth.Tests.
- Test: `AuthStartupHookTests.ExecuteAsync_AllConfigValid_Succeeds` wraps the call in `Record.ExceptionAsync` and asserts `Assert.Null(exception)` so the "must not throw" property is captured explicitly (Sonar S2699 blocker).
- Test: `CachingSigningKeyProviderTests.RecordingSigningKeyProvider._keys` switched to collection-expression form `[...]` (Sonar S6602).

### Internal

- Bumped `HoneyDrunk.Vault` / `Providers.AppConfiguration` / `Providers.AzureKeyVault` / `EventGrid` `0.5.0 → 0.7.0` (Vault's 0.6.0 SonarCloud onboarding + 0.7.0 DIM promotion — Auth only consumes ISecretStore via the bootstrap extensions; no surface affected).
- Bumped `HoneyDrunk.Kernel.Abstractions` `0.7.0 → 0.8.0`.
- Bumped `Microsoft.Extensions.Configuration` / `Configuration.Binder` `10.0.6 → 10.0.8`, `Microsoft.AspNetCore.TestHost` `10.0.5 → 10.0.8`, `Microsoft.IdentityModel.JsonWebTokens` `8.17.0 → 8.18.0`.
- Two Sonar S1199 ("Extract this nested code block into a separate method") findings on the Test 4a / Test 4b scope blocks in `Program.cs` remain — the canary is a top-level Program and static local functions cannot reference the other top-level helpers (CS8422), so the analyzer suggestion does not apply cleanly. Documented in-line.

## [0.5.0] - 2026-05-21

### Added

- Wired Auth as the first `IAuditLog` emitter using `HoneyDrunk.Audit.Abstractions` `0.1.0`.
- Appended durable security audit entries for bearer token validation outcomes and authorization allow/deny decisions.
- Added a no-op fallback audit sink plus startup warning when hosts have not composed a durable `IAuditLog` backing.

### Verified

- Auth depends only on `HoneyDrunk.Audit.Abstractions`; it does not depend on `HoneyDrunk.Audit.Data`.
- Audit metadata avoids raw token material and subject claims.

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

[0.5.0]: https://github.com/HoneyDrunkStudios/HoneyDrunk.Auth/releases/tag/v0.5.0
[0.4.0]: https://github.com/HoneyDrunkStudios/HoneyDrunk.Auth/releases/tag/v0.4.0
[0.3.0]: https://github.com/HoneyDrunkStudios/HoneyDrunk.Auth/releases/tag/v0.3.0
[0.2.0]: https://github.com/HoneyDrunkStudios/HoneyDrunk.Auth/releases/tag/v0.2.0
[0.1.0]: https://github.com/HoneyDrunkStudios/HoneyDrunk.Auth/releases/tag/v0.1.0
