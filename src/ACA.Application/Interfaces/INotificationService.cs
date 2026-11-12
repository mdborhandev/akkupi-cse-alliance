using ACA.Application.DTOs;
using ACA.Application.DTOs.Common;
using ACA.Domain.Enums;

namespace ACA.Application.Interfaces;

public interface INotificationService
{
    Task NotifyAsync(Guid recipientId, Guid actorId, NotificationType type, Guid? postId, Guid? commentId, CancellationToken ct = default);
    Task<PagedResult<NotificationDto>> GetForUserAsync(Guid userId, int page, int pageSize, CancellationToken ct = default);
    Task MarkReadAsync(Guid notificationId, Guid userId, CancellationToken ct = default);
    Task MarkAllReadAsync(Guid userId, CancellationToken ct = default);
    Task<int> GetUnreadCountAsync(Guid userId, CancellationToken ct = default);
}
