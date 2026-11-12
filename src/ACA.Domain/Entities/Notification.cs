using ACA.Domain.Enums;

namespace ACA.Domain.Entities;

public class Notification
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid RecipientId { get; set; }
    public Guid? ActorId { get; set; }
    public NotificationType Type { get; set; }
    public Guid? PostId { get; set; }
    public Guid? CommentId { get; set; }
    public bool IsRead { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
