namespace HoneyDrunk.Auth.Abstractions;

/// <summary>
/// Represents the result of an authorization evaluation.
/// </summary>
public sealed class AuthorizationDecision
{
    private AuthorizationDecision(
        bool isAllowed,
        IReadOnlyList<DenyReason> denyReasons,
        IReadOnlyList<string> satisfiedRequirements)
    {
        IsAllowed = isAllowed;
        DenyReasons = denyReasons;
        SatisfiedRequirements = satisfiedRequirements;
    }

    /// <summary>
    /// Gets a value indicating whether the request is allowed.
    /// </summary>
    public bool IsAllowed { get; }

    /// <summary>
    /// Gets the deny reasons when authorization failed.
    /// </summary>
    public IReadOnlyList<DenyReason> DenyReasons { get; }

    /// <summary>
    /// Gets the list of satisfied requirements for auditability.
    /// </summary>
    public IReadOnlyList<string> SatisfiedRequirements { get; }

    /// <summary>
    /// Creates an allowed authorization decision.
    /// </summary>
    /// <param name="satisfiedRequirements">The requirements that were satisfied.</param>
    /// <returns>An allowed authorization decision.</returns>
    public static AuthorizationDecision Allow(IEnumerable<string>? satisfiedRequirements = null)
    {
        var requirements = satisfiedRequirements?.ToList().AsReadOnly() ?? (IReadOnlyList<string>)[];
        return new AuthorizationDecision(true, [], requirements);
    }

    /// <summary>
    /// Creates a denied authorization decision.
    /// </summary>
    /// <param name="denyReasons">The reasons for denial.</param>
    /// <param name="satisfiedRequirements">Any requirements that were satisfied before denial.</param>
    /// <returns>A denied authorization decision.</returns>
    public static AuthorizationDecision Deny(
        IEnumerable<DenyReason> denyReasons,
        IEnumerable<string>? satisfiedRequirements = null)
    {
        var reasons = denyReasons.ToList().AsReadOnly();
        var requirements = satisfiedRequirements?.ToList().AsReadOnly() ?? (IReadOnlyList<string>)[];
        return new AuthorizationDecision(false, reasons, requirements);
    }

    /// <summary>
    /// Creates a denied authorization decision with a single reason.
    /// </summary>
    /// <param name="code">The deny code.</param>
    /// <param name="message">The deny message.</param>
    /// <returns>A denied authorization decision.</returns>
    public static AuthorizationDecision Deny(AuthorizationDenyCode code, string message)
    {
        return new AuthorizationDecision(false, [new DenyReason(code, message)], []);
    }
}
