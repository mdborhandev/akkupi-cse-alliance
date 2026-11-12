using ACA.Application.DTOs;
using ACA.Application.DTOs.Common;
using ACA.Application.Interfaces;
using ACA.Domain.Entities;
using ACA.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace ACA.Application.Services;

/// <summary>Case-insensitive, filterable and paginated member directory search.</summary>
public class MemberSearchService : IMemberSearchService
{
    public const int DefaultPageSize = 12;
    public const int MaxPageSize = 60;

    private readonly IAppDbContext _db;

    public MemberSearchService(IAppDbContext db)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
    }

    public async Task<PagedResult<MemberSummaryDto>> SearchAsync(string? query, Guid? batchId, MemberType? memberType, int page, int pageSize, CancellationToken ct = default)
    {
        if (page < 1) page = 1;
        if (pageSize <= 0) pageSize = DefaultPageSize;
        if (pageSize > MaxPageSize) pageSize = MaxPageSize;

        IQueryable<MemberProfile> source = _db.MemberProfiles
            .AsNoTracking()
            .Where(p => p.IsApproved);

        if (!string.IsNullOrWhiteSpace(query))
        {
            var term = query.Trim().ToLowerInvariant();
            source = source.Where(p => p.FullName.ToLower().Contains(term));
        }

        if (batchId is { } selectedBatch && selectedBatch != Guid.Empty)
        {
            source = source.Where(p => p.BatchId == selectedBatch);
        }

        if (memberType is { } selectedType)
        {
            source = source.Where(p => p.MemberType == selectedType);
        }

        var totalCount = await source.CountAsync(ct);
        if (totalCount == 0)
        {
            return PagedResult<MemberSummaryDto>.Create(Array.Empty<MemberSummaryDto>(), page, pageSize, 0);
        }

        var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);
        if (page > totalPages)
        {
            page = totalPages;
        }

        var items = await source
            .OrderBy(p => p.FullName)
            .ThenBy(p => p.UserId)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(p => new MemberSummaryDto
            {
                UserId = p.UserId,
                FullName = p.FullName,
                ProfilePhotoUrl = p.ProfilePhotoUrl,
                BatchName = p.Batch != null ? p.Batch.Name : null,
                MemberType = p.MemberType,
                CurrentStatus = p.CurrentStatus
            })
            .ToListAsync(ct);

        return PagedResult<MemberSummaryDto>.Create(items, page, pageSize, totalCount);
    }
}
