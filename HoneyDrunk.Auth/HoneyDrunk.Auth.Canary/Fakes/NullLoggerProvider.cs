using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace HoneyDrunk.Auth.Canary.Fakes;

/// <summary>
/// No-op logger provider for basic DI registration. Delegates to
/// <see cref="NullLogger.Instance"/> from Microsoft.Extensions.Logging.Abstractions
/// — no need to roll our own inner ILogger (previous attempts ran into
/// Sonar S3604 on shadowed Instance properties and S109 on the
/// renamed-Singleton accessor).
/// </summary>
internal sealed class NullLoggerProvider : ILoggerProvider
{
    public static NullLoggerProvider Instance { get; } = new();

    public ILogger CreateLogger(string categoryName) => NullLogger.Instance;

    public void Dispose()
    {
    }
}
