using HoneyDrunk.Auth.Abstractions;
using HoneyDrunk.Auth.Secrets;
using HoneyDrunk.Auth.Telemetry;
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
/// <param name="logger">The logger.</param>
public sealed class BearerTokenAuthenticationProvider(
    ISigningKeyProvider keyProvider,
    IOptions<AuthOptions> options,
    ITelemetryActivityFactory telemetryFactory,
    ILogger<BearerTokenAuthenticationProvider> logger) : IAuthenticationProvider
{
    private readonly ISigningKeyProvider _keyProvider = keyProvider ?? throw new ArgumentNullException(nameof(keyProvider));
    private readonly AuthOptions _options = options?.Value ?? new AuthOptions();
    private readonly ITelemetryActivityFactory _telemetryFactory = telemetryFactory ?? throw new ArgumentNullException(nameof(telemetryFactory));
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
            return validationResult;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (AuthenticationException ex)
        {
            // Rethrow known authentication exceptions with proper codes
            return RecordFailure(activity, ex.FailureCode, ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Authentication failed with unexpected error");
            return RecordFailure(activity, AuthenticationFailureCode.InternalError, "Unexpected authentication error");
        }
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
                foreach (var item in arrayValue)
                {
                    var itemValue = item?.ToString() ?? string.Empty;
                    values.Add(itemValue);
                }
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
            ?? string.Empty; // Should not happen if RequiredClaims includes "sub"

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

    private async Task<AuthenticationResult> ValidateTokenAsync(string token, CancellationToken cancellationToken)
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
            throw new AuthenticationException(AuthenticationFailureCode.VaultUnavailable, "Authentication service temporarily unavailable");
        }

        if (signingKeys.Count == 0)
        {
            _logger.LogError("No signing keys available for token validation");
            throw new AuthenticationException(AuthenticationFailureCode.ConfigurationError, "Authentication service is misconfigured");
        }

        if (string.IsNullOrWhiteSpace(issuer))
        {
            _logger.LogError("Issuer is not configured");
            throw new AuthenticationException(AuthenticationFailureCode.ConfigurationError, "Authentication service is misconfigured");
        }

        if (string.IsNullOrWhiteSpace(audience))
        {
            _logger.LogError("Audience is not configured");
            throw new AuthenticationException(AuthenticationFailureCode.ConfigurationError, "Authentication service is misconfigured");
        }

        var validationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = issuer,
            ValidateAudience = true,
            ValidAudience = audience,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKeys = signingKeys,
            ClockSkew = clockSkew,
        };

        var handler = new JsonWebTokenHandler();
        var result = await handler.ValidateTokenAsync(token, validationParameters);

        if (!result.IsValid)
        {
            return MapValidationException(result.Exception);
        }

        // Extract claims
        var claims = ExtractClaims(result);

        // Validate required claims
        var missingClaims = ValidateRequiredClaims(claims);
        if (missingClaims.Count > 0)
        {
            return AuthenticationResult.Fail(
                AuthenticationFailureCode.MissingClaim,
                $"Required claim(s) missing: {string.Join(", ", missingClaims)}");
        }

        // Build identity
        var identity = CreateIdentityFromClaims(claims);

        // Do not log subject IDs at info/warning level to avoid leaking sensitive information
        _logger.LogDebug("Successfully authenticated identity");

        return AuthenticationResult.Success(identity);
    }

    private List<string> ValidateRequiredClaims(Dictionary<string, List<string>> claims)
    {
        var missing = new List<string>();

        foreach (var requiredClaim in _options.RequiredClaims)
        {
            // Check common aliases for 'sub'
            if (string.Equals(requiredClaim, "sub", StringComparison.OrdinalIgnoreCase))
            {
                var hasSubject = claims.ContainsKey(AuthClaimTypes.Subject) ||
                                 claims.ContainsKey(JwtRegisteredClaimNames.Sub);
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

    /// <summary>
    /// Internal exception for propagating authentication failures with proper codes.
    /// </summary>
    private sealed class AuthenticationException : Exception
    {
        public AuthenticationException(AuthenticationFailureCode failureCode, string message)
            : base(message)
        {
            FailureCode = failureCode;
        }

        public AuthenticationFailureCode FailureCode { get; }
    }
}
