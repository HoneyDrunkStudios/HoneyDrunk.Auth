using Microsoft.Extensions.Logging;

namespace HoneyDrunk.Auth.Canary.Fakes;

/// <summary>
/// Logger factory that returns strict loggers.
/// </summary>
internal sealed class StrictLoggerFactory : ILoggerFactory
{
    public static StrictLoggerFactory Instance { get; } = new();

    public void AddProvider(ILoggerProvider provider)
    {
    }

    public ILogger CreateLogger(string categoryName)
    {
        // Return a generic StrictLogger
        return new StrictLoggerInstance(categoryName);
    }

    public void Dispose()
    {
    }

    private sealed class StrictLoggerInstance(string categoryName) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull
        {
            throw new InvalidOperationException($"PURITY VIOLATION: Logging scope started for {categoryName} in a context where side effects are forbidden.");
        }

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            throw new InvalidOperationException($"PURITY VIOLATION: Log [{logLevel}] invoked for {categoryName} in a context where side effects are forbidden: {formatter(state, exception)}");
        }
    }
}
