namespace HoneyDrunk.Auth.Canary;

/// <summary>
/// Exit codes for canary checks. Each category has a unique range.
/// </summary>
internal static class ExitCodes
{
    public const int Success = 0;

    // Registration guard failures (10-19)
    public const int GuardMissingKernel = 10;
    public const int GuardMissingVault = 11;

    // Happy path failures (20-29)
    public const int HappyPathFailed = 20;

    // Required claims failures (30-39)
    public const int MissingClaimWrongCode = 30;
    public const int MissingSubWrongCode = 31;

    // Caching failures (40-49)
    public const int CacheLastKnownGoodFailed = 40;
    public const int CacheTtlExpiryLkgOrVaultUnavailableFailed = 41;

    // Unknown kid failures (50-59)
    public const int UnknownKidRefreshFailed = 50;
    public const int UnknownKidVaultDownWrongCode = 51;

    // Authorization failures (60-69)
    public const int PolicyNotFoundIndistinguishable = 60;

    // Purity boundary failures (70-79)
    public const int PurityViolation = 70;

    // Secret boundary failures (80-89)
    public const int SecretBoundaryViolation = 80;

    // General unexpected error
    public const int UnexpectedError = 99;
}
