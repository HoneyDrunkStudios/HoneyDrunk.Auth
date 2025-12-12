namespace HoneyDrunk.Auth.Secrets;

/// <summary>
/// Represents a signing key retrieved from Vault.
/// </summary>
/// <param name="KeyId">The key identifier (kid).</param>
/// <param name="Algorithm">The signing algorithm.</param>
/// <param name="KeyMaterial">The Base64-encoded key material.</param>
/// <param name="IsActive">Whether this key is active for validation.</param>
public sealed record SigningKeyInfo(string KeyId, string Algorithm, string KeyMaterial, bool IsActive);
