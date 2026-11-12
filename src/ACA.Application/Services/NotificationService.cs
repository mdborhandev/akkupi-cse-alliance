using ACA.Application.DTOs;
using ACA.Application.DTOs.Common;
using ACA.Application.Interfaces;
using ACA.Domain.Entities;
using ACA.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace ACA.Application.Services;

public class NotificationService : INotificationService
{
    private const int MaxPageSize = 100;

    private readonly IAppDbContext _db;

    public NotificationService(IAppDbContext db)
    {
        _db = db;
    }

    public async Task NotifyAsync(Guid recipientId, Guid actorId, NotificationType type, Guid? postId, Guid? commentId, CancellationToken ct = default)
    {
        // Never notify a member about their own activity.
        if (recipientId == Guid.Empty || actorId == Guid.Empty || recipientId == actorId)
        {
            return;
        }

        _db.Notifications.Add(new Notification
        {
            RecipientId = recipientId,
            ActorId = actorId,
            Type = type,
            PostId = postId,
            CommentId = commentId,
            IsRead = false,
            CreatedAtUtc = DateTime.UtcNow
        });

        await _db.SaveChangesAsync(ct);
    }

    public async Task<PagedResult<NotificationDto>> GetForUserAsync(Guid userId, int page, int pageSize, CancellationToken ct = default)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 20;
        if (pageSize > MaxPageSize) pageSize = MaxPageSize;

        var query = _db.Notifications.AsNoTracking().Where(n => n.RecipientId == userId);
        var total = await query.CountAsync(ct);

        var rows = await query
            .OrderByDescending(n => n.CreatedAtUtc)
            .ThenByDescending(n => n.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        var actorIds = rows
            .Where(n => n.ActorId.HasValue)
            .Select(n => n.ActorId!.Value)
            .Distinct()
            .ToList();

        var profiles = actorIds.Count == 0
            ? new Dictionary<Guid, MemberProfile>()
            : await _db.MemberProfiles.AsNoTracking()
                .Where(p => actorIds.Contains(p.UserId))
                .ToDictionaryAsync(p => p.UserId, ct);

        var items = rows.Select(n => Map(n, profiles)).ToList();
        return PagedResult<NotificationDto>.Create(items, page, pageSize, total);
    }

    public async Task MarkReadAsync(Guid notificationId, Guid userId, CancellationToken ct = default)
    {
        var notification = await _db.Notifications
            .FirstOrDefaultAsync(n => n.Id == notificationId && n.RecipientId == userId, ct);

        if (notification is null || notification.IsRead)
        {
            return;
        }

        notification.IsRead = true;
        await _db.SaveChangesAsync(ct);
    }

    public async Task MarkAllReadAsync(Guid userId, CancellationToken ct = default)
    {
        var unread = await _db.Notifications
            .Where(n => n.RecipientId == userId && !n.IsRead)
            .ToListAsync(ct);

        if (unread.Count == 0)
        {
            return;
        }

        foreach (var notification in unread)
        {
            notification.IsRead = true;
        }

        await _db.SaveChangesAsync(ct);
    }

    public Task<int> GetUnreadCountAsync(Guid userId, CancellationToken ct = default)
    {
        return _db.Notifications.CountAsync(n => n.RecipientId == userId && !n.IsRead, ct);
    }

    private static NotificationDto Map(Notification notification, IReadOnlyDictionary<Guid, MemberProfile> profiles)
    {
        MemberProfile? actor = null;
        if (notification.ActorId is Guid actorId)
        {
            profiles.TryGetValue(actorId, out actor);
        }

        var actorName = string.IsNullOrWhiteSpace(actor?.FullName)
            ? (notification.ActorId.HasValue ? "A member" : "ACA")
            : actor!.FullName;

        return new NotificationDto
        {
            Id = notification.Id,
            Type = notification.Type,
            ActorName = actor?.FullName,
            ActorPhotoUrl = actor?.ProfilePhotoUrl,
            PostId = notification.PostId,
            CommentId = notification.CommentId,
            Message = BuildMessage(notification.Type, actorName),
            IsRead = notification.IsRead,
            CreatedAtUtc = notification.CreatedAtUtc
        };
    }

    private static string BuildMessage(NotificationType type, string actorName) => type switch
    {
        NotificationType.Reaction => $"{actorName} reacted to your post.",
        NotificationType.Comment => $"{actorName} commented on your post.",
        NotificationType.Reply => $"{actorName} replied to your comment.",
        _ => "You have a new notification."
    };
}
