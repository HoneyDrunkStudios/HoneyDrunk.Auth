using HoneyDrunk.Audit.Abstractions;

namespace HoneyDrunk.Auth.Canary.Fakes;

/// <summary>
/// In-memory audit log for canary emission checks.
/// </summary>
public sealed class InMemoryAuditLog : IAuditLog
{
    private readonly List<AuditEntry> _entries = [];
    private readonly Lock _lock = new();

    /// <inheritdoc />
    public Task AppendAsync(AuditEntry entry, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            _entries.Add(entry);
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Returns a stable copy of appended audit entries.
    /// </summary>
    /// <returns>The appended entries.</returns>
    public IReadOnlyList<AuditEntry> Snapshot()
    {
        lock (_lock)
        {
            return _entries.ToList().AsReadOnly();
        }
    }
}
