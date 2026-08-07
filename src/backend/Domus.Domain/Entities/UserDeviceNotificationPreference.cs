using Domus.Domain.Common;

namespace Domus.Domain.Entities;

public class UserDeviceNotificationPreference : Entity
{
    public Guid UserId { get; private set; }
    public Guid DeviceId { get; private set; }
    public Guid TenantId { get; private set; }
    public bool NotifyOnOpen { get; private set; }
    public bool NotifyOnClose { get; private set; }
    public bool NotifyWhenOpenTooLong { get; private set; }
    public int OpenAlertMinutes { get; private set; } = 15;
    public DateTimeOffset? LastOpenAlertAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; } = DateTimeOffset.UtcNow;

    public User? User { get; private set; }
    public Device? Device { get; private set; }

    private UserDeviceNotificationPreference()
    {
    }

    public static UserDeviceNotificationPreference CreateDefault(
        Guid userId,
        Guid deviceId,
        Guid tenantId)
    {
        return new UserDeviceNotificationPreference
        {
            UserId = userId,
            DeviceId = deviceId,
            TenantId = tenantId
        };
    }

    public void Update(
        bool notifyOnOpen,
        bool notifyOnClose,
        bool notifyWhenOpenTooLong,
        int openAlertMinutes)
    {
        NotifyOnOpen = notifyOnOpen;
        NotifyOnClose = notifyOnClose;
        NotifyWhenOpenTooLong = notifyWhenOpenTooLong;
        OpenAlertMinutes = Math.Clamp(openAlertMinutes, 1, 24 * 60);
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void MarkOpenAlertSent(DateTimeOffset at) => LastOpenAlertAt = at;

    public void ClearOpenAlert() => LastOpenAlertAt = null;
}
