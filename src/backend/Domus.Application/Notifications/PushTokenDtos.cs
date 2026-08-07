namespace Domus.Application.Notifications;

public sealed record RegisterPushTokenRequest(
    string Token,
    string Platform,
    string? DeviceName = null);

public sealed record PushTokenResponse(
    Guid Id,
    string Token,
    string Platform,
    string? DeviceName,
    DateTimeOffset UpdatedAt);
