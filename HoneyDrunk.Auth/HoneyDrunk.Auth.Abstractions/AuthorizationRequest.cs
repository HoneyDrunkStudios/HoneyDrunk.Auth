namespace HoneyDrunk.Auth.Abstractions;

/// <summary>
/// Represents a request for authorization evaluation.
/// </summary>
public sealed class AuthorizationRequest
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AuthorizationRequest"/> class.
    /// </summary>
    /// <param name="action">The action being performed.</param>
    /// <param name="resource">The resource being accessed.</param>
    /// <param name="requiredScopes">The scopes required for the action.</param>
    /// <param name="requiredRoles">The roles required for the action.</param>
    /// <param name="resourceOwnerId">The optional resource owner ID for ownership checks.</param>
    public AuthorizationRequest(
        string action,
        string resource,
        IEnumerable<string>? requiredScopes = null,
        IEnumerable<string>? requiredRoles = null,
        string? resourceOwnerId = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(action);
        ArgumentException.ThrowIfNullOrWhiteSpace(resource);

        Action = action;
        Resource = resource;
        RequiredScopes = requiredScopes?.ToList().AsReadOnly() ?? (IReadOnlyList<string>)[];
        RequiredRoles = requiredRoles?.ToList().AsReadOnly() ?? (IReadOnlyList<string>)[];
        ResourceOwnerId = resourceOwnerId;
    }

    /// <summary>
    /// Gets the action being performed (e.g., "read", "write", "delete").
    /// </summary>
    public string Action { get; }

    /// <summary>
    /// Gets the resource being accessed (e.g., "projects", "users/{id}").
    /// </summary>
    public string Resource { get; }

    /// <summary>
    /// Gets the scopes required for this action.
    /// </summary>
    public IReadOnlyList<string> RequiredScopes { get; }

    /// <summary>
    /// Gets the roles required for this action.
    /// </summary>
    public IReadOnlyList<string> RequiredRoles { get; }

    /// <summary>
    /// Gets the optional resource owner ID for ownership-based authorization.
    /// </summary>
    public string? ResourceOwnerId { get; }
}
