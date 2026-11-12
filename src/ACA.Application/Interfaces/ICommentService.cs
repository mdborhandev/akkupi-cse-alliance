using ACA.Application.DTOs;
using ACA.Application.DTOs.Common;

namespace ACA.Application.Interfaces;

public interface ICommentService
{
    Task<PagedResult<CommentDto>> GetForPostAsync(Guid postId, int page, int pageSize, Guid? currentUserId, CancellationToken ct = default);
    Task<Guid> AddAsync(Guid postId, Guid authorId, string content, Guid? parentCommentId, CancellationToken ct = default);
    Task DeleteAsync(Guid commentId, Guid currentUserId, bool isAdmin, CancellationToken ct = default);
}
