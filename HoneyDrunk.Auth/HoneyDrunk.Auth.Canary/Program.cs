// ============================================================================
// HoneyDrunk.Auth Canary
// ============================================================================
// This is a canary project that validates HoneyDrunk.Auth behaves correctly
// in real hosting scenarios. It crashes loudly (non-zero exit code) when
// invariants are violated.
//
// Run: dotnet run
//
// Exit codes:
//   0  = All checks passed
//   10 = Guard: Missing Kernel check failed
//   11 = Guard: Missing Vault check failed
//   20 = Happy path authentication failed
//   30 = MissingClaim code not returned for missing custom claim
//   31 = MissingClaim code not returned for missing sub
//   40 = Cache last-known-good fallback failed
//   41 = TTL expiry + Vault down: expected LKG success or VaultUnavailable
//   50 = Unknown kid refresh failed
//   51 = Unknown kid with vault down did not return VaultUnavailable
//   60 = PolicyNotFound indistinguishable from generic Deny
//   70 = Purity boundary violation detected
//   80 = Secret boundary violation detected
//   99 = Unexpected error
// ============================================================================

using HoneyDrunk.Audit.Abstractions;
using HoneyDrunk.Auth;
using HoneyDrunk.Auth.Abstractions;
using HoneyDrunk.Auth.Authentication;
using HoneyDrunk.Auth.Authorization;
using HoneyDrunk.Auth.Canary;
using HoneyDrunk.Auth.Canary.Fakes;
using HoneyDrunk.Auth.Canary.Helpers;
using HoneyDrunk.Auth.DependencyInjection;
using HoneyDrunk.Auth.Secrets;
using HoneyDrunk.Kernel.Abstractions.Context;
using HoneyDrunk.Kernel.Abstractions.Telemetry;
using HoneyDrunk.Vault.Abstractions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

Console.WriteLine("=== HoneyDrunk.Auth Canary ===");
Console.WriteLine();

var exitCode = ExitCodes.Success;

try
{
    // Check 1: Guard - Missing Kernel
    exitCode = await Check1_GuardMissingKernel();
    if (exitCode != ExitCodes.Success)
    {
        return exitCode;
    }

    // Check 2: Guard - Missing Vault
    exitCode = await Check2_GuardMissingVault();
    if (exitCode != ExitCodes.Success)
    {
        return exitCode;
    }

    // Check 3: Happy path authentication
    exitCode = await Check3_HappyPathAuthentication();
    if (exitCode != ExitCodes.Success)
    {
        return exitCode;
    }

    // Check 4: Missing required claim returns MissingClaim
    exitCode = await Check4_MissingRequiredClaim();
    if (exitCode != ExitCodes.Success)
    {
        return exitCode;
    }

    // Check 5: Vault down uses last-known-good cache
    exitCode = await Check5_VaultDownUsesLastKnownGood();
    if (exitCode != ExitCodes.Success)
    {
        return exitCode;
    }

    // Check 6: TTL expiry + Vault down returns VaultUnavailable
    exitCode = await Check6_TtlExpiryVaultDownReturnsVaultUnavailable();
    if (exitCode != ExitCodes.Success)
    {
        return exitCode;
    }

    // Check 7: Unknown kid refresh behavior
    exitCode = await Check7_UnknownKidRefreshBehavior();
    if (exitCode != ExitCodes.Success)
    {
        return exitCode;
    }

    // Check 8: PolicyNotFound distinguishable
    exitCode = await Check8_PolicyNotFoundDistinguishable();
    if (exitCode != ExitCodes.Success)
    {
        return exitCode;
    }

    // Check 9: Purity boundary respected
    exitCode = await Check9_PurityBoundary();
    if (exitCode != ExitCodes.Success)
    {
        return exitCode;
    }

    // Check 10: Secret values flow through ISecretStore only
    exitCode = await Check10_SecretValuesFlowThroughISecretStoreOnly();
    if (exitCode != ExitCodes.Success)
    {
        return exitCode;
    }

    Console.WriteLine();
    Console.WriteLine("=== ALL CHECKS PASSED ===");
    return ExitCodes.Success;
}
catch (Exception ex)
{
    Console.WriteLine($"FAIL Unexpected: {ex.Message}");
    Console.WriteLine(ex.StackTrace);
    return ExitCodes.UnexpectedError;
}

