using Microsoft.Extensions.Logging;

namespace HoneyDrunk.Auth.Canary.Fakes;

/// <summary>
/// No-op logger provider for basic DI registration.
/// </summary>
internal sealed class NullLoggerProvider : ILoggerProvider
{
    public static NullLoggerProvider Instance { get; } = new();

    public ILogger CreateLogger(string categoryName) => InnerNullLogger.Singleton;

    public void Dispose()
    {
    }

    // Singleton (not "Instance") so the inner property doesn't shadow the
    // outer NullLoggerProvider.Instance (Sonar S3604).
    private sealed class InnerNullLogger : ILogger
    {
        public static InnerNullLogger Singleton { get; } = new();

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => false;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
        }
    }
}
