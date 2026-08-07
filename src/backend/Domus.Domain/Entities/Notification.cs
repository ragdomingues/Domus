using Domus.Domain.Common;

namespace Domus.Domain.Entities;

public class Notification : Entity
{
    public Guid TenantId { get; private set; }
    public Guid UserId { get; private set; }
    public string Type { get; private set; } = string.Empty;
    public string Title { get; private set; } = string.Empty;
    public string Body { get; private set; } = string.Empty;
    public string? PayloadJson { get; private set; }
    public DateTimeOffset? ReadAt { get; private set; }

    public ICollection<NotificationDelivery> Deliveries { get; private set; } = new List<NotificationDelivery>();

    private Notification()
    {
    }

    public static Notification Create(
        Guid tenantId,
        Guid userId,
        string type,
        string title,
        string body,
        string? payloadJson = null)
    {
        return new Notification
        {
            TenantId = tenantId,
            UserId = userId,
            Type = type,
            Title = title,
            Body = body,
            PayloadJson = payloadJson
        };
    }

    public void MarkRead(DateTimeOffset? at = null) => ReadAt = at ?? DateTimeOffset.UtcNow;
}

public class NotificationDelivery : Entity
{
    public Guid NotificationId { get; private set; }
    public string Channel { get; private set; } = "Push";
    public string Status { get; private set; } = "Pending";
    public DateTimeOffset? SentAt { get; private set; }
    public string? Error { get; private set; }

    public Notification? Notification { get; private set; }

    private NotificationDelivery()
    {
    }

    public static NotificationDelivery Create(Guid notificationId, string channel = "Push")
    {
        return new NotificationDelivery
        {
            NotificationId = notificationId,
            Channel = channel,
            Status = "Pending"
        };
    }

    public void MarkSent(DateTimeOffset? at = null)
    {
        Status = "Sent";
        SentAt = at ?? DateTimeOffset.UtcNow;
        Error = null;
    }

    public void MarkFailed(string error, DateTimeOffset? at = null)
    {
        Status = "Failed";
        SentAt = at ?? DateTimeOffset.UtcNow;
        Error = error.Length > 1000 ? error[..1000] : error;
    }
}