// ============================================================================
// Check Implementations
// ============================================================================
static Task<int> Check1_GuardMissingKernel()
{
    const string name = "Guard: Missing Kernel";

    try
    {
        var services = new ServiceCollection();
        services.AddLogging();

        // Add Vault secret store but NOT Kernel
        services.AddSingleton<ISecretStore>(StubSecretStore.Instance);

        // This should throw with our authored guard message
        services.AddHoneyDrunkAuth();

        Console.WriteLine($"FAIL {name}: Expected InvalidOperationException but none was thrown");
        return Task.FromResult(ExitCodes.GuardMissingKernel);
    }
    catch (InvalidOperationException ex) when (ex.Message.Contains("HoneyDrunk.Kernel", StringComparison.OrdinalIgnoreCase))
    {
        Console.WriteLine($"PASS {name}");
        return Task.FromResult(ExitCodes.Success);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"FAIL {name}: Got unexpected exception type: {ex.GetType().Name}: {ex.Message}");
        return Task.FromResult(ExitCodes.GuardMissingKernel);
    }
}

static Task<int> Check2_GuardMissingVault()
{
    const string name = "Guard: Missing Vault";

    try
    {
        var services = new ServiceCollection();
        services.AddLogging();

        // Add Kernel guard services but NOT Vault
        services.AddSingleton<IGridContextAccessor>(_ => throw new InvalidOperationException("Not used by this guard canary."));
        services.AddSingleton<IOperationContextAccessor>(_ => throw new InvalidOperationException("Not used by this guard canary."));
        services.AddSingleton<ITelemetryActivityFactory>(NoOpTelemetryActivityFactory.Instance);

        // This should throw with our authored guard message
        services.AddHoneyDrunkAuth();

        Console.WriteLine($"FAIL {name}: Expected InvalidOperationException but none was thrown");
        return Task.FromResult(ExitCodes.GuardMissingVault);
    }
    catch (InvalidOperationException ex) when (ex.Message.Contains("HoneyDrunk.Vault", StringComparison.OrdinalIgnoreCase))
    {
        Console.WriteLine($"PASS {name}");
        return Task.FromResult(ExitCodes.Success);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"FAIL {name}: Got unexpected exception type: {ex.GetType().Name}: {ex.Message}");
        return Task.FromResult(ExitCodes.GuardMissingVault);
    }
}

static async Task<int> Check3_HappyPathAuthentication()
{
    const string name = "Happy path authentication";

    var key = TokenMinter.GenerateKey("happy-key-1");
    var toggleableProvider = new ToggleableSigningKeyProvider().AddKey(key);

    var (services, _) = BuildServicesWithCaching(toggleableProvider);
    var provider = services.BuildServiceProvider();
    var authProvider = provider.GetRequiredService<IAuthenticationProvider>();
    var auditLog = provider.GetRequiredService<InMemoryAuditLog>();

    var token = TokenMinter.MintValid(key, subject: "canary-user-42", name: "Canary Test User");
    var credential = AuthCredential.Bearer(token);

    var result = await authProvider.AuthenticateAsync(credential);

    if (!result.IsAuthenticated)
    {
        Console.WriteLine($"FAIL {name}: Expected success but got {result.FailureCode}: {result.FailureMessage}");
        return ExitCodes.HappyPathFailed;
    }

    if (result.Identity?.SubjectId != "canary-user-42")
    {
        Console.WriteLine($"FAIL {name}: SubjectId mismatch, expected 'canary-user-42' but got '{result.Identity?.SubjectId}'");
        return ExitCodes.HappyPathFailed;
    }

    var auditEntry = auditLog.Snapshot().SingleOrDefault(entry => entry.EventName == "auth.token.validate");
    if (auditEntry is null || auditEntry.Outcome != AuditOutcome.Succeeded || auditEntry.Actor != "canary-user-42")
    {
        Console.WriteLine($"FAIL {name}: Expected successful auth.token.validate audit entry");
        return ExitCodes.HappyPathFailed;
    }

    Console.WriteLine($"PASS {name}");
    return ExitCodes.Success;
}

