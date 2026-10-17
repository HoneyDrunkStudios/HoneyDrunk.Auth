using HoneyDrunk.Audit.Abstractions;
using HoneyDrunk.Auth.Abstractions;
using HoneyDrunk.Auth.Audit;
using HoneyDrunk.Auth.Secrets;
using HoneyDrunk.Auth.Telemetry;
using HoneyDrunk.Kernel.Abstractions.Context;
using HoneyDrunk.Kernel.Abstractions.Telemetry;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace HoneyDrunk.Auth.Authentication;

/// <summary>
/// Bearer token authentication provider using JWT validation.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="BearerTokenAuthenticationProvider"/> class.
/// </remarks>
/// <param name="keyProvider">The signing key provider.</param>
/// <param name="options">The auth options.</param>
/// <param name="telemetryFactory">The telemetry activity factory.</param>
/// <param name="auditLog">The audit log used to append token validation outcomes.</param>
/// <param name="gridContextAccessor">The Grid context accessor used for correlation and tenant context.</param>
/// <param name="logger">The logger.</param>
public sealed class BearerTokenAuthenticationProvider(
    ISigningKeyProvider keyProvider,
    IOptions<AuthOptions> options,
    ITelemetryActivityFactory telemetryFactory,
    IAuditLog auditLog,
    IGridContextAccessor gridContextAccessor,
    ILogger<BearerTokenAuthenticationProvider> logger) : IAuthenticationProvider
{
    internal static readonly HashSet<string> AuditAllowedClaims = new(StringComparer.Ordinal)
    {
        JwtRegisteredClaimNames.Jti,
        JwtRegisteredClaimNames.Iss,
        JwtRegisteredClaimNames.Aud,
        JwtRegisteredClaimNames.Exp,
    };

    private readonly ISigningKeyProvider _keyProvider = keyProvider ?? throw new ArgumentNullException(nameof(keyProvider));
    private readonly AuthOptions _options = options?.Value ?? new AuthOptions();
    private readonly ITelemetryActivityFactory _telemetryFactory = telemetryFactory ?? throw new ArgumentNullException(nameof(telemetryFactory));
    private readonly IAuditLog _auditLog = auditLog ?? throw new ArgumentNullException(nameof(auditLog));
    private readonly IGridContextAccessor _gridContextAccessor = gridContextAccessor ?? throw new ArgumentNullException(nameof(gridContextAccessor));
    private readonly ILogger<BearerTokenAuthenticationProvider> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

    /// <inheritdoc />
    public IReadOnlyList<string> SupportedSchemes => [AuthScheme.Bearer];

    /// <inheritdoc />
    public async Task<AuthenticationResult> AuthenticateAsync(
        AuthCredential credential,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(credential);

        using var activity = _telemetryFactory.Start(
            AuthTelemetry.AuthenticateActivityName,
            new Dictionary<string, object?>
            {
                [AuthTelemetry.Tags.Scheme] = credential.Scheme,
            });

        try
        {
            if (!string.Equals(credential.Scheme, AuthScheme.Bearer, StringComparison.OrdinalIgnoreCase))
            {
                return RecordFailure(activity, AuthenticationFailureCode.UnsupportedScheme, $"Scheme '{credential.Scheme}' is not supported");
            }

            var validationResult = await ValidateTokenAsync(credential.Value, cancellationToken);
            await EmitTokenValidationAuditAsync(validationResult, credential.Value, cancellationToken);
            return validationResult;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (BearerAuthenticationException ex)
        {
            // Rethrow known authentication exceptions with proper codes
            var failure = RecordFailure(activity, ex.FailureCode, ex.Message);
            await EmitTokenValidationAuditAsync(failure, credential.Value, cancellationToken);
            return failure;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Authentication failed with unexpected error");
            var failure = RecordFailure(activity, AuthenticationFailureCode.InternalError, "Unexpected authentication error");
            await EmitTokenValidationAuditAsync(failure, credential.Value, cancellationToken);
            return failure;
        }
    }

    private static IReadOnlyDictionary<string, string> BuildValidationMetadata(AuthenticationResult result, string token)
    {
        var metadata = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["scheme"] = AuthScheme.Bearer,
        };

        foreach (var claim in GetAllowedAuditClaims(result, token))
        {
            metadata["claim." + claim.Key] = claim.Value;
        }

        if (!result.IsAuthenticated)
        {
            metadata["failureCode"] = result.FailureCode.ToString();
            if (!string.IsNullOrWhiteSpace(result.FailureMessage))
            {
                metadata["failureMessage"] = result.FailureMessage;
            }
        }

        return AuditMetadata.Cap(metadata);
    }

    private static Dictionary<string, string> GetAllowedAuditClaims(AuthenticationResult result, string token)
    {
        return result.Identity is { } identity
            ? GetAllowedAuditClaims(identity)
            : TryReadAllowedClaims(token);
    }

    private static Dictionary<string, string> GetAllowedAuditClaims(AuthenticatedIdentity identity)
    {
        var metadata = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var claimType in AuditAllowedClaims)
        {
            var values = identity.GetClaimValues(claimType);
            if (values.Count > 0)
            {
                metadata[claimType] = string.Join(",", values);
            }
        }

        return metadata;
    }

    private static string GetAuditTokenTargetId(AuthenticationResult result, string token)
    {
        if (result.Identity is { } identity)
        {
            return identity.GetClaimValue(JwtRegisteredClaimNames.Jti) is { Length: > 0 } identityJti
                ? identityJti
                : "unavailable";
        }

        var parsedClaims = TryReadAllowedClaims(token);
        return parsedClaims.TryGetValue(JwtRegisteredClaimNames.Jti, out var jti) && !string.IsNullOrWhiteSpace(jti)
            ? jti
            : "unavailable";
    }

    private static Dictionary<string, string> TryReadAllowedClaims(string token)
    {
        var metadata = new Dictionary<string, string>(StringComparer.Ordinal);

        try
        {
            var jwt = new JsonWebTokenHandler().ReadJsonWebToken(token);
            foreach (var group in jwt.Claims
                .Where(claim => AuditAllowedClaims.Contains(claim.Type))
                .GroupBy(claim => claim.Type, StringComparer.Ordinal))
            {
                metadata[group.Key] = string.Join(",", group.Select(claim => claim.Value));
            }
        }
        catch (Exception)
        {
            // Malformed tokens still produce a validation-denied audit entry without token text.
        }

        return metadata;
    }

    private static Dictionary<string, List<string>> ExtractClaims(TokenValidationResult result)
    {
        var claims = new Dictionary<string, List<string>>();

        foreach (var claim in result.Claims)
        {
            if (!claims.TryGetValue(claim.Key, out var values))
            {
                values = [];
                claims[claim.Key] = values;
            }

            // Handle claims that are arrays (multiple values for same key)
            if (claim.Value is IEnumerable<object> arrayValue)
            {
                values.AddRange(arrayValue.Select(item => item?.ToString() ?? string.Empty));
            }
            else
            {
                var claimValue = claim.Value?.ToString() ?? string.Empty;

                // Handle space-separated scopes
                if (string.Equals(claim.Key, AuthClaimTypes.Scope, StringComparison.OrdinalIgnoreCase)
                    && claimValue.Contains(' ', StringComparison.Ordinal))
                {
                    values.AddRange(claimValue.Split(' ', StringSplitOptions.RemoveEmptyEntries));
                }
                else
                {
                    values.Add(claimValue);
                }
            }
        }

        return claims;
    }

    private static AuthenticatedIdentity CreateIdentityFromClaims(Dictionary<string, List<string>> claims)
    {
        var subjectId = claims.GetValueOrDefault(AuthClaimTypes.Subject)?.FirstOrDefault()
            ?? claims.GetValueOrDefault(JwtRegisteredClaimNames.Sub)?.FirstOrDefault()
            ?? throw new InvalidOperationException("Token missing subject claim after validation");

        var displayName = claims.GetValueOrDefault(AuthClaimTypes.Name)?.FirstOrDefault()
            ?? claims.GetValueOrDefault(JwtRegisteredClaimNames.Name)?.FirstOrDefault();

        var claimsReadOnly = claims.ToDictionary(
            kvp => kvp.Key,
            kvp => (IReadOnlyList<string>)kvp.Value.AsReadOnly());

        return new AuthenticatedIdentity(subjectId, AuthScheme.Bearer, displayName, claimsReadOnly);
    }

    private static AuthenticationResult MapValidationException(Exception? exception)
    {
        return exception switch
        {
            SecurityTokenExpiredException => AuthenticationResult.Fail(
                AuthenticationFailureCode.TokenExpired, "Token has expired"),

            SecurityTokenNotYetValidException => AuthenticationResult.Fail(
                AuthenticationFailureCode.TokenNotYetValid, "Token is not yet valid"),

            SecurityTokenInvalidSignatureException => AuthenticationResult.Fail(
                AuthenticationFailureCode.InvalidSignature, "Token signature is invalid"),

            SecurityTokenInvalidIssuerException => AuthenticationResult.Fail(
                AuthenticationFailureCode.InvalidIssuer, "Token issuer is not trusted"),

            SecurityTokenInvalidAudienceException => AuthenticationResult.Fail(
                AuthenticationFailureCode.InvalidAudience, "Token audience is invalid"),

            SecurityTokenMalformedException => AuthenticationResult.Fail(
                AuthenticationFailureCode.MalformedCredential, "Token format is invalid"),

            null => AuthenticationResult.Fail(
                AuthenticationFailureCode.InvalidSignature, "Token validation failed"),

            _ => AuthenticationResult.Fail(
                AuthenticationFailureCode.InvalidSignature, "Token validation failed"),
        };
    }

    private static bool IsSignatureFailure(Exception? exception)
    {
        return exception is SecurityTokenInvalidSignatureException
            or SecurityTokenSignatureKeyNotFoundException;
    }

    private static string? TryExtractKeyId(string token)
    {
        try
        {
            var handler = new JsonWebTokenHandler();
            var jwt = handler.ReadJsonWebToken(token);
            return string.IsNullOrEmpty(jwt.Kid) ? null : jwt.Kid;
        }
        catch
        {
            return null;
        }
    }

    private static TokenValidationParameters BuildValidationParameters(ValidationConfiguration config)
    {
        return new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = config.Issuer,
            ValidateAudience = true,
            ValidAudience = config.Audience,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKeys = config.SigningKeys,
            ClockSkew = config.ClockSkew,
        };
    }

    private async Task EmitTokenValidationAuditAsync(
        AuthenticationResult result,
        string token,
        CancellationToken cancellationToken)
    {
        try
        {
            var gridContext = _gridContextAccessor.GridContext;
            await _auditLog.AppendAsync(
                new AuditEntry(
                    AuditEntryId.Empty,
                    DateTimeOffset.UtcNow,
                    result.Identity?.SubjectId ?? "anonymous",
                    "auth.token.validate",
                    AuditCategory.Security,
                    result.IsAuthenticated ? AuditOutcome.Succeeded : AuditOutcome.Denied,
                    new AuditTarget("auth.token", GetAuditTokenTargetId(result, token)),
                    gridContext.TenantId,
                    gridContext.CorrelationId,
                    Metadata: BuildValidationMetadata(result, token),
                    Reason: result.FailureCode == AuthenticationFailureCode.None ? null : result.FailureCode.ToString()),
                cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Audit emission failed for token validation; authentication outcome is unchanged");
        }
    }

    private async Task<AuthenticationResult> ValidateTokenAsync(string token, CancellationToken cancellationToken)
    {
        var config = await LoadValidationConfigurationAsync(cancellationToken);
        var validationParameters = BuildValidationParameters(config);

        var handler = new JsonWebTokenHandler();
        var result = await handler.ValidateTokenAsync(token, validationParameters);

        if (!result.IsValid)
        {
            var resolvedResult = await TryResolveSignatureFailureAsync(
                handler, token, validationParameters, result, cancellationToken);
            if (resolvedResult is null)
            {
                return MapValidationException(result.Exception);
            }

            if (!resolvedResult.IsValid)
            {
                return MapValidationException(resolvedResult.Exception);
            }

            result = resolvedResult;
        }

        return BuildIdentityResult(result);
    }

    private async Task<ValidationConfiguration> LoadValidationConfigurationAsync(CancellationToken cancellationToken)
    {
        IReadOnlyList<SecurityKey> signingKeys;
        string issuer;
        string audience;
        TimeSpan clockSkew;

        try
        {
            signingKeys = await _keyProvider.GetSigningKeysAsync(cancellationToken);
            issuer = await _keyProvider.GetIssuerAsync(cancellationToken);
            audience = await _keyProvider.GetAudienceAsync(cancellationToken);
            clockSkew = await _keyProvider.GetClockSkewAsync(cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Failed to retrieve authentication configuration from Vault");
            throw new BearerAuthenticationException(AuthenticationFailureCode.VaultUnavailable, "Authentication service temporarily unavailable");
        }

        if (signingKeys.Count == 0)
        {
            _logger.LogError("No signing keys available for token validation");
            throw new BearerAuthenticationException(AuthenticationFailureCode.ConfigurationError, "Authentication service is misconfigured");
        }

        if (string.IsNullOrWhiteSpace(issuer))
        {
            _logger.LogError("Issuer is not configured");
            throw new BearerAuthenticationException(AuthenticationFailureCode.ConfigurationError, "Authentication service is misconfigured");
        }

        if (string.IsNullOrWhiteSpace(audience))
        {
            _logger.LogError("Audience is not configured");
            throw new BearerAuthenticationException(AuthenticationFailureCode.ConfigurationError, "Authentication service is misconfigured");
        }

        return new ValidationConfiguration(signingKeys, issuer, audience, clockSkew);
    }

    /// <summary>
    /// When the initial validation failed due to an unknown signing key and a caching
    /// provider is in play, refresh the cache and retry once. Returns the retry result
    /// (which may itself be invalid), or <c>null</c> when no refresh path applied.
    /// </summary>
    private async Task<TokenValidationResult?> TryResolveSignatureFailureAsync(
        JsonWebTokenHandler handler,
        string token,
        TokenValidationParameters validationParameters,
        TokenValidationResult failedResult,
        CancellationToken cancellationToken)
    {
        if (!IsSignatureFailure(failedResult.Exception) ||
            _keyProvider is not CachingSigningKeyProvider cachingProvider)
        {
            return null;
        }

        var kid = TryExtractKeyId(token);
        if (kid is null || !await cachingProvider.TryRefreshForUnknownKeyIdAsync(kid, cancellationToken))
        {
            return null;
        }

        var refreshedKeys = await cachingProvider.GetSigningKeysAsync(cancellationToken);
        validationParameters.IssuerSigningKeys = refreshedKeys;

        return await handler.ValidateTokenAsync(token, validationParameters);
    }

    private AuthenticationResult BuildIdentityResult(TokenValidationResult result)
    {
        var claims = ExtractClaims(result);

        var missingClaims = ValidateRequiredClaims(claims);
        if (missingClaims.Count > 0)
        {
            return AuthenticationResult.Fail(
                AuthenticationFailureCode.MissingClaim,
                $"Required claim(s) missing: {string.Join(", ", missingClaims)}");
        }

        var identity = CreateIdentityFromClaims(claims);

        // Do not log subject IDs at info/warning level to avoid leaking sensitive information
        _logger.LogDebug("Successfully authenticated identity");

        return AuthenticationResult.Success(identity);
    }

    private List<string> ValidateRequiredClaims(Dictionary<string, List<string>> claims)
    {
        var missing = new List<string>();

        // "sub" is always required for identity construction, even if not explicitly configured
        var hasSubject = claims.ContainsKey(AuthClaimTypes.Subject) ||
                         claims.ContainsKey(JwtRegisteredClaimNames.Sub);
        var subAlreadyChecked = false;

        foreach (var requiredClaim in _options.RequiredClaims)
        {
            // Check common aliases for 'sub'
            if (string.Equals(requiredClaim, "sub", StringComparison.OrdinalIgnoreCase))
            {
                subAlreadyChecked = true;
                if (!hasSubject)
                {
                    missing.Add(requiredClaim);
                }
            }
            else if (!claims.ContainsKey(requiredClaim))
            {
                missing.Add(requiredClaim);
            }
        }

        // Enforce "sub" even if the caller removed it from RequiredClaims
        if (!subAlreadyChecked && !hasSubject)
        {
            missing.Add("sub");
        }

        return missing;
    }

    private AuthenticationResult RecordFailure(
        System.Diagnostics.Activity? activity,
        AuthenticationFailureCode code,
        string message)
    {
        // Log at debug level to avoid leaking sensitive information
        _logger.LogDebug("Authentication failed: {FailureCode}", code);

        if (activity != null)
        {
            activity.SetTag(AuthTelemetry.Tags.Result, AuthTelemetry.ResultFail);
            activity.SetTag(AuthTelemetry.Tags.FailureCode, code.ToString());
        }

        return AuthenticationResult.Fail(code, message);
    }

    private readonly record struct ValidationConfiguration(
        IReadOnlyList<SecurityKey> SigningKeys,
        string Issuer,
        string Audience,
        TimeSpan ClockSkew);
}
