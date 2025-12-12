using HoneyDrunk.Auth.Abstractions;
using HoneyDrunk.Auth.Authorization;
using HoneyDrunk.Kernel.Abstractions.Telemetry;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace HoneyDrunk.Auth.Tests;

/// <summary>
/// Tests for <see cref="DefaultAuthorizationPolicy"/>.
/// </summary>
public sealed class DefaultAuthorizationPolicyTests
{
    private readonly DefaultAuthorizationPolicy _policy;

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultAuthorizationPolicyTests"/> class.
    /// </summary>
    public DefaultAuthorizationPolicyTests()
    {
        var telemetryFactory = Substitute.For<ITelemetryActivityFactory>();
        _policy = new DefaultAuthorizationPolicy(
            telemetryFactory,
            NullLogger<DefaultAuthorizationPolicy>.Instance);
    }

    /// <summary>
    /// Tests that unauthenticated request is denied with NotAuthenticated code.
    /// </summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Fact]
    public async Task EvaluateAsync_NotAuthenticated_DeniesWithNotAuthenticated()
    {
        // Arrange
        var request = new AuthorizationRequest("read", "resource");

        // Act
        var decision = await _policy.EvaluateAsync(null, request);

        // Assert
        Assert.False(decision.IsAllowed);
        Assert.Contains(decision.DenyReasons, r => r.Code == AuthorizationDenyCode.NotAuthenticated);
    }

    /// <summary>
    /// Tests that authenticated user with no requirements is allowed.
    /// </summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Fact]
    public async Task EvaluateAsync_NoRequirements_AllowsAuthenticatedUser()
    {
        // Arrange
        var identity = CreateIdentity();
        var request = new AuthorizationRequest("read", "resource");

        // Act
        var decision = await _policy.EvaluateAsync(identity, request);

        // Assert
        Assert.True(decision.IsAllowed);
        Assert.Contains("authenticated", decision.SatisfiedRequirements);
    }

    /// <summary>
    /// Tests that request with required scope present is allowed.
    /// </summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Fact]
    public async Task EvaluateAsync_RequiredScopePresent_Allows()
    {
        // Arrange
        var identity = CreateIdentity(scopes: ["read", "write"]);
        var request = new AuthorizationRequest("read", "resource", requiredScopes: ["read"]);

        // Act
        var decision = await _policy.EvaluateAsync(identity, request);

        // Assert
        Assert.True(decision.IsAllowed);
        Assert.Contains("scope:read", decision.SatisfiedRequirements);
    }

    /// <summary>
    /// Tests that request with missing required scope is denied.
    /// </summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Fact]
    public async Task EvaluateAsync_RequiredScopeMissing_DeniesWithMissingScope()
    {
        // Arrange
        var identity = CreateIdentity(scopes: ["read"]);
        var request = new AuthorizationRequest("write", "resource", requiredScopes: ["write"]);

        // Act
        var decision = await _policy.EvaluateAsync(identity, request);

        // Assert
        Assert.False(decision.IsAllowed);
        Assert.Contains(decision.DenyReasons, r => r.Code == AuthorizationDenyCode.MissingScope);
    }

    /// <summary>
    /// Tests that request with required role present is allowed.
    /// </summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Fact]
    public async Task EvaluateAsync_RequiredRolePresent_Allows()
    {
        // Arrange
        var identity = CreateIdentity(roles: ["admin", "user"]);
        var request = new AuthorizationRequest("delete", "resource", requiredRoles: ["admin"]);

        // Act
        var decision = await _policy.EvaluateAsync(identity, request);

        // Assert
        Assert.True(decision.IsAllowed);
        Assert.Contains("role:admin", decision.SatisfiedRequirements);
    }

    /// <summary>
    /// Tests that request with missing required role is denied.
    /// </summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Fact]
    public async Task EvaluateAsync_RequiredRoleMissing_DeniesWithMissingRole()
    {
        // Arrange
        var identity = CreateIdentity(roles: ["user"]);
        var request = new AuthorizationRequest("delete", "resource", requiredRoles: ["admin"]);

        // Act
        var decision = await _policy.EvaluateAsync(identity, request);

        // Assert
        Assert.False(decision.IsAllowed);
        Assert.Contains(decision.DenyReasons, r => r.Code == AuthorizationDenyCode.MissingRole);
    }

    /// <summary>
    /// Tests that request is allowed when one of required roles is present.
    /// </summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Fact]
    public async Task EvaluateAsync_OneOfRequiredRolesPresent_Allows()
    {
        // Arrange
        var identity = CreateIdentity(roles: ["moderator"]);
        var request = new AuthorizationRequest("delete", "resource", requiredRoles: ["admin", "moderator"]);

        // Act
        var decision = await _policy.EvaluateAsync(identity, request);

        // Assert
        Assert.True(decision.IsAllowed);
        Assert.Contains("role:moderator", decision.SatisfiedRequirements);
    }

    /// <summary>
    /// Tests that resource owner is allowed access.
    /// </summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Fact]
    public async Task EvaluateAsync_ResourceOwner_Allows()
    {
        // Arrange
        var identity = CreateIdentity(subjectId: "user-123");
        var request = new AuthorizationRequest("update", "profile", resourceOwnerId: "user-123");

        // Act
        var decision = await _policy.EvaluateAsync(identity, request);

        // Assert
        Assert.True(decision.IsAllowed);
        Assert.Contains("owner", decision.SatisfiedRequirements);
    }

    /// <summary>
    /// Tests that non-owner is denied access to owned resource.
    /// </summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Fact]
    public async Task EvaluateAsync_NotResourceOwner_DeniesWithOwnershipDenied()
    {
        // Arrange
        var identity = CreateIdentity(subjectId: "user-456");
        var request = new AuthorizationRequest("update", "profile", resourceOwnerId: "user-123");

        // Act
        var decision = await _policy.EvaluateAsync(identity, request);

        // Assert
        Assert.False(decision.IsAllowed);
        Assert.Contains(decision.DenyReasons, r => r.Code == AuthorizationDenyCode.ResourceOwnershipDenied);
    }

    /// <summary>
    /// Tests that all satisfied requirements are listed in the decision.
    /// </summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Fact]
    public async Task EvaluateAsync_MultipleRequirementsSatisfied_ListsAll()
    {
        // Arrange
        var identity = CreateIdentity(subjectId: "user-123", scopes: ["read"], roles: ["user"]);
        var request = new AuthorizationRequest(
            "read",
            "resource",
            requiredScopes: ["read"],
            requiredRoles: ["user"],
            resourceOwnerId: "user-123");

        // Act
        var decision = await _policy.EvaluateAsync(identity, request);

        // Assert
        Assert.True(decision.IsAllowed);
        Assert.Contains("authenticated", decision.SatisfiedRequirements);
        Assert.Contains("scope:read", decision.SatisfiedRequirements);
        Assert.Contains("role:user", decision.SatisfiedRequirements);
        Assert.Contains("owner", decision.SatisfiedRequirements);
    }

    private static AuthenticatedIdentity CreateIdentity(
        string subjectId = "user-123",
        IEnumerable<string>? scopes = null,
        IEnumerable<string>? roles = null)
    {
        var claims = new Dictionary<string, IReadOnlyList<string>>();

        if (scopes != null)
        {
            claims[AuthClaimTypes.Scope] = scopes.ToList().AsReadOnly();
        }

        if (roles != null)
        {
            claims[AuthClaimTypes.Role] = roles.ToList().AsReadOnly();
        }

        return new AuthenticatedIdentity(subjectId, AuthScheme.Bearer, "Test User", claims);
    }
}