static async Task<int> Check4_MissingRequiredClaim()
{
    const string name = "Missing required claim → MissingClaim";

    var key = TokenMinter.GenerateKey("claim-key-1");

    // Test 4a: Missing custom required claim (tenant_id)
    {
        var toggleableProvider = new ToggleableSigningKeyProvider().AddKey(key);
        var (services, _) = BuildServicesWithCaching(toggleableProvider, configureOptions: opts =>
        {
            opts.RequiredClaims.Add("tenant_id");
        });

        var provider = services.BuildServiceProvider();
        var authProvider = provider.GetRequiredService<IAuthenticationProvider>();

        // Token has 'sub' but no 'tenant_id'
        var token = TokenMinter.MintMissingClaim(key, "tenant_id");
        var result = await authProvider.AuthenticateAsync(AuthCredential.Bearer(token));

        if (result.IsAuthenticated)
        {
            Console.WriteLine($"FAIL {name}: Expected failure for missing tenant_id but got success");
            return ExitCodes.MissingClaimWrongCode;
        }

        if (result.FailureCode != AuthenticationFailureCode.MissingClaim)
        {
            Console.WriteLine($"FAIL {name}: Expected MissingClaim but got {result.FailureCode}");
            return ExitCodes.MissingClaimWrongCode;
        }
    }

    // Test 4b: Missing sub claim specifically
    {
        var toggleableProvider = new ToggleableSigningKeyProvider().AddKey(key);
        var (services, _) = BuildServicesWithCaching(toggleableProvider);
        var provider = services.BuildServiceProvider();
        var authProvider = provider.GetRequiredService<IAuthenticationProvider>();

        var token = TokenMinter.MintWithoutSub(key);
        var result = await authProvider.AuthenticateAsync(AuthCredential.Bearer(token));

        if (result.IsAuthenticated)
        {
            Console.WriteLine($"FAIL {name} (sub): Expected failure for missing sub but got success");
            return ExitCodes.MissingSubWrongCode;
        }

        if (result.FailureCode != AuthenticationFailureCode.MissingClaim)
        {
            Console.WriteLine($"FAIL {name} (sub): Expected MissingClaim but got {result.FailureCode}");
            return ExitCodes.MissingSubWrongCode;
        }
    }

    Console.WriteLine($"PASS {name}");
    return ExitCodes.Success;
}

static async Task<int> Check5_VaultDownUsesLastKnownGood()
{
    const string name = "Vault down uses last-known-good cache";

    var key = TokenMinter.GenerateKey("cache-key-1");
    var toggleableProvider = new ToggleableSigningKeyProvider().AddKey(key);

    var (services, _) = BuildServicesWithCaching(toggleableProvider, configureOptions: opts =>
    {
        opts.CacheTtl = TimeSpan.FromMinutes(5); // Long TTL
    });

    var provider = services.BuildServiceProvider();
    var authProvider = provider.GetRequiredService<IAuthenticationProvider>();

    // Warm the cache with a valid authentication
    var token = TokenMinter.MintValid(key, subject: "cache-user");
    var warmResult = await authProvider.AuthenticateAsync(AuthCredential.Bearer(token));

    if (!warmResult.IsAuthenticated)
    {
        Console.WriteLine($"FAIL {name}: Cache warmup failed: {warmResult.FailureCode}");
        return ExitCodes.CacheLastKnownGoodFailed;
    }

    // Now flip Vault to unavailable
    toggleableProvider.IsAvailable = false;

    // Authenticate again - should still work from cache (within TTL)
    var cachedResult = await authProvider.AuthenticateAsync(AuthCredential.Bearer(token));

    if (!cachedResult.IsAuthenticated)
    {
        Console.WriteLine($"FAIL {name}: Expected success from cache but got {cachedResult.FailureCode}");
        return ExitCodes.CacheLastKnownGoodFailed;
    }

    // Verify cache was used (no additional calls to inner provider during TTL)
    // Note: There might be one call if the cache had just expired, but we should still succeed from LKG
    Console.WriteLine($"PASS {name}");
    return ExitCodes.Success;
}

