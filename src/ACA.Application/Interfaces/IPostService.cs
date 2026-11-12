using ACA.Application.DTOs;
using ACA.Application.DTOs.Common;

namespace ACA.Application.Interfaces;

public interface IPostService
{
    Task<PagedResult<PostDto>> GetFeedAsync(int page, int pageSize, Guid? batchId, Guid? currentUserId, CancellationToken ct = default);
    Task<PostDto?> GetByIdAsync(Guid postId, Guid? currentUserId, CancellationToken ct = default);
    Task<Guid> CreateAsync(CreatePostDto dto, Guid authorId, CancellationToken ct = default);
    Task UpdateAsync(Guid postId, UpdatePostDto dto, Guid currentUserId, bool isAdmin, CancellationToken ct = default);
    Task DeleteAsync(Guid postId, Guid currentUserId, bool isAdmin, CancellationToken ct = default);
}
