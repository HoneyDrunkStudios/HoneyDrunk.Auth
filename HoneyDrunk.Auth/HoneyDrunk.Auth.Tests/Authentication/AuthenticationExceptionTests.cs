using HoneyDrunk.Auth.Abstractions;
using HoneyDrunk.Auth.Authentication;

namespace HoneyDrunk.Auth.Tests.Authentication;

/// <summary>
/// Tests for <see cref="AuthenticationException"/>.
/// </summary>
public sealed class AuthenticationExceptionTests
{
    /// <summary>
    /// Constructor captures the failure code and message.
    /// </summary>
    [Fact]
    public void Constructor_StoresFailureCodeAndMessage()
    {
        var exception = new AuthenticationException(AuthenticationFailureCode.VaultUnavailable, "Vault is down");

        Assert.Equal(AuthenticationFailureCode.VaultUnavailable, exception.FailureCode);
        Assert.Equal("Vault is down", exception.Message);
    }

    /// <summary>
    /// FailureCode is preserved across throw + catch.
    /// </summary>
    [Fact]
    public void Throw_PreservesFailureCode()
    {
        static void Thrower() =>
            throw new AuthenticationException(AuthenticationFailureCode.ConfigurationError, "missing key");

        var caught = Assert.Throws<AuthenticationException>(Thrower);

        Assert.Equal(AuthenticationFailureCode.ConfigurationError, caught.FailureCode);
    }
}
