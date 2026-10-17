using HoneyDrunk.Auth.Abstractions;

namespace HoneyDrunk.Auth.Tests.Abstractions;

/// <summary>
/// Tests for <see cref="AuthorizationDecision"/>.
/// </summary>
public sealed class AuthorizationDecisionTests
{
    /// <summary>
    /// Tests that Allow creates an allowed decision.
    /// </summary>
    [Fact]
    public void Allow_NoRequirements_CreatesAllowedDecision()
    {
        // Act
        var decision = AuthorizationDecision.Allow();

        // Assert
        Assert.True(decision.IsAllowed);
        Assert.Empty(decision.DenyReasons);
        Assert.Empty(decision.SatisfiedRequirements);
    }

    /// <summary>
    /// Tests that Allow with requirements includes them.
    /// </summary>
    [Fact]
    public void Allow_WithRequirements_IncludesRequirements()
    {
        // Arrange
        var requirements = new[] { "authenticated", "scope:read", "role:admin" };

        // Act
        var decision = AuthorizationDecision.Allow(requirements);

        // Assert
        Assert.True(decision.IsAllowed);
        Assert.Empty(decision.DenyReasons);
        Assert.Equal(3, decision.SatisfiedRequirements.Count);
        Assert.Contains("authenticated", decision.SatisfiedRequirements);
        Assert.Contains("scope:read", decision.SatisfiedRequirements);
        Assert.Contains("role:admin", decision.SatisfiedRequirements);
    }

    /// <summary>
    /// Tests that Allow with null requirements creates empty list.
    /// </summary>
    [Fact]
    public void Allow_NullRequirements_CreatesEmptyList()
    {
        // Act
        var decision = AuthorizationDecision.Allow(null);

        // Assert
        Assert.True(decision.IsAllowed);
        Assert.Empty(decision.SatisfiedRequirements);
    }

    /// <summary>
    /// Tests that Deny with single reason creates denied decision.
    /// </summary>
    [Fact]
    public void Deny_SingleReason_CreatesDeniedDecision()
    {
        // Act
        var decision = AuthorizationDecision.Deny(AuthorizationDenyCode.NotAuthenticated, "User is not authenticated");

        // Assert
        Assert.False(decision.IsAllowed);
        Assert.Single(decision.DenyReasons);
        Assert.Equal(AuthorizationDenyCode.NotAuthenticated, decision.DenyReasons[0].Code);
        Assert.Equal("User is not authenticated", decision.DenyReasons[0].Message);
        Assert.Empty(decision.SatisfiedRequirements);
    }

    /// <summary>
    /// Tests that Deny with multiple reasons creates denied decision.
    /// </summary>
    [Fact]
    public void Deny_MultipleReasons_CreatesDeniedDecisionWithAllReasons()
    {
        // Arrange
        var denyReasons = new[]
        {
            new DenyReason(AuthorizationDenyCode.MissingScope, "Missing scope: write"),
            new DenyReason(AuthorizationDenyCode.MissingRole, "Missing role: admin"),
        };

        // Act
        var decision = AuthorizationDecision.Deny(denyReasons);

        // Assert
        Assert.False(decision.IsAllowed);
        Assert.Equal(2, decision.DenyReasons.Count);
        Assert.Contains(decision.DenyReasons, r => r.Code == AuthorizationDenyCode.MissingScope);
        Assert.Contains(decision.DenyReasons, r => r.Code == AuthorizationDenyCode.MissingRole);
    }

    /// <summary>
    /// Tests that Deny with reasons and satisfied requirements includes both.
    /// </summary>
    [Fact]
    public void Deny_WithSatisfiedRequirements_IncludesBoth()
    {
        // Arrange
        var denyReasons = new[] { new DenyReason(AuthorizationDenyCode.MissingRole, "Missing role: admin") };
        var satisfiedRequirements = new[] { "authenticated", "scope:read" };

        // Act
        var decision = AuthorizationDecision.Deny(denyReasons, satisfiedRequirements);

        // Assert
        Assert.False(decision.IsAllowed);
        Assert.Single(decision.DenyReasons);
        Assert.Equal(2, decision.SatisfiedRequirements.Count);
        Assert.Contains("authenticated", decision.SatisfiedRequirements);
        Assert.Contains("scope:read", decision.SatisfiedRequirements);
    }

    /// <summary>
    /// Tests that Deny with null satisfied requirements creates empty list.
    /// </summary>
    [Fact]
    public void Deny_NullSatisfiedRequirements_CreatesEmptyList()
    {
        // Arrange
        var denyReasons = new[] { new DenyReason(AuthorizationDenyCode.NotAuthenticated, "Not authenticated") };

        // Act
        var decision = AuthorizationDecision.Deny(denyReasons, null);

        // Assert
        Assert.False(decision.IsAllowed);
        Assert.Empty(decision.SatisfiedRequirements);
    }

    /// <summary>
    /// Tests all deny codes can be used.
    /// </summary>
    /// <param name="code">The deny code to test.</param>
    [Theory]
    [InlineData(AuthorizationDenyCode.NotAuthenticated)]
    [InlineData(AuthorizationDenyCode.MissingScope)]
    [InlineData(AuthorizationDenyCode.MissingRole)]
    [InlineData(AuthorizationDenyCode.ResourceOwnershipDenied)]
    [InlineData(AuthorizationDenyCode.PolicyNotSatisfied)]
    public void Deny_AllDenyCodes_CreatesDecisionWithCorrectCode(AuthorizationDenyCode code)
    {
        // Act
        var decision = AuthorizationDecision.Deny(code, "Test message");

        // Assert
        Assert.False(decision.IsAllowed);
        Assert.Equal(code, decision.DenyReasons[0].Code);
    }
}
