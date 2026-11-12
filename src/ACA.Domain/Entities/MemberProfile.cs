using ACA.Domain.Enums;

namespace ACA.Domain.Entities;

public class MemberProfile
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string? Bio { get; set; }
    public string? ProfilePhotoUrl { get; set; }
    public Guid? BatchId { get; set; }
    public Batch? Batch { get; set; }
    public MemberType MemberType { get; set; } = MemberType.CurrentStudent;
    public string? CurrentStatus { get; set; }
    public bool IsApproved { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}
