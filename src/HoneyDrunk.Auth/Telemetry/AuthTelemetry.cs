namespace HoneyDrunk.Auth.Telemetry;

/// <summary>
/// Defines telemetry constants for Auth operations.
/// </summary>
public static class AuthTelemetry
{
    /// <summary>
    /// Activity name for authentication validation.
    /// </summary>
    public const string AuthenticateActivityName = "authn.validate";

    /// <summary>
    /// Activity name for authorization evaluation.
    /// </summary>
    public const string AuthorizeActivityName = "authz.evaluate";

    /// <summary>
    /// Result value indicating success.
    /// </summary>
    public const string ResultSuccess = "success";

    /// <summary>
    /// Result value indicating failure.
    /// </summary>
    public const string ResultFail = "fail";

    /// <summary>
    /// Result value indicating allowed.
    /// </summary>
    public const string ResultAllow = "allow";

    /// <summary>
    /// Result value indicating denied.
    /// </summary>
    public const string ResultDeny = "deny";

    /// <summary>
    /// Telemetry tag names for Auth operations.
    /// </summary>
    public static class Tags
    {
        /// <summary>
        /// Tag for authentication scheme.
        /// </summary>
        public const string Scheme = "auth.scheme";

        /// <summary>
        /// Tag for authentication result.
        /// </summary>
        public const string Result = "auth.result";

        /// <summary>
        /// Tag for authentication failure code.
        /// </summary>
        public const string FailureCode = "auth.failure_code";

        /// <summary>
        /// Tag for authorization result.
        /// </summary>
        public const string AuthzResult = "authz.result";

        /// <summary>
        /// Tag for authorization policy name.
        /// </summary>
        public const string Policy = "authz.policy";

        /// <summary>
        /// Tag for authorization failure code.
        /// </summary>
        public const string AuthzFailureCode = "authz.failure_code";
    }
}
