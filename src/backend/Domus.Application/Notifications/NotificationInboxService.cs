using Domus.Application.Abstractions;
using Domus.Application.Common;
using Microsoft.EntityFrameworkCore;

namespace Domus.Application.Notifications;

public interface INotificationInboxService
{
    Task<Result<IReadOnlyList<NotificationResponse>>> ListAsync(
        Guid userId,
        int take = 50,
        CancellationToken cancellationToken = default);

    Task<Result> MarkReadAsync(
        Guid notificationId,
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<Result> MarkAllReadAsync(Guid userId, CancellationToken cancellationToken = default);
}

public sealed class NotificationInboxService : INotificationInboxService
{
    private readonly IDomusDbContext _db;

    public NotificationInboxService(IDomusDbContext db)
    {
        _db = db;
    }

    public async Task<Result<IReadOnlyList<NotificationResponse>>> ListAsync(
        Guid userId,
        int take = 50,
        CancellationToken cancellationToken = default)
    {
        take = Math.Clamp(take, 1, 100);
        var items = await _db.Notifications.AsNoTracking()
            .Where(n => n.UserId == userId)
            .OrderByDescending(n => n.CreatedAt)
            .Take(take)
            .Select(n => new NotificationResponse(
                n.Id,
                n.Type,
                n.Title,
                n.Body,
                n.PayloadJson,
                n.CreatedAt,
                n.ReadAt))
            .ToListAsync(cancellationToken);

        return Result<IReadOnlyList<NotificationResponse>>.Success(items);
    }

    public async Task<Result> MarkReadAsync(
        Guid notificationId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var notification = await _db.Notifications
            .FirstOrDefaultAsync(n => n.Id == notificationId && n.UserId == userId, cancellationToken);

        if (notification is null)
        {
            return Result.Failure("Notificação não encontrada.", "not_found");
        }

        if (notification.ReadAt is null)
        {
            notification.MarkRead();
            await _db.SaveChangesAsync(cancellationToken);
        }

        return Result.Success();
    }

    public async Task<Result> MarkAllReadAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var unread = await _db.Notifications
            .Where(n => n.UserId == userId && n.ReadAt == null)
            .ToListAsync(cancellationToken);

        foreach (var item in unread)
        {
            item.MarkRead();
        }

        if (unread.Count > 0)
        {
            await _db.SaveChangesAsync(cancellationToken);
        }

        return Result.Success();
    }
}
