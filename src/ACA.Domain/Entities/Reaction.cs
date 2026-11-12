using ACA.Domain.Enums;

namespace ACA.Domain.Entities;

public class Reaction
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PostId { get; set; }
    public Post? Post { get; set; }
    public Guid UserId { get; set; }
    public ReactionType Type { get; set; } = ReactionType.Like;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