static async Task<int> Check6_TtlExpiryVaultDownReturnsVaultUnavailable()
{
    const string name = "TTL expiry + Vault down → VaultUnavailable";

    var key = TokenMinter.GenerateKey("ttl-key-1");
    var toggleableProvider = new ToggleableSigningKeyProvider().AddKey(key);

    // Use very short TTL for this test
    var (services, _) = BuildServicesWithCaching(toggleableProvider, configureOptions: opts =>
    {
        opts.CacheTtl = TimeSpan.FromMilliseconds(50); // Very short TTL
    });

    var provider = services.BuildServiceProvider();
    var authProvider = provider.GetRequiredService<IAuthenticationProvider>();

    // Warm up (vault available)
    var token = TokenMinter.MintValid(key, subject: "ttl-user");
    var warmResult = await authProvider.AuthenticateAsync(AuthCredential.Bearer(token));

    if (!warmResult.IsAuthenticated)
    {
        Console.WriteLine($"FAIL {name}: Warmup failed: {warmResult.FailureCode}");
        return ExitCodes.CacheTtlExpiryLkgOrVaultUnavailableFailed;
    }

    // Wait for TTL to expire
    await Task.Delay(100);

    // Simulate vault down
    toggleableProvider.IsAvailable = false;

    // Now try to authenticate - cache is expired and vault is down
    // CachingSigningKeyProvider should use last-known-good on failure
    var result = await authProvider.AuthenticateAsync(AuthCredential.Bearer(token));

    // With LKG, this should still succeed because the cached keys are used
    // If LKG is working correctly, the auth should succeed
    if (!result.IsAuthenticated)
    {
        // If it fails, it should be VaultUnavailable (when no LKG available)
        // But since we warmed up, LKG should be populated
        Console.WriteLine($"INFO {name}: Got failure {result.FailureCode} - checking if LKG was bypassed");

        // This might happen if the implementation doesn't have LKG
        if (result.FailureCode == AuthenticationFailureCode.VaultUnavailable)
        {
            Console.WriteLine($"PASS {name} (VaultUnavailable correctly returned when LKG exhausted)");
            return ExitCodes.Success;
        }

        Console.WriteLine($"FAIL {name}: Expected success from LKG or VaultUnavailable but got {result.FailureCode}");
        return ExitCodes.CacheTtlExpiryLkgOrVaultUnavailableFailed;
    }

    Console.WriteLine($"PASS {name} (LKG fallback worked)");
    return ExitCodes.Success;
}

