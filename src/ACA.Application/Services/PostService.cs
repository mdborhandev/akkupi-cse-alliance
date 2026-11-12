using ACA.Application.DTOs;
using ACA.Application.DTOs.Common;
using ACA.Application.Interfaces;
using ACA.Domain.Entities;
using ACA.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace ACA.Application.Services;

/// <summary>
/// Feed, post detail, and post authoring rules. All queries are projected so a page of posts
/// is fetched in a single round-trip (author, batch, images, counts, and the caller's reaction).
/// </summary>
public class PostService : IPostService
{
    public const int MaxContentLength = 5000;
    public const int MaxImagesPerPost = 4;

    private readonly IAppDbContext _db;
    private readonly IImageStorage _images;

    public PostService(IAppDbContext db, IImageStorage images)
    {
        _db = db;
        _images = images;
    }

    public async Task<PagedResult<PostDto>> GetFeedAsync(
        int page,
        int pageSize,
        Guid? batchId,
        Guid? currentUserId,
        CancellationToken ct = default)
    {
        page = page < 1 ? 1 : page;
        pageSize = Math.Clamp(pageSize, 1, 50);

        var posts = BuildPostQuery(batchId);

        var totalCount = await posts.CountAsync(ct);
        if (totalCount == 0)
        {
            return PagedResult<PostDto>.Empty(page, pageSize);
        }

        var rows = await ProjectRows(posts, currentUserId)
            .OrderByDescending(r => r.CreatedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return PagedResult<PostDto>.Create(rows.Select(Map).ToList(), page, pageSize, totalCount);
    }

    public async Task<PostDto?> GetByIdAsync(Guid postId, Guid? currentUserId, CancellationToken ct = default)
    {
        var row = await ProjectRows(BuildPostQuery(null), currentUserId)
            .Where(r => r.Id == postId)
            .FirstOrDefaultAsync(ct);

        return row is null ? null : Map(row);
    }

    public async Task<Guid> CreateAsync(CreatePostDto dto, Guid authorId, CancellationToken ct = default)
    {
        var content = NormalizeContent(dto.Content);

        var images = dto.Images ?? Array.Empty<ImageUpload>();
        if (images.Count > MaxImagesPerPost)
        {
            throw new InvalidOperationException($"A post can include at most {MaxImagesPerPost} images.");
        }

        var now = DateTime.UtcNow;
        var post = new Post
        {
            AuthorId = authorId,
            Content = content,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        var savedUrls = new List<string>(images.Count);
        try
        {
            foreach (var image in images)
            {
                savedUrls.Add(await _images.SaveAsync(image, ct));
            }

            for (var i = 0; i < savedUrls.Count; i++)
            {
                post.Images.Add(new PostImage
                {
                    PostId = post.Id,
                    Url = savedUrls[i],
                    SortOrder = i,
                    CreatedAtUtc = now
                });
            }

            _db.Posts.Add(post);
            await _db.SaveChangesAsync(ct);
        }
        catch
        {
            // Never leave orphaned files behind when validation or persistence fails.
            foreach (var url in savedUrls)
            {
                await _images.DeleteAsync(url, ct);
            }

            throw;
        }

        return post.Id;
    }

    public async Task UpdateAsync(
        Guid postId,
        UpdatePostDto dto,
        Guid currentUserId,
        bool isAdmin,
        CancellationToken ct = default)
    {
        var post = await _db.Posts.FirstOrDefaultAsync(p => p.Id == postId && !p.IsDeleted, ct);
        if (post is null)
        {
            throw new KeyNotFoundException("Post not found.");
        }

        if (!isAdmin && post.AuthorId != currentUserId)
        {
            throw new UnauthorizedAccessException("You can only edit your own posts.");
        }

        post.Content = NormalizeContent(dto.Content);
        post.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(Guid postId, Guid currentUserId, bool isAdmin, CancellationToken ct = default)
    {
        var post = await _db.Posts.FirstOrDefaultAsync(p => p.Id == postId && !p.IsDeleted, ct);
        if (post is null)
        {
            throw new KeyNotFoundException("Post not found.");
        }

        if (!isAdmin && post.AuthorId != currentUserId)
        {
            throw new UnauthorizedAccessException("You can only delete your own posts.");
        }

        post.IsDeleted = true;
        post.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
    }

    private IQueryable<Post> BuildPostQuery(Guid? batchId)
    {
        var posts = _db.Posts.AsNoTracking().Where(p => !p.IsDeleted);

        if (batchId.HasValue)
        {
            var batch = batchId.Value;
            posts = posts.Where(p => _db.MemberProfiles.Any(mp => mp.UserId == p.AuthorId && mp.BatchId == batch));
        }

        return posts;
    }

    private IQueryable<PostRow> ProjectRows(IQueryable<Post> posts, Guid? currentUserId)
    {
        // Guid.Empty never matches a real user, so anonymous callers get a null MyReaction.
        var viewerId = currentUserId ?? Guid.Empty;

        return from p in posts
               join mp in _db.MemberProfiles.AsNoTracking() on p.AuthorId equals mp.UserId into authors
               from mp in authors.DefaultIfEmpty()
               select new PostRow
               {
                   Id = p.Id,
                   AuthorId = p.AuthorId,
                   AuthorName = mp == null ? string.Empty : mp.FullName,
                   AuthorPhotoUrl = mp == null ? null : mp.ProfilePhotoUrl,
                   BatchName = mp == null
                       ? null
                       : _db.Batches.Where(b => b.Id == mp.BatchId).Select(b => b.Name).FirstOrDefault(),
                   MemberType = mp == null ? MemberType.CurrentStudent : mp.MemberType,
                   Content = p.Content,
                   CreatedAtUtc = p.CreatedAtUtc,
                   UpdatedAtUtc = p.UpdatedAtUtc,
                   Images = p.Images
                       .OrderBy(i => i.SortOrder)
                       .Select(i => new PostImageDto { Id = i.Id, Url = i.Url })
                       .ToList(),
                   ReactionCount = p.Reactions.Count(),
                   CommentCount = p.Comments.Count(c => !c.IsDeleted),
                   MyReaction = p.Reactions
                       .Where(r => r.UserId == viewerId)
                       .Select(r => (ReactionType?)r.Type)
                       .FirstOrDefault()
               };
    }

    private static string NormalizeContent(string? content)
    {
        var value = (content ?? string.Empty).Trim();
        if (value.Length == 0)
        {
            throw new InvalidOperationException("Post content cannot be empty.");
        }

        if (value.Length > MaxContentLength)
        {
            throw new InvalidOperationException($"Post content cannot exceed {MaxContentLength} characters.");
        }

        return value;
    }

    private static PostDto Map(PostRow row) => new()
    {
        Id = row.Id,
        AuthorId = row.AuthorId,
        AuthorName = row.AuthorName,
        AuthorPhotoUrl = row.AuthorPhotoUrl,
        BatchName = row.BatchName,
        MemberType = row.MemberType,
        Content = row.Content,
        CreatedAtUtc = row.CreatedAtUtc,
        // UpdatedAtUtc tracks creation time until an edit happens.
        IsEdited = row.UpdatedAtUtc > row.CreatedAtUtc.AddSeconds(1),
        Images = row.Images,
        ReactionCount = row.ReactionCount,
        CommentCount = row.CommentCount,
        MyReaction = row.MyReaction
    };

    /// <summary>Flat projection target so the feed and the detail page share one query shape.</summary>
    private sealed class PostRow
    {
        public Guid Id { get; set; }
        public Guid AuthorId { get; set; }
        public string AuthorName { get; set; } = string.Empty;
        public string? AuthorPhotoUrl { get; set; }
        public string? BatchName { get; set; }
        public MemberType MemberType { get; set; }
        public string Content { get; set; } = string.Empty;
        public DateTime CreatedAtUtc { get; set; }
        public DateTime UpdatedAtUtc { get; set; }
        public List<PostImageDto> Images { get; set; } = new();
        public int ReactionCount { get; set; }
        public int CommentCount { get; set; }
        public ReactionType? MyReaction { get; set; }
    }
}
