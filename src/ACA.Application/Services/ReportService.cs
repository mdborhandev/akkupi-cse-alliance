using ACA.Application.DTOs;
using ACA.Application.DTOs.Common;
using ACA.Application.Interfaces;
using ACA.Domain.Entities;
using ACA.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace ACA.Application.Services;

/// <summary>Member reporting and moderator resolution of reports.</summary>
public class ReportService : IReportService
{
    private const int DefaultPageSize = 20;
    private const int MaxPageSize = 100;
    private const int MinReasonLength = 3;
    private const int MaxReasonLength = 1000;

    private readonly IAppDbContext _db;

    public ReportService(IAppDbContext db)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
    }

    public async Task<Guid> CreateAsync(CreateReportDto dto, Guid reporterId, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var targetCount = 0;
        if (dto.PostId.HasValue) targetCount++;
        if (dto.CommentId.HasValue) targetCount++;
        if (dto.TargetUserId.HasValue) targetCount++;

        if (targetCount != 1)
        {
            throw new ArgumentException(
                "A report must target exactly one post, comment, or member.", nameof(dto));
        }

        var reason = (dto.Reason ?? string.Empty).Trim();
        if (reason.Length < MinReasonLength || reason.Length > MaxReasonLength)
        {
            throw new ArgumentException(
                $"Report reason must be between {MinReasonLength} and {MaxReasonLength} characters.", nameof(dto));
        }

        var report = new Report
        {
            ReporterId = reporterId,
            PostId = dto.PostId,
            CommentId = dto.CommentId,
            TargetUserId = dto.TargetUserId,
            Reason = reason,
            Status = ReportStatus.Pending,
            CreatedAtUtc = DateTime.UtcNow
        };

        _db.Reports.Add(report);
        await _db.SaveChangesAsync(ct);
        return report.Id;
    }

    public async Task<PagedResult<ReportDto>> GetPendingAsync(int page, int pageSize, CancellationToken ct = default)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = DefaultPageSize;
        if (pageSize > MaxPageSize) pageSize = MaxPageSize;

        var query = _db.Reports.AsNoTracking().Where(r => r.Status == ReportStatus.Pending);
        var totalCount = await query.CountAsync(ct);

        var items = await query
            .OrderByDescending(r => r.CreatedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(r => new ReportDto
            {
                Id = r.Id,
                ReporterId = r.ReporterId,
                PostId = r.PostId,
                CommentId = r.CommentId,
                TargetUserId = r.TargetUserId,
                Reason = r.Reason,
                Status = r.Status,
                CreatedAtUtc = r.CreatedAtUtc
            })
            .ToListAsync(ct);

        if (items.Count > 0)
        {
            var reporterIds = items.Select(i => i.ReporterId).Distinct().ToList();
            var reporterNames = await _db.MemberProfiles.AsNoTracking()
                .Where(p => reporterIds.Contains(p.UserId))
                .Select(p => new { p.UserId, p.FullName })
                .ToDictionaryAsync(x => x.UserId, x => x.FullName, ct);

            foreach (var item in items)
            {
                item.ReporterName = reporterNames.TryGetValue(item.ReporterId, out var fullName)
                    ? fullName
                    : "Unknown member";
            }
        }

        return PagedResult<ReportDto>.Create(items, page, pageSize, totalCount);
    }

    public async Task ResolveAsync(Guid reportId, ReportStatus status, Guid reviewerId, bool removeContent, CancellationToken ct = default)
    {
        var report = await _db.Reports.FirstOrDefaultAsync(r => r.Id == reportId, ct);
        if (report is null)
        {
            throw new InvalidOperationException("Report not found.");
        }

        var now = DateTime.UtcNow;
        report.Status = status;
        report.ReviewedByUserId = reviewerId;
        report.ReviewedAtUtc = now;

        if (removeContent)
        {
            if (report.PostId.HasValue)
            {
                var post = await _db.Posts.FirstOrDefaultAsync(p => p.Id == report.PostId.Value, ct);
                if (post is not null)
                {
                    post.IsDeleted = true;
                    post.UpdatedAtUtc = now;
                }
            }
            else if (report.CommentId.HasValue)
            {
                var comment = await _db.Comments.FirstOrDefaultAsync(c => c.Id == report.CommentId.Value, ct);
                if (comment is not null)
                {
                    comment.IsDeleted = true;
                    comment.UpdatedAtUtc = now;
                }
            }
            else if (report.TargetUserId.HasValue)
            {
                var profile = await _db.MemberProfiles
                    .FirstOrDefaultAsync(p => p.UserId == report.TargetUserId.Value, ct);
                if (profile is not null)
                {
                    profile.IsApproved = false;
                    profile.UpdatedAtUtc = now;
                }
            }
        }

        await _db.SaveChangesAsync(ct);
    }
}
