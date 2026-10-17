using HoneyDrunk.Auth.Abstractions;

namespace HoneyDrunk.Auth.Tests.Abstractions;

/// <summary>
/// Tests for <see cref="AuthenticatedIdentity"/>.
/// </summary>
public sealed class AuthenticatedIdentityTests
{
    /// <summary>
    /// Tests that constructor sets properties correctly.
    /// </summary>
    [Fact]
    public void Constructor_ValidParameters_SetsProperties()
    {
        // Arrange
        var claims = new Dictionary<string, IReadOnlyList<string>>
        {
            ["email"] = ["user@example.com"],
        };

        // Act
        var identity = new AuthenticatedIdentity("user-123", "Bearer", "Test User", claims);

        // Assert
        Assert.Equal("user-123", identity.SubjectId);
        Assert.Equal("Bearer", identity.Scheme);
        Assert.Equal("Test User", identity.DisplayName);
        Assert.Single(identity.Claims);
    }

    /// <summary>
    /// Tests that constructor throws when subjectId is null.
    /// </summary>
    [Fact]
    public void Constructor_NullSubjectId_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentNullException>(() => new AuthenticatedIdentity(null!, "Bearer"));
    }

    /// <summary>
    /// Tests that constructor throws when subjectId is empty.
    /// </summary>
    [Fact]
    public void Constructor_EmptySubjectId_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => new AuthenticatedIdentity(string.Empty, "Bearer"));
    }

    /// <summary>
    /// Tests that constructor throws when subjectId is whitespace.
    /// </summary>
    [Fact]
    public void Constructor_WhitespaceSubjectId_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => new AuthenticatedIdentity("   ", "Bearer"));
    }

    /// <summary>
    /// Tests that constructor throws when scheme is null.
    /// </summary>
    [Fact]
    public void Constructor_NullScheme_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentNullException>(() => new AuthenticatedIdentity("user-123", null!));
    }

    /// <summary>
    /// Tests that constructor throws when scheme is empty.
    /// </summary>
    [Fact]
    public void Constructor_EmptyScheme_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => new AuthenticatedIdentity("user-123", string.Empty));
    }

    /// <summary>
    /// Tests that constructor works with null claims.
    /// </summary>
    [Fact]
    public void Constructor_NullClaims_CreatesEmptyClaimsDictionary()
    {
        // Act
        var identity = new AuthenticatedIdentity("user-123", "Bearer", claims: null);

        // Assert
        Assert.Empty(identity.Claims);
    }

    /// <summary>
    /// Tests that constructor works with null display name.
    /// </summary>
    [Fact]
    public void Constructor_NullDisplayName_AllowsNull()
    {
        // Act
        var identity = new AuthenticatedIdentity("user-123", "Bearer", displayName: null);

        // Assert
        Assert.Null(identity.DisplayName);
    }

    /// <summary>
    /// Tests GetClaimValue returns first value for existing claim.
    /// </summary>
    [Fact]
    public void GetClaimValue_ExistingClaim_ReturnsFirstValue()
    {
        // Arrange
        var claims = new Dictionary<string, IReadOnlyList<string>>
        {
            ["role"] = ["admin", "user"],
        };
        var identity = new AuthenticatedIdentity("user-123", "Bearer", claims: claims);

        // Act
        var value = identity.GetClaimValue("role");

        // Assert
        Assert.Equal("admin", value);
    }

    /// <summary>
    /// Tests GetClaimValue returns null for non-existing claim.
    /// </summary>
    [Fact]
    public void GetClaimValue_NonExistingClaim_ReturnsNull()
    {
        // Arrange
        var identity = new AuthenticatedIdentity("user-123", "Bearer");

        // Act
        var value = identity.GetClaimValue("role");

        // Assert
        Assert.Null(value);
    }

    /// <summary>
    /// Tests GetClaimValue returns null for claim with empty values list.
    /// </summary>
    [Fact]
    public void GetClaimValue_EmptyValuesList_ReturnsNull()
    {
        // Arrange
        var claims = new Dictionary<string, IReadOnlyList<string>>
        {
            ["role"] = [],
        };
        var identity = new AuthenticatedIdentity("user-123", "Bearer", claims: claims);

        // Act
        var value = identity.GetClaimValue("role");

        // Assert
        Assert.Null(value);
    }

    /// <summary>
    /// Tests GetClaimValues returns all values for existing claim.
    /// </summary>
    [Fact]
    public void GetClaimValues_ExistingClaim_ReturnsAllValues()
    {
        // Arrange
        var claims = new Dictionary<string, IReadOnlyList<string>>
        {
            ["role"] = ["admin", "user", "moderator"],
        };
        var identity = new AuthenticatedIdentity("user-123", "Bearer", claims: claims);

        // Act
        var values = identity.GetClaimValues("role");

        // Assert
        Assert.Equal(3, values.Count);
        Assert.Contains("admin", values);
        Assert.Contains("user", values);
        Assert.Contains("moderator", values);
    }

    /// <summary>
    /// Tests GetClaimValues returns empty list for non-existing claim.
    /// </summary>
    [Fact]
    public void GetClaimValues_NonExistingClaim_ReturnsEmptyList()
    {
        // Arrange
        var identity = new AuthenticatedIdentity("user-123", "Bearer");

        // Act
        var values = identity.GetClaimValues("role");

        // Assert
        Assert.Empty(values);
    }

    /// <summary>
    /// Tests HasClaim returns true for existing claim type.
    /// </summary>
    [Fact]
    public void HasClaim_ExistingClaimType_ReturnsTrue()
    {
        // Arrange
        var claims = new Dictionary<string, IReadOnlyList<string>>
        {
            ["email"] = ["user@example.com"],
        };
        var identity = new AuthenticatedIdentity("user-123", "Bearer", claims: claims);

        // Act
        var hasClaim = identity.HasClaim("email");

        // Assert
        Assert.True(hasClaim);
    }

    /// <summary>
    /// Tests HasClaim returns false for non-existing claim type.
    /// </summary>
    [Fact]
    public void HasClaim_NonExistingClaimType_ReturnsFalse()
    {
        // Arrange
        var identity = new AuthenticatedIdentity("user-123", "Bearer");

        // Act
        var hasClaim = identity.HasClaim("email");

        // Assert
        Assert.False(hasClaim);
    }

    /// <summary>
    /// Tests HasClaim with value returns true when claim has the value.
    /// </summary>
    [Fact]
    public void HasClaimWithValue_ClaimHasValue_ReturnsTrue()
    {
        // Arrange
        var claims = new Dictionary<string, IReadOnlyList<string>>
        {
            ["role"] = ["admin", "user"],
        };
        var identity = new AuthenticatedIdentity("user-123", "Bearer", claims: claims);

        // Act
        var hasClaim = identity.HasClaim("role", "admin");

        // Assert
        Assert.True(hasClaim);
    }

    /// <summary>
    /// Tests HasClaim with value returns false when claim doesn't have the value.
    /// </summary>
    [Fact]
    public void HasClaimWithValue_ClaimDoesNotHaveValue_ReturnsFalse()
    {
        // Arrange
        var claims = new Dictionary<string, IReadOnlyList<string>>
        {
            ["role"] = ["user"],
        };
        var identity = new AuthenticatedIdentity("user-123", "Bearer", claims: claims);

        // Act
        var hasClaim = identity.HasClaim("role", "admin");

        // Assert
        Assert.False(hasClaim);
    }

    /// <summary>
    /// Tests HasClaim with value returns false when claim type doesn't exist.
    /// </summary>
    [Fact]
    public void HasClaimWithValue_ClaimTypeDoesNotExist_ReturnsFalse()
    {
        // Arrange
        var identity = new AuthenticatedIdentity("user-123", "Bearer");

        // Act
        var hasClaim = identity.HasClaim("role", "admin");

        // Assert
        Assert.False(hasClaim);
    }

    /// <summary>
    /// Tests HasClaim with value is case-sensitive.
    /// </summary>
    [Fact]
    public void HasClaimWithValue_CaseSensitive_ReturnsFalseForDifferentCase()
    {
        // Arrange
        var claims = new Dictionary<string, IReadOnlyList<string>>
        {
            ["role"] = ["Admin"],
        };
        var identity = new AuthenticatedIdentity("user-123", "Bearer", claims: claims);

        // Act
        var hasClaim = identity.HasClaim("role", "admin");

        // Assert
        Assert.False(hasClaim);
    }
}
