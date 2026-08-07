using Domus.Domain.Common;
using Domus.Domain.Enums;

namespace Domus.Domain.Entities;

public class SecurityAuditLog : Entity
{
    public Guid? TenantId { get; private set; }
    public Guid? UserId { get; private set; }
    public SecurityAuditAction Action { get; private set; }
    public string? IpAddress { get; private set; }
    public string? UserAgent { get; private set; }
    public string? Details { get; private set; }
    public bool Succeeded { get; private set; }

    private SecurityAuditLog()
    {
    }

    public static SecurityAuditLog Create(
        SecurityAuditAction action,
        bool succeeded,
        Guid? userId = null,
        Guid? tenantId = null,
        string? ipAddress = null,
        string? userAgent = null,
        string? details = null)
    {
        return new SecurityAuditLog
        {
            Action = action,
            Succeeded = succeeded,
            UserId = userId,
            TenantId = tenantId,
            IpAddress = ipAddress,
            UserAgent = userAgent,
            Details = details
        };
    }
}
