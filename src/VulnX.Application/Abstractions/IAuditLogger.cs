namespace VulnX.Application.Abstractions;

public interface IAuditLogger
{
    /// metadata must never contain secrets, tokens, passwords or full request bodies.
    Task LogAsync(Guid? userId, string action, string entityType, string? entityId = null,
        object? metadata = null, CancellationToken ct = default);
}
