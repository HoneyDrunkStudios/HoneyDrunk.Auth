# Changelog

All notable changes to HoneyDrunk.Auth.AspNetCore will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

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
