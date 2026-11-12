using ACA.Domain.Enums;

namespace ACA.Application.DTOs;

public class NotificationDto
{
    public Guid Id { get; set; }
    public NotificationType Type { get; set; }
    public string? ActorName { get; set; }
    public string? ActorPhotoUrl { get; set; }
    public Guid? PostId { get; set; }
    public Guid? CommentId { get; set; }
    public string Message { get; set; } = string.Empty;
    public bool IsRead { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}
