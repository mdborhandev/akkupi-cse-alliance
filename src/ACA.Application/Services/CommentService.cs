using ACA.Application.DTOs;
using ACA.Application.DTOs.Common;
using ACA.Application.Interfaces;
using ACA.Domain.Entities;
using ACA.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace ACA.Application.Services;

public class CommentService : ICommentService
{
    private const int MaxContentLength = 2000;
    private const int MaxPageSize = 100;

    private readonly IAppDbContext _db;
    private readonly INotificationService _notifications;

    public CommentService(IAppDbContext db, INotificationService notifications)
    {
        _db = db;
        _notifications = notifications;
    }

    public async Task<PagedResult<CommentDto>> GetForPostAsync(Guid postId, int page, int pageSize, Guid? currentUserId, CancellationToken ct = default)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 20;
        if (pageSize > MaxPageSize) pageSize = MaxPageSize;

        var topLevelQuery = _db.Comments.AsNoTracking()
            .Where(c => c.PostId == postId && c.ParentCommentId == null && !c.IsDeleted);

        var total = await topLevelQuery.CountAsync(ct);

        var topLevel = await topLevelQuery
            .OrderByDescending(c => c.CreatedAtUtc)
            .ThenByDescending(c => c.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        if (topLevel.Count == 0)
        {
            return PagedResult<CommentDto>.Create(Array.Empty<CommentDto>(), page, pageSize, total);
        }

        var topLevelIds = topLevel.Select(c => c.Id).ToHashSet();

        // Load every reply on the post so that replies to replies are still shown under
        // their top-level ancestor (the UI keeps one level of nesting).
        var replies = await _db.Comments.AsNoTracking()
            .Where(c => c.PostId == postId && c.ParentCommentId != null && !c.IsDeleted)
            .OrderBy(c => c.CreatedAtUtc)
            .ToListAsync(ct);

        var authorIds = topLevel.Select(c => c.AuthorId)
            .Concat(replies.Select(c => c.AuthorId))
            .Distinct()
            .ToList();

        var profiles = await _db.MemberProfiles.AsNoTracking()
            .Where(p => authorIds.Contains(p.UserId))
            .ToDictionaryAsync(p => p.UserId, ct);

        var dtos = topLevel
            .Select(c => Map(c, profiles))
            .ToDictionary(d => d.Id);

        var parentOf = replies
            .Where(r => r.ParentCommentId.HasValue)
            .ToDictionary(r => r.Id, r => r.ParentCommentId!.Value);

        foreach (var reply in replies.OrderBy(r => r.CreatedAtUtc))
        {
            var rootId = ResolveRootId(reply.Id, parentOf, topLevelIds);
            if (rootId is Guid root && dtos.TryGetValue(root, out var rootDto))
            {
                rootDto.Replies.Add(Map(reply, profiles));
            }
        }

        return PagedResult<CommentDto>.Create(dtos.Values.OrderByDescending(d => d.CreatedAtUtc).ToList(), page, pageSize, total);
    }

    public async Task<Guid> AddAsync(Guid postId, Guid authorId, string content, Guid? parentCommentId, CancellationToken ct = default)
    {
        var text = (content ?? string.Empty).Trim();
        if (text.Length == 0)
        {
            throw new InvalidOperationException("Comment cannot be empty.");
        }

        if (text.Length > MaxContentLength)
        {
            throw new InvalidOperationException($"Comment must be {MaxContentLength} characters or fewer.");
        }

        var post = await _db.Posts.AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == postId && !p.IsDeleted, ct);

        if (post is null)
        {
            throw new InvalidOperationException("This post no longer exists.");
        }

        Comment? parent = null;
        if (parentCommentId.HasValue)
        {
            parent = await _db.Comments.AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == parentCommentId.Value && !c.IsDeleted, ct);

            if (parent is null || parent.PostId != postId)
            {
                throw new InvalidOperationException("The comment you are replying to no longer exists.");
            }
        }

        var now = DateTime.UtcNow;
        var comment = new Comment
        {
            PostId = postId,
            AuthorId = authorId,
            ParentCommentId = parent?.Id,
            Content = text,
            IsDeleted = false,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        _db.Comments.Add(comment);
        await _db.SaveChangesAsync(ct);

        var recipientId = parent?.AuthorId ?? post.AuthorId;
        var notificationType = parent is null ? NotificationType.Comment : NotificationType.Reply;
        await _notifications.NotifyAsync(recipientId, authorId, notificationType, postId, comment.Id, ct);

        return comment.Id;
    }

    public async Task DeleteAsync(Guid commentId, Guid currentUserId, bool isAdmin, CancellationToken ct = default)
    {
        var comment = await _db.Comments
            .FirstOrDefaultAsync(c => c.Id == commentId && !c.IsDeleted, ct);

        if (comment is null)
        {
            return;
        }

        if (!isAdmin && comment.AuthorId != currentUserId)
        {
            throw new UnauthorizedAccessException("You can only delete your own comments.");
        }

        comment.IsDeleted = true;
        comment.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
    }

    private static Guid? ResolveRootId(Guid commentId, IReadOnlyDictionary<Guid, Guid> parentOf, ISet<Guid> topLevelIds)
    {
        var current = commentId;
        for (var depth = 0; depth < 20; depth++)
        {
            if (topLevelIds.Contains(current))
            {
                return current;
            }

            if (!parentOf.TryGetValue(current, out var parentId))
            {
                return null;
            }

            current = parentId;
        }

        return null;
    }

    private static CommentDto Map(Comment comment, IReadOnlyDictionary<Guid, MemberProfile> profiles)
    {
        profiles.TryGetValue(comment.AuthorId, out var author);
        var name = string.IsNullOrWhiteSpace(author?.FullName) ? "Member" : author!.FullName;

        return new CommentDto
        {
            Id = comment.Id,
            PostId = comment.PostId,
            AuthorId = comment.AuthorId,
            AuthorName = name,
            AuthorPhotoUrl = author?.ProfilePhotoUrl,
            ParentCommentId = comment.ParentCommentId,
            Content = comment.Content,
            CreatedAtUtc = comment.CreatedAtUtc,
            Replies = new List<CommentDto>()
        };
    }
}
