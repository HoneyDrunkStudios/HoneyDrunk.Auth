using HoneyDrunk.Auth.Abstractions;
using HoneyDrunk.Auth.Secrets;
using HoneyDrunk.Auth.Telemetry;
using HoneyDrunk.Kernel.Abstractions.Telemetry;
using Microsoft.Extensions.Logging;
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
/// <param name="telemetryFactory">The telemetry activity factory.</param>
/// <param name="logger">The logger.</param>
public sealed class BearerTokenAuthenticationProvider(
    ISigningKeyProvider keyProvider,
    ITelemetryActivityFactory telemetryFactory,
    ILogger<BearerTokenAuthenticationProvider> logger) : IAuthenticationProvider
{
    private readonly ISigningKeyProvider _keyProvider = keyProvider ?? throw new ArgumentNullException(nameof(keyProvider));
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
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Authentication failed with unexpected error");
            return RecordFailure(activity, AuthenticationFailureCode.InternalError, "Internal authentication error");
        }
    }

    private static AuthenticatedIdentity CreateIdentityFromToken(TokenValidationResult result)
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

        var subjectId = claims.GetValueOrDefault(AuthClaimTypes.Subject)?.FirstOrDefault()
            ?? claims.GetValueOrDefault(JwtRegisteredClaimNames.Sub)?.FirstOrDefault()
            ?? throw new InvalidOperationException("Token missing subject claim");

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
                AuthenticationFailureCode.InvalidSignature, exception.Message),
        };
    }

    private async Task<AuthenticationResult> ValidateTokenAsync(string token, CancellationToken cancellationToken)
    {
        var signingKeys = await _keyProvider.GetSigningKeysAsync(cancellationToken);
        if (signingKeys.Count == 0)
        {
            _logger.LogError("No signing keys available for token validation");
            return AuthenticationResult.Fail(AuthenticationFailureCode.InternalError, "No signing keys available");
        }

        var issuer = await _keyProvider.GetIssuerAsync(cancellationToken);
        var audience = await _keyProvider.GetAudienceAsync(cancellationToken);
        var clockSkew = await _keyProvider.GetClockSkewAsync(cancellationToken);

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

        var identity = CreateIdentityFromToken(result);
        _logger.LogDebug("Successfully authenticated subject {SubjectId}", identity.SubjectId);

        return AuthenticationResult.Success(identity);
    }

    private AuthenticationResult RecordFailure(
        System.Diagnostics.Activity? activity,
        AuthenticationFailureCode code,
        string message)
    {
        _logger.LogWarning("Authentication failed: {FailureCode} - {Message}", code, message);

        if (activity != null)
        {
            activity.SetTag(AuthTelemetry.Tags.Result, AuthTelemetry.ResultFail);
            activity.SetTag(AuthTelemetry.Tags.FailureCode, code.ToString());
        }

        return AuthenticationResult.Fail(code, message);
    }
}
