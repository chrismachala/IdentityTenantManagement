namespace IdentityTenantManagementDatabase.Repositories;

using Microsoft.EntityFrameworkCore;
using DbContexts;
using Models;
using System.Text.Json;

public class AuditLogRepository : IAuditLogRepository
{
    protected readonly IdentityTenantManagementContext _context;

    public AuditLogRepository(IdentityTenantManagementContext context)
    {
        _context = context;
    }

    // Implement IRepository<AuditLog> members
    public async Task<AuditLog?> GetByIdAsync(Guid id)
    {
        return await _context.AuditLogs.FindAsync(id);
    }

    public async Task<IEnumerable<AuditLog>> GetAllAsync()
    {
        return await _context.AuditLogs.ToListAsync();
    }

    public async Task AddAsync(AuditLog entity)
    {
        await _context.AuditLogs.AddAsync(entity);
    }

    public async Task UpdateAsync(AuditLog entity)
    {
        _context.AuditLogs.Update(entity);
    }

    public async Task DeleteAsync(Guid id)
    {
        var auditLog = await GetByIdAsync(id);
        if (auditLog != null)
        {
            _context.AuditLogs.Remove(auditLog);
        }
    }

    public async Task<bool> ExistsAsync(Guid id)
    {
        return await _context.AuditLogs.AnyAsync(al => al.Id == id);
    }

    public async Task LogAsync(
        string action,
        string resourceType,
        string resourceId,
        Guid? actorUserId = null,
        string? actorDisplayName = null,
        Guid? tenantId = null,
        string? oldValues = null,
        string? newValues = null,
        string? ipAddress = null,
        string? userAgent = null,
        string? additionalContext = null)
    {
        var auditLog = new AuditLog
        {
            Id = Guid.NewGuid(),
            Timestamp = DateTime.UtcNow,
            ActorUserId = actorUserId,
            ActorDisplayName = actorDisplayName ?? "system",
            TenantId = tenantId,
            Action = action,
            ResourceType = resourceType,
            ResourceId = resourceId,
            OldValues = oldValues,
            NewValues = newValues,
            IpAddress = ipAddress,
            UserAgent = userAgent,
            AdditionalContext = additionalContext
        };

        await AddAsync(auditLog);
    }

    public async Task AnonymizeLogsForUserAsync(Guid userId)
    {
        var logs = await _context.AuditLogs
            .Where(log => log.ActorUserId == userId)
            .ToListAsync();

        foreach (var log in logs)
        {
            log.ActorDisplayName = "deleted user";

            if (log.OldValues != null)
                log.OldValues = RedactPII(log.OldValues);

            if (log.NewValues != null)
                log.NewValues = RedactPII(log.NewValues);
        }

        await _context.SaveChangesAsync();
    }

    private string RedactPII(string jsonString)
    {
        if (string.IsNullOrEmpty(jsonString))
            return jsonString;

        try
        {
            // Parse JSON and redact PII fields
            var jsonDoc = JsonDocument.Parse(jsonString);
            var root = jsonDoc.RootElement;

            var redactedObj = new Dictionary<string, object>();

            foreach (var property in root.EnumerateObject())
            {
                var propertyName = property.Name.ToLowerInvariant();

                // Redact PII fields
                if (propertyName == "firstname" ||
                    propertyName == "lastname" ||
                    propertyName == "email" ||
                    propertyName == "phone" ||
                    propertyName == "phonenumber")
                {
                    redactedObj[property.Name] = "[REDACTED]";
                }
                else
                {
                    redactedObj[property.Name] = property.Value.ToString();
                }
            }

            return JsonSerializer.Serialize(redactedObj);
        }
        catch
        {
            // If JSON parsing fails, return original string
            // In production, log the error
            return jsonString;
        }
    }
}
