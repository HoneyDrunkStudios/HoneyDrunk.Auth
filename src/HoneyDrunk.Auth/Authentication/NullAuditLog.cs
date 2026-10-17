using HoneyDrunk.Audit.Abstractions;

namespace HoneyDrunk.Auth.Authentication;

/// <summary>
/// No-op audit sink used when a host has not composed a durable Audit backing.
/// </summary>
internal sealed class NullAuditLog : IAuditLog
{
    /// <inheritdoc />
    public Task AppendAsync(AuditEntry entry, CancellationToken cancellationToken = default) => Task.CompletedTask;
}
