using Domus.Domain.Common;
using Domus.Domain.Enums;

namespace Domus.Domain.Entities;

public class DeviceEvent : Entity
{
    public Guid TenantId { get; private set; }
    public Guid DeviceId { get; private set; }
    public Guid? UserId { get; private set; }
    public Guid? CommandId { get; private set; }
    public string Action { get; private set; } = string.Empty;
    public EventResult Result { get; private set; }
    public EventOrigin Origin { get; private set; }
    public string? IpAddress { get; private set; }
    public string? UserAgent { get; private set; }
    public string? Details { get; private set; }

    public Device? Device { get; private set; }
    public User? User { get; private set; }

    private DeviceEvent()
    {
    }

    public static DeviceEvent Create(
        Guid tenantId,
        Guid deviceId,
        string action,
        EventResult result,
        EventOrigin origin,
        Guid? userId = null,
        Guid? commandId = null,
        string? ipAddress = null,
        string? userAgent = null,
        string? details = null)
    {
        return new DeviceEvent
        {
            TenantId = tenantId,
            DeviceId = deviceId,
            UserId = userId,
            CommandId = commandId,
            Action = action,
            Result = result,
            Origin = origin,
            IpAddress = ipAddress,
            UserAgent = userAgent,
            Details = details
        };
    }
}
