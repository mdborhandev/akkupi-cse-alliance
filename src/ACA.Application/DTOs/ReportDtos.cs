using ACA.Domain.Enums;

namespace ACA.Application.DTOs;

public class CreateReportDto
{
    public Guid? PostId { get; set; }
    public Guid? CommentId { get; set; }
    public Guid? TargetUserId { get; set; }
    public string Reason { get; set; } = string.Empty;
}

public class ReportDto
{
    public Guid Id { get; set; }
    public Guid ReporterId { get; set; }
    public string ReporterName { get; set; } = string.Empty;
    public Guid? PostId { get; set; }
    public Guid? CommentId { get; set; }
    public Guid? TargetUserId { get; set; }
    public string Reason { get; set; } = string.Empty;
    public ReportStatus Status { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}
