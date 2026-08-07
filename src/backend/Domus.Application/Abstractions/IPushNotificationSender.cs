namespace Domus.Application.Abstractions;

public sealed record PushNotificationMessage(
    string Token,
    string Title,
    string Body,
    IReadOnlyDictionary<string, string>? Data = null,
    string Sound = "default",
    int? Badge = null,
    string Priority = "high",
    string ChannelId = "domus-alerts");

public sealed record PushSendResult(
    string Token,
    bool Succeeded,
    string? TicketId = null,
    string? ErrorCode = null,
    string? ErrorMessage = null,
    bool ShouldRemoveToken = false);

public interface IPushNotificationSender
{
    Task<IReadOnlyList<PushSendResult>> SendAsync(
        IReadOnlyList<PushNotificationMessage> messages,
        CancellationToken cancellationToken = default);
}

public sealed class NullPushNotificationSender : IPushNotificationSender
{
    public static NullPushNotificationSender Instance { get; } = new();

    public Task<IReadOnlyList<PushSendResult>> SendAsync(
        IReadOnlyList<PushNotificationMessage> messages,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<PushSendResult>>(
            messages.Select(m => new PushSendResult(m.Token, true, "null")).ToList());
}