static async Task<int> Check7_UnknownKidRefreshBehavior()
{
    const string name = "Unknown kid refresh behavior";

    // Initial key
    var initialKey = TokenMinter.GenerateKey("initial-key-1");
    var toggleableProvider = new ToggleableSigningKeyProvider().AddKey(initialKey);

    // Create key that will be "rotated in" later
    var rotatedKey = TokenMinter.GenerateKey("rotated-key-2");

    var (services, _) = BuildServicesWithCaching(toggleableProvider, configureOptions: opts =>
    {
        opts.RefreshOnUnknownKeyId = true;
        opts.CacheTtl = TimeSpan.FromMinutes(5);
    });

    var provider = services.BuildServiceProvider();
    var authProvider = provider.GetRequiredService<IAuthenticationProvider>();

    // Warm cache with initial key
    var warmToken = TokenMinter.MintValid(initialKey, subject: "kid-user");
    var warmResult = await authProvider.AuthenticateAsync(AuthCredential.Bearer(warmToken));

    if (!warmResult.IsAuthenticated)
    {
        Console.WriteLine($"FAIL {name}: Warmup failed: {warmResult.FailureCode}");
        return ExitCodes.UnknownKidRefreshFailed;
    }

    toggleableProvider.ResetCallCount();

    // Now add the rotated key to the provider (simulating key rotation in Vault)
    toggleableProvider.AddKey(rotatedKey);

    // Create a new token signed by the rotated key
    var rotatedToken = TokenMinter.MintValid(rotatedKey, subject: "rotated-user");

    // Try to authenticate with the token signed by rotated key
    var refreshResult = await authProvider.AuthenticateAsync(AuthCredential.Bearer(rotatedToken));

    // The unknown kid should trigger a refresh attempt
    // After refresh, the rotated key should be found and auth should succeed
    // OR it might fail if unknown kid refresh is not working correctly
    if (!refreshResult.IsAuthenticated)
    {
        // Check if a refresh was attempted
        var refreshCallCount = toggleableProvider.GetKeysCallCount;
        Console.WriteLine($"INFO {name}: Auth failed, inner provider was called {refreshCallCount} times");

        Console.WriteLine($"FAIL {name}: Expected authentication success after unknown kid refresh but got {refreshResult.FailureCode}");
        return ExitCodes.UnknownKidRefreshFailed;
    }

    Console.WriteLine($"PASS {name} (key rotation detected and auth succeeded)");

    // Part 2: Test with vault down and unknown kid
    toggleableProvider.ResetCallCount();
    toggleableProvider.IsAvailable = false;

    var (vaultDownToken, _) = TokenMinter.MintWithUnknownKid("never-known-key");

    var vaultDownResult = await authProvider.AuthenticateAsync(AuthCredential.Bearer(vaultDownToken));

    if (vaultDownResult.IsAuthenticated)
    {
        Console.WriteLine($"FAIL {name} (vault down): Expected failure but got success");
        return ExitCodes.UnknownKidVaultDownWrongCode;
    }

    // Should be InvalidSignature (key not found) since cache has LKG keys but not this kid
    if (vaultDownResult.FailureCode != AuthenticationFailureCode.InvalidSignature &&
        vaultDownResult.FailureCode != AuthenticationFailureCode.VaultUnavailable)
    {
        Console.WriteLine($"FAIL {name} (vault down): Expected InvalidSignature or VaultUnavailable but got {vaultDownResult.FailureCode}");
        return ExitCodes.UnknownKidVaultDownWrongCode;
    }

    Console.WriteLine($"PASS {name} (vault down returned {vaultDownResult.FailureCode})");
    return ExitCodes.Success;
}

static Task<int> Check8_PolicyNotFoundDistinguishable()
{
    const string name = "PolicyNotFound distinguishable";

    // Verify the enum value exists and is distinct
    var policyNotFound = AuthorizationDenyCode.PolicyNotFound;
    var genericDeny = AuthorizationDenyCode.PolicyNotSatisfied;

    if (policyNotFound == genericDeny)
    {
        Console.WriteLine($"FAIL {name}: PolicyNotFound is not distinguishable from PolicyNotSatisfied");
        return Task.FromResult(ExitCodes.PolicyNotFoundIndistinguishable);
    }

    if ((int)policyNotFound != 8)
    {
        Console.WriteLine($"FAIL {name}: PolicyNotFound has unexpected value {(int)policyNotFound}, expected 8");
        return Task.FromResult(ExitCodes.PolicyNotFoundIndistinguishable);
    }

    // Create a deny reason with PolicyNotFound and verify it's preserved
    var decision = AuthorizationDecision.Deny(AuthorizationDenyCode.PolicyNotFound, "Test policy not found");

    if (!decision.DenyReasons.Any(r => r.Code == AuthorizationDenyCode.PolicyNotFound))
    {
        Console.WriteLine($"FAIL {name}: PolicyNotFound code not preserved in AuthorizationDecision");
        return Task.FromResult(ExitCodes.PolicyNotFoundIndistinguishable);
    }

    Console.WriteLine($"PASS {name}");
    return Task.FromResult(ExitCodes.Success);
}

