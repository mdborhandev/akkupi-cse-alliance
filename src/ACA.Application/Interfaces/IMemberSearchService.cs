using ACA.Application.DTOs;
using ACA.Application.DTOs.Common;
using ACA.Domain.Enums;

namespace ACA.Application.Interfaces;

public interface IMemberSearchService
{
    Task<PagedResult<MemberSummaryDto>> SearchAsync(string? query, Guid? batchId, MemberType? memberType, int page, int pageSize, CancellationToken ct = default);
}
