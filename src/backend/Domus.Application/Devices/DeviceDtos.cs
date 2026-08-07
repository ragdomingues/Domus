using Domus.Domain.Enums;

namespace Domus.Application.Devices;

public sealed record CreateDeviceRequest(
    Guid ResidenceId,
    DeviceType Type,
    string Name,
    DeviceConfigurationRequest? Configuration = null);

public sealed record UpdateDeviceRequest(string Name);

public sealed record DeviceConfigurationRequest(
    int RelayPulseMs = 500,
    int HeartbeatIntervalSeconds = 30,
    int CommandTimeoutSeconds = 30,
    int? OpenAlertMinutes = 15,
    bool SupportsClose = true,
    bool SupportsStop = false,
    string CapabilitiesJson = "{}",
    /// <summary>URL HTTP(S) do .bin OTA; enviada ao device via MQTT e não persistida.</summary>
    string? OtaUrl = null);

public sealed record DeviceConfigurationResponse(
    int RelayPulseMs,
    int HeartbeatIntervalSeconds,
    int CommandTimeoutSeconds,
    int? OpenAlertMinutes,
    bool SupportsClose,
    bool SupportsStop,
    string CapabilitiesJson,
    DateTimeOffset UpdatedAt);

public sealed record DeviceResponse(
    Guid Id,
    Guid TenantId,
    Guid ResidenceId,
    DeviceType Type,
    string Name,
    DeviceLifecycleStatus LifecycleStatus,
    DeviceConnectionStatus ConnectionStatus,
    string? FirmwareVersion,
    string? HardwareId,
    bool IsProvisioned,
    DateTimeOffset? LastSeenAt,
    DateTimeOffset CreatedAt,
    DeviceConfigurationResponse? Configuration,
    GateState? GateState,
    bool IsSimulated = false);

public sealed record DeviceActorContext(Guid UserId, string? IpAddress, string? UserAgent);
