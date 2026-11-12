using ACA.Domain.Enums;

namespace ACA.Application.DTOs;

public class MemberSummaryDto
{
    public Guid UserId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string? ProfilePhotoUrl { get; set; }
    public string? BatchName { get; set; }
    public MemberType MemberType { get; set; }
    public string? CurrentStatus { get; set; }
}

public class MemberDetailDto : MemberSummaryDto
{
    public Guid ProfileId { get; set; }
    public string? Bio { get; set; }
    public Guid? BatchId { get; set; }
    public string? Session { get; set; }
    public bool IsApproved { get; set; }
    public DateTime JoinedAtUtc { get; set; }
}

public class UpdateProfileDto
{
    public string FullName { get; set; } = string.Empty;
    public string? Bio { get; set; }
    public Guid? BatchId { get; set; }
    public MemberType MemberType { get; set; }
    public string? CurrentStatus { get; set; }
    public ImageUpload? ProfilePhoto { get; set; }
}

public class RegisterDto
{
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public Guid? BatchId { get; set; }
    public MemberType MemberType { get; set; } = MemberType.CurrentStudent;
    public string? CurrentStatus { get; set; }
}
