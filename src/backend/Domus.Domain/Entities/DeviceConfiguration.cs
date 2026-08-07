using Domus.Domain.Common;

namespace Domus.Domain.Entities;

public class DeviceConfiguration : Entity
{
    public Guid DeviceId { get; private set; }
    public int RelayPulseMs { get; private set; } = 500;
    public int HeartbeatIntervalSeconds { get; private set; } = 30;
    public int CommandTimeoutSeconds { get; private set; } = 30;
    public int? OpenAlertMinutes { get; private set; } = 15;
    public bool SupportsClose { get; private set; } = true;
    public bool SupportsStop { get; private set; }
    public string CapabilitiesJson { get; private set; } = "{}";
    public DateTimeOffset UpdatedAt { get; private set; } = DateTimeOffset.UtcNow;
    public Guid? UpdatedByUserId { get; private set; }

    public Device? Device { get; private set; }

    private DeviceConfiguration()
    {
    }

    public static DeviceConfiguration CreateDefault(Guid deviceId)
    {
        return new DeviceConfiguration
        {
            DeviceId = deviceId
        };
    }

    public void Update(
        int relayPulseMs,
        int heartbeatIntervalSeconds,
        int commandTimeoutSeconds,
        int? openAlertMinutes,
        bool supportsClose,
        bool supportsStop,
        string capabilitiesJson,
        Guid? updatedByUserId)
    {
        RelayPulseMs = relayPulseMs;
        HeartbeatIntervalSeconds = heartbeatIntervalSeconds;
        CommandTimeoutSeconds = commandTimeoutSeconds;
        OpenAlertMinutes = openAlertMinutes;
        SupportsClose = supportsClose;
        SupportsStop = supportsStop;
        CapabilitiesJson = capabilitiesJson;
        UpdatedByUserId = updatedByUserId;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}
