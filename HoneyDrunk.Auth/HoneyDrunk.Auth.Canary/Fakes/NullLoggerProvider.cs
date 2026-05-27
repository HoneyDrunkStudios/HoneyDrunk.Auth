using Microsoft.Extensions.Logging;

namespace HoneyDrunk.Auth.Canary.Fakes;

/// <summary>
/// No-op logger provider for basic DI registration.
/// </summary>
internal sealed class NullLoggerProvider : ILoggerProvider
{
    public static NullLoggerProvider Instance { get; } = new();

    public ILogger CreateLogger(string categoryName) => InnerNullLogger.Instance;

    public void Dispose()
    {
    }

    // Renamed from NullLogger to avoid shadowing the outer NullLoggerProvider class
    // through the inner Instance property (Sonar S3604).
    private sealed class InnerNullLogger : ILogger
    {
        public static InnerNullLogger Instance { get; } = new();

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => false;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
        }
    }
}
