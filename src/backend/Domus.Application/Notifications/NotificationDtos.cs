namespace Domus.Application.Notifications;

public sealed record DeviceNotificationPreferenceResponse(
    Guid DeviceId,
    bool NotifyOnOpen,
    bool NotifyOnClose,
    bool NotifyWhenOpenTooLong,
    int OpenAlertMinutes,
    DateTimeOffset? UpdatedAt);

public sealed record UpdateDeviceNotificationPreferenceRequest(
    bool NotifyOnOpen,
    bool NotifyOnClose,
    bool NotifyWhenOpenTooLong,
    int OpenAlertMinutes = 15);

public sealed record NotificationResponse(
    Guid Id,
    string Type,
    string Title,
    string Body,
    string? PayloadJson,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ReadAt);
