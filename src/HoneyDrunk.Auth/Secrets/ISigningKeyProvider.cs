using Microsoft.IdentityModel.Tokens;

namespace HoneyDrunk.Auth.Secrets;

/// <summary>
/// Provides signing keys for token validation from Vault.
/// </summary>
public interface ISigningKeyProvider
{
    /// <summary>
    /// Gets all active signing keys for token validation.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>Collection of security keys for validation.</returns>
    Task<IReadOnlyList<SecurityKey>> GetSigningKeysAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the expected issuer for token validation.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The expected issuer string.</returns>
    Task<string> GetIssuerAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the expected audience for token validation.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The expected audience string.</returns>
    Task<string> GetAudienceAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the clock skew tolerance for token validation.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The clock skew tolerance.</returns>
    Task<TimeSpan> GetClockSkewAsync(CancellationToken cancellationToken = default);
}
