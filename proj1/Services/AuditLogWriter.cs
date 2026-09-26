using System.Text.Json;
using proj1.Data;
using proj1.Models;

namespace proj1.Services;

public static class AuditLogWriter
{
    public static void Add(ApplicationDbContext db, Guid? userId, string action, string entityType, Guid? entityId, object? oldValue = null, object? newValue = null)
    {
        db.AuditLogs.Add(new AuditLog
        {
            UserId = userId,
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            OldValue = oldValue is null ? null : JsonSerializer.Serialize(oldValue),
            NewValue = newValue is null ? null : JsonSerializer.Serialize(newValue),
            CreatedAt = DateTime.UtcNow
        });
    }
}
