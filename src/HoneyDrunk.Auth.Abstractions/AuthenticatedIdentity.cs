namespace HoneyDrunk.Auth.Abstractions;

/// <summary>
/// Represents an authenticated identity with claims and attributes.
/// </summary>
/// <remarks>
/// This is a neutral representation independent of any specific identity framework.
/// </remarks>
public sealed class AuthenticatedIdentity
{
    private readonly Dictionary<string, IReadOnlyList<string>> _claims;

    /// <summary>
    /// Initializes a new instance of the <see cref="AuthenticatedIdentity"/> class.
    /// </summary>
    /// <param name="subjectId">The stable subject identifier.</param>
    /// <param name="scheme">The authentication scheme used.</param>
    /// <param name="displayName">The optional display name.</param>
    /// <param name="claims">The identity claims.</param>
    /// <exception cref="ArgumentNullException">Thrown when subjectId or scheme is null.</exception>
    /// <exception cref="ArgumentException">Thrown when subjectId or scheme is empty or whitespace.</exception>
    public AuthenticatedIdentity(
        string subjectId,
        string scheme,
        string? displayName = null,
        IEnumerable<KeyValuePair<string, IReadOnlyList<string>>>? claims = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(subjectId);
        ArgumentException.ThrowIfNullOrWhiteSpace(scheme);

        SubjectId = subjectId;
        Scheme = scheme;
        DisplayName = displayName;
        _claims = claims?.ToDictionary(kvp => kvp.Key, kvp => kvp.Value)
            ?? [];
    }

    /// <summary>
    /// Gets the stable subject identifier (unique user/principal ID).
    /// </summary>
    public string SubjectId { get; }

    /// <summary>
    /// Gets the authentication scheme used to authenticate this identity.
    /// </summary>
    public string Scheme { get; }

    /// <summary>
    /// Gets the optional display name for the identity.
    /// </summary>
    public string? DisplayName { get; }

    /// <summary>
    /// Gets all claims associated with this identity.
    /// </summary>
    public IReadOnlyDictionary<string, IReadOnlyList<string>> Claims => _claims;

    /// <summary>
    /// Gets the first value for a specific claim type.
    /// </summary>
    /// <param name="claimType">The claim type.</param>
    /// <returns>The first claim value, or null if not found.</returns>
    public string? GetClaimValue(string claimType)
    {
        return _claims.TryGetValue(claimType, out var values) && values.Count > 0
            ? values[0]
            : null;
    }

    /// <summary>
    /// Gets all values for a specific claim type.
    /// </summary>
    /// <param name="claimType">The claim type.</param>
    /// <returns>All claim values for the type, or empty if not found.</returns>
    public IReadOnlyList<string> GetClaimValues(string claimType)
    {
        return _claims.TryGetValue(claimType, out var values)
            ? values
            : [];
    }

    /// <summary>
    /// Checks if the identity has a specific claim type.
    /// </summary>
    /// <param name="claimType">The claim type.</param>
    /// <returns>True if the claim exists; otherwise false.</returns>
    public bool HasClaim(string claimType) => _claims.ContainsKey(claimType);

    /// <summary>
    /// Checks if the identity has a specific claim with a specific value.
    /// </summary>
    /// <param name="claimType">The claim type.</param>
    /// <param name="value">The expected value.</param>
    /// <returns>True if the claim exists with the specified value; otherwise false.</returns>
    public bool HasClaim(string claimType, string value)
    {
        return _claims.TryGetValue(claimType, out var values)
            && values.Contains(value, StringComparer.Ordinal);
    }
}
