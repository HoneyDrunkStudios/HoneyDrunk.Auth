namespace HoneyDrunk.Auth.Abstractions;

/// <summary>
/// Represents a deny reason from an authorization decision.
/// </summary>
/// <param name="Code">The deny reason code.</param>
/// <param name="Message">The deny reason message.</param>
public readonly record struct DenyReason(AuthorizationDenyCode Code, string Message);
