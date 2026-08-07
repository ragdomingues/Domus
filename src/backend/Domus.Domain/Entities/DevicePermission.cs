using Domus.Domain.Common;

namespace Domus.Domain.Entities;

public class DevicePermission : Entity
{
    public Guid UserId { get; private set; }
    public Guid DeviceId { get; private set; }
    public bool CanView { get; private set; } = true;
    public bool CanOpen { get; private set; }
    public bool CanClose { get; private set; }
    public bool CanStop { get; private set; }

    public User? User { get; private set; }
    public Device? Device { get; private set; }

    private DevicePermission()
    {
    }

    public static DevicePermission Create(
        Guid userId,
        Guid deviceId,
        bool canView = true,
        bool canOpen = false,
        bool canClose = false,
        bool canStop = false)
    {
        return new DevicePermission
        {
            UserId = userId,
            DeviceId = deviceId,
            CanView = canView,
            CanOpen = canOpen,
            CanClose = canClose,
            CanStop = canStop
        };
    }
}
