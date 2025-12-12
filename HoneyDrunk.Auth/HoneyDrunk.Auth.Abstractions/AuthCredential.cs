namespace HoneyDrunk.Auth.Abstractions;

/// <summary>
/// Represents an authentication credential for validation.
/// </summary>
/// <remarks>
/// Designed to be extensible for future credential types beyond Bearer tokens.
/// </remarks>
public sealed class AuthCredential
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AuthCredential"/> class.
    /// </summary>
    /// <param name="scheme">The authentication scheme.</param>
    /// <param name="value">The credential value.</param>
    /// <exception cref="ArgumentNullException">Thrown when scheme or value is null.</exception>
    /// <exception cref="ArgumentException">Thrown when scheme or value is empty or whitespace.</exception>
    public AuthCredential(string scheme, string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(scheme);
        ArgumentException.ThrowIfNullOrWhiteSpace(value);

        Scheme = scheme;
        Value = value;
    }

    /// <summary>
    /// Gets the authentication scheme (e.g., "Bearer").
    /// </summary>
    public string Scheme { get; }

    /// <summary>
    /// Gets the credential value (e.g., the JWT token string).
    /// </summary>
    public string Value { get; }

    /// <summary>
    /// Creates a Bearer token credential.
    /// </summary>
    /// <param name="token">The JWT bearer token.</param>
    /// <returns>A new <see cref="AuthCredential"/> with Bearer scheme.</returns>
    public static AuthCredential Bearer(string token) => new(AuthScheme.Bearer, token);
}