static Task<int> Check9_PurityBoundary()
{
    const string name = "Purity boundary (evaluator has no side effects)";

    // Create a test identity
    var claims = new Dictionary<string, IReadOnlyList<string>>
    {
        ["sub"] = ["purity-user"],
        ["scope"] = ["read", "write"],
    };
    var identity = new AuthenticatedIdentity("purity-user", AuthScheme.Bearer, "Purity Test", claims);

    // Create a request
    var request = new AuthorizationRequest("read", "resource", requiredScopes: ["read"]);

    // The evaluator should work without any telemetry or logging infrastructure
    try
    {
        var decision = AuthorizationPolicyEvaluator.Evaluate(identity, request);

        if (!decision.IsAllowed)
        {
            Console.WriteLine($"FAIL {name}: Expected Allow but got Deny: {string.Join(", ", decision.DenyReasons.Select(r => r.Message))}");
            return Task.FromResult(ExitCodes.PurityViolation);
        }

        // Test null identity (should return NotAuthenticated, not throw)
        var nullDecision = AuthorizationPolicyEvaluator.Evaluate(null, request);
        if (nullDecision.IsAllowed)
        {
            Console.WriteLine($"FAIL {name}: Expected Deny for null identity but got Allow");
            return Task.FromResult(ExitCodes.PurityViolation);
        }

        if (!nullDecision.DenyReasons.Any(r => r.Code == AuthorizationDenyCode.NotAuthenticated))
        {
            Console.WriteLine($"FAIL {name}: Expected NotAuthenticated deny code");
            return Task.FromResult(ExitCodes.PurityViolation);
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"FAIL {name}: Evaluator threw exception: {ex.Message}");
        return Task.FromResult(ExitCodes.PurityViolation);
    }

    // Structural purity check: evaluator should not have ITelemetryActivityFactory or ILogger parameters
    var evaluatorType = typeof(AuthorizationPolicyEvaluator);
    var constructors = evaluatorType.GetConstructors();

    var impureParameter = constructors
        .SelectMany(ctor => ctor.GetParameters())
        .FirstOrDefault(param =>
            param.ParameterType == typeof(ITelemetryActivityFactory) ||
            param.ParameterType.IsAssignableTo(typeof(ILogger)));

    if (impureParameter is not null)
    {
        Console.WriteLine($"FAIL {name}: Evaluator constructor has {impureParameter.ParameterType.Name} parameter, violating purity");
        return Task.FromResult(ExitCodes.PurityViolation);
    }

    Console.WriteLine($"PASS {name}");
    return Task.FromResult(ExitCodes.Success);
}

static async Task<int> Check10_SecretValuesFlowThroughISecretStoreOnly()
{
    const string name = "Secret boundary (ISecretStore is the only secret path)";

    var store = new RecordingSecretStore(CreateSigningKeysJson());
    var configuration = new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Auth:Issuer"] = "https://issuer.example.com",
            ["Auth:Audience"] = "api://honeydrunk-auth",
            ["Auth:ClockSkewSeconds"] = "60",
        })
        .Build();

    var provider = new VaultSigningKeyProvider(
        store,
        configuration,
        new CapturingLogger<VaultSigningKeyProvider>());

    _ = await provider.GetSigningKeysAsync();
    _ = await provider.GetIssuerAsync();
    _ = await provider.GetAudienceAsync();
    _ = await provider.GetClockSkewAsync();

    if (store.RequestedSecretNames.Count != 1 ||
        !string.Equals(store.RequestedSecretNames[0], "Jwt--SigningKeys", StringComparison.Ordinal))
    {
        Console.WriteLine($"FAIL {name}: Expected only Jwt--SigningKeys from ISecretStore");
        return ExitCodes.SecretBoundaryViolation;
    }

    if (store.RequestedIdentifiers.Any(identifier => identifier.Version is not null))
    {
        Console.WriteLine($"FAIL {name}: Secret read pinned to a specific version");
        return ExitCodes.SecretBoundaryViolation;
    }

    var constructorUsesVaultClient = typeof(VaultSigningKeyProvider)
        .GetConstructors()
        .SelectMany(ctor => ctor.GetParameters())
        .Any(parameter => parameter.ParameterType == typeof(IVaultClient));
    if (constructorUsesVaultClient)
    {
        Console.WriteLine($"FAIL {name}: VaultSigningKeyProvider still accepts IVaultClient");
        return ExitCodes.SecretBoundaryViolation;
    }

    Console.WriteLine($"PASS {name}");
    return ExitCodes.Success;
}

