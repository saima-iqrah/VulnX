using System.Text.Json;
using VulnX.Application.Abstractions;
using VulnX.Domain.Entities;
using VulnX.Infrastructure.Persistence;

namespace VulnX.Infrastructure.Services;

internal sealed class AuditLogger(AppDbContext db) : IAuditLogger
{
    public async Task LogAsync(Guid? userId, string action, string entityType, string? entityId = null,
        object? metadata = null, CancellationToken ct = default)
    {
        db.AuditLogs.Add(new AuditLog
        {
            UserId = userId,
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            SafeMetadataJson = metadata is null ? null : JsonSerializer.Serialize(metadata)
        });
        await db.SaveChangesAsync(ct);
    }
}
