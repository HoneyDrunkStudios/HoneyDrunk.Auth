using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace HoneyDrunk.Auth.Canary.Fakes;

/// <summary>
/// No-op logger provider for basic DI registration. Delegates to
/// <see cref="NullLogger.Instance"/> from Microsoft.Extensions.Logging.Abstractions
/// so we don't need a bespoke inner ILogger implementation here.
/// </summary>
internal sealed class NullLoggerProvider : ILoggerProvider
{
    public static NullLoggerProvider Instance { get; } = new();

    public ILogger CreateLogger(string categoryName) => NullLogger.Instance;

    public void Dispose()
    {
    }
}
