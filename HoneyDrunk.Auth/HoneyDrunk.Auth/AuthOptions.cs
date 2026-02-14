using System.Collections.ObjectModel;

namespace HoneyDrunk.Auth;

/// <summary>
/// Configuration options for HoneyDrunk Auth.
/// </summary>
public sealed class AuthOptions
{
    private readonly List<string> _requiredClaims = ["sub"];

    /// <summary>
    /// Gets or sets the cache time-to-live for signing keys and configuration.
    /// Default is 5 minutes.
    /// </summary>
    public TimeSpan CacheTtl { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Gets the list of required claims that must be present in tokens.
    /// Default includes "sub" (subject) only.
    /// </summary>
    public Collection<string> RequiredClaims => new(_requiredClaims);

    /// <summary>
    /// Gets or sets a value indicating whether to attempt a cache refresh when an unknown key ID is encountered.
    /// Default is true.
    /// </summary>
    public bool RefreshOnUnknownKeyId { get; set; } = true;
}
