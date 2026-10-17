using Microsoft.Extensions.Logging;

namespace HoneyDrunk.Auth.Canary.Fakes;

/// <summary>
/// Logger that captures all log events for inspection.
/// </summary>
internal sealed class CapturingLogger<T> : ILogger<T>
{
    private readonly List<LogEntry> _entries = [];

    public IReadOnlyList<LogEntry> Entries => _entries;

    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        _entries.Add(new LogEntry(logLevel, formatter(state, exception), exception));
    }

    public void Clear() => _entries.Clear();

    public sealed record LogEntry(LogLevel Level, string Message, Exception? Exception);
}
