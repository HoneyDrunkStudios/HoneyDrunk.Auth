# Changelog

All notable changes to HoneyDrunk.Auth.Abstractions will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

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