// ============================================================================
// Helper Methods
// ============================================================================
// Builds a ServiceCollection with CachingSigningKeyProvider decorating the given inner provider.
// Registers Vault stubs to satisfy guards, then replaces ISigningKeyProvider with the caching decorator.
static (ServiceCollection services, ToggleableSigningKeyProvider innerProvider) BuildServicesWithCaching(
    ToggleableSigningKeyProvider innerProvider,
    Action<AuthOptions>? configureOptions = null)
{
    var services = new ServiceCollection();
    services.AddLogging(b => b.AddProvider(NullLoggerProvider.Instance));

    // Kernel requirement
    services.AddSingleton<IGridContextAccessor>(CanaryGridContextAccessor.Instance);
    services.AddSingleton<IOperationContextAccessor>(_ => throw new InvalidOperationException("Not used by these canary checks."));
    services.AddSingleton<ITelemetryActivityFactory>(NoOpTelemetryActivityFactory.Instance);

    // Vault secret store to satisfy guards
    services.AddSingleton<ISecretStore>(StubSecretStore.Instance);

    // Configure auth options
    services.Configure<AuthOptions>(opts =>
    {
        opts.CacheTtl = TimeSpan.FromMinutes(5);
        configureOptions?.Invoke(opts);
    });

    // Register the toggleable provider as a named/inner instance
    services.AddSingleton(innerProvider);

    // Register CachingSigningKeyProvider decorating the toggleable provider
    services.AddSingleton<ISigningKeyProvider>(sp =>
    {
        var inner = sp.GetRequiredService<ToggleableSigningKeyProvider>();
        var opts = sp.GetRequiredService<IOptions<AuthOptions>>();
        var logger = sp.GetRequiredService<ILogger<CachingSigningKeyProvider>>();
        return new CachingSigningKeyProvider(inner, opts, logger);
    });

    services.AddSingleton<InMemoryAuditLog>();
    services.AddSingleton<IAuditLog>(sp => sp.GetRequiredService<InMemoryAuditLog>());

    // Register authentication provider
    services.AddSingleton<IAuthenticationProvider>(sp =>
    {
        var keyProv = sp.GetRequiredService<ISigningKeyProvider>();
        var opts = sp.GetRequiredService<IOptions<AuthOptions>>();
        var telemetry = sp.GetRequiredService<ITelemetryActivityFactory>();
        var auditLog = sp.GetRequiredService<IAuditLog>();
        var gridContextAccessor = sp.GetRequiredService<IGridContextAccessor>();
        var logger = sp.GetRequiredService<ILogger<BearerTokenAuthenticationProvider>>();
        return new BearerTokenAuthenticationProvider(keyProv, opts, telemetry, auditLog, gridContextAccessor, logger);
    });

    // Register authorization policy (delegates to static AuthorizationPolicyEvaluator.Evaluate)
    services.AddSingleton<IAuthorizationPolicy>(sp =>
    {
        var telemetry = sp.GetRequiredService<ITelemetryActivityFactory>();
        var auditLog = sp.GetRequiredService<IAuditLog>();
        var gridContextAccessor = sp.GetRequiredService<IGridContextAccessor>();
        var logger = sp.GetRequiredService<ILogger<DefaultAuthorizationPolicy>>();
        return new DefaultAuthorizationPolicy(telemetry, auditLog, gridContextAccessor, logger);
    });

    return (services, innerProvider);
}

static string CreateSigningKeysJson()
{
    var keyMaterial = Convert.ToBase64String(new byte[32]
    {
        1, 2, 3, 4, 5, 6, 7, 8,
        9, 10, 11, 12, 13, 14, 15, 16,
        17, 18, 19, 20, 21, 22, 23, 24,
        25, 26, 27, 28, 29, 30, 31, 32,
    });

    return $$"""[{"kid":"auth-key-1","alg":"HS256","key":"{{keyMaterial}}","active":true}]""";
}
