using ACA.Application.DTOs;

namespace ACA.Application.Interfaces;

public interface IProfileService
{
    Task<MemberDetailDto?> GetAsync(Guid userId, CancellationToken ct = default);
    Task UpdateAsync(Guid userId, UpdateProfileDto dto, CancellationToken ct = default);
    Task EnsureProfileAsync(Guid userId, string fullName, Guid? batchId, Domain.Enums.MemberType memberType, string? currentStatus, CancellationToken ct = default);
}
