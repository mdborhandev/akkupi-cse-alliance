using ACA.Application.DTOs;

namespace ACA.Application.Interfaces;

public interface IBatchService
{
    Task<IReadOnlyList<BatchDto>> GetAllAsync(CancellationToken ct = default);
    Task<BatchDto> CreateAsync(string name, string session, int batchNo, CancellationToken ct = default);
    Task UpdateAsync(Guid id, string name, string session, int batchNo, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}
