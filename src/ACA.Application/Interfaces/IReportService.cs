using ACA.Application.DTOs;
using ACA.Application.DTOs.Common;
using ACA.Domain.Enums;

namespace ACA.Application.Interfaces;

public interface IReportService
{
    Task<Guid> CreateAsync(CreateReportDto dto, Guid reporterId, CancellationToken ct = default);
    Task<PagedResult<ReportDto>> GetPendingAsync(int page, int pageSize, CancellationToken ct = default);
    Task ResolveAsync(Guid reportId, ReportStatus status, Guid reviewerId, bool removeContent, CancellationToken ct = default);
}
