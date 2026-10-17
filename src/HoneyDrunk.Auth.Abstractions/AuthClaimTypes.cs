namespace HoneyDrunk.Auth.Abstractions;

/// <summary>
/// Defines standard claim types used by the Auth system.
/// </summary>
public static class AuthClaimTypes
{
    /// <summary>
    /// The subject (user) identifier claim.
    /// </summary>
    public const string Subject = "sub";

    /// <summary>
    /// The name claim.
    /// </summary>
    public const string Name = "name";

    /// <summary>
    /// The email claim.
    /// </summary>
    public const string Email = "email";

    /// <summary>
    /// The role claim.
    /// </summary>
    public const string Role = "role";

    /// <summary>
    /// The scope claim.
    /// </summary>
    public const string Scope = "scope";

    /// <summary>
    /// The tenant identifier claim.
    /// </summary>
    public const string TenantId = "tenant_id";

    /// <summary>
    /// The project identifier claim.
    /// </summary>
    public const string ProjectId = "project_id";

    /// <summary>
    /// The issuer claim.
    /// </summary>
    public const string Issuer = "iss";

    /// <summary>
    /// The audience claim.
    /// </summary>
    public const string Audience = "aud";

    /// <summary>
    /// The expiration time claim.
    /// </summary>
    public const string ExpirationTime = "exp";

    /// <summary>
    /// The not-before time claim.
    /// </summary>
    public const string NotBefore = "nbf";

    /// <summary>
    /// The issued-at time claim.
    /// </summary>
    public const string IssuedAt = "iat";

    /// <summary>
    /// The JWT ID claim.
    /// </summary>
    public const string JwtId = "jti";
}
