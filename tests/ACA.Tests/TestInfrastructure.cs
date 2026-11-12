using ACA.Application.DTOs;
using ACA.Application.Interfaces;
using ACA.Domain.Entities;
using ACA.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ACA.Tests;

/// <summary>
/// Creates an isolated EF Core InMemory database per test. A fresh Guid database name is used so
/// tests can never observe each other's rows even when xUnit runs them in parallel.
/// </summary>
internal static class TestDb
{
    public static AppDbContext Create()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"aca-tests-{Guid.NewGuid():N}")
            .Options;

        return new AppDbContext(options);
    }
}

/// <summary>In-memory IImageStorage double; never touches the file system.</summary>
internal sealed class FakeImageStorage : IImageStorage
{
    public List<string> SavedUrls { get; } = new();
    public List<string> DeletedUrls { get; } = new();
    public Func<ImageUpload, string>? UrlFactory { get; set; }
    public List<string> FailOnUrls { get; } = new();

    public Task<string> SaveAsync(ImageUpload image, CancellationToken ct = default)
    {
        var url = UrlFactory?.Invoke(image) ?? $"/uploads/posts/test/{Guid.NewGuid():N}.png";
        SavedUrls.Add(url);
        return Task.FromResult(url);
    }

    public Task DeleteAsync(string url, CancellationToken ct = default)
    {
        DeletedUrls.Add(url);
        return Task.CompletedTask;
    }
}

/// <summary>Small builders for the entities the services need.</summary>
internal static class TestData
{
    public static Batch Batch(
        AppDbContext db,
        string name = "CSE 12th Batch",
        string session = "2023-2024",
        int batchNo = 12)
    {
        var batch = new Batch
        {
            Name = name,
            Session = session,
            BatchNo = batchNo,
            CreatedAtUtc = DateTime.UtcNow
        };

        db.Batches.Add(batch);
        db.SaveChanges();
        return batch;
    }

    public static MemberProfile Member(
        AppDbContext db,
        Guid userId,
        string fullName = "Test Member",
        Guid? batchId = null,
        bool isApproved = true)
    {
        var profile = new MemberProfile
        {
            UserId = userId,
            FullName = fullName,
            BatchId = batchId,
            IsApproved = isApproved,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };

        db.MemberProfiles.Add(profile);
        db.SaveChanges();
        return profile;
    }

    public static Post Post(
        AppDbContext db,
        Guid authorId,
        string content = "A post",
        DateTime? createdAtUtc = null,
        bool isDeleted = false)
    {
        var createdAt = createdAtUtc ?? DateTime.UtcNow;
        var post = new Post
        {
            AuthorId = authorId,
            Content = content,
            IsDeleted = isDeleted,
            CreatedAtUtc = createdAt,
            UpdatedAtUtc = createdAt
        };

        db.Posts.Add(post);
        db.SaveChanges();
        return post;
    }

    public static Comment Comment(
        AppDbContext db,
        Guid postId,
        Guid authorId,
        string content = "A comment",
        Guid? parentCommentId = null,
        bool isDeleted = false)
    {
        var comment = new Comment
        {
            PostId = postId,
            AuthorId = authorId,
            ParentCommentId = parentCommentId,
            Content = content,
            IsDeleted = isDeleted,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };

        db.Comments.Add(comment);
        db.SaveChanges();
        return comment;
    }

    public static Notification Notification(
        AppDbContext db,
        Guid recipientId,
        Guid? actorId = null,
        bool isRead = false,
        Domain.Enums.NotificationType type = Domain.Enums.NotificationType.Comment)
    {
        var notification = new Notification
        {
            RecipientId = recipientId,
            ActorId = actorId,
            Type = type,
            IsRead = isRead,
            CreatedAtUtc = DateTime.UtcNow
        };

        db.Notifications.Add(notification);
        db.SaveChanges();
        return notification;
    }
}
