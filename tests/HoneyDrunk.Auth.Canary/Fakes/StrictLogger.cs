using Microsoft.Extensions.Logging;

namespace HoneyDrunk.Auth.Canary.Fakes;

/// <summary>
/// Strict logger that throws if any logging is performed.
/// Used to verify the pure evaluator does not invoke logging.
/// </summary>
internal sealed class StrictLogger<T> : ILogger<T>
{
    public static StrictLogger<T> Instance { get; } = new();

    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull
    {
        throw new InvalidOperationException("PURITY VIOLATION: Logging scope started in a context where side effects are forbidden.");
    }

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        throw new InvalidOperationException($"PURITY VIOLATION: Log [{logLevel}] invoked in a context where side effects are forbidden: {formatter(state, exception)}");
    }
}
