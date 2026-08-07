using Domus.Domain.Common;
using Domus.Domain.Enums;

namespace Domus.Domain.Entities;

public class Gate : Entity
{
    public Guid DeviceId { get; private set; }
    public GateState GateState { get; private set; } = GateState.Unknown;
    public bool SupportsClose { get; private set; }
    public bool SupportsStop { get; private set; }
    public DateTimeOffset? OpenedAt { get; private set; }

    public Device? Device { get; private set; }

    private Gate()
    {
    }

    public static Gate Create(Guid deviceId, bool supportsClose = true, bool supportsStop = false)
    {
        return new Gate
        {
            DeviceId = deviceId,
            SupportsClose = supportsClose,
            SupportsStop = supportsStop,
            GateState = GateState.Unknown
        };
    }

    public void UpdateState(GateState state, DateTimeOffset? at = null)
    {
        var now = at ?? DateTimeOffset.UtcNow;

        if (state == GateState.Open && GateState != GateState.Open)
        {
            OpenedAt = now;
        }
        else if (state == GateState.Closed)
        {
            OpenedAt = null;
        }

        GateState = state;
    }
}
