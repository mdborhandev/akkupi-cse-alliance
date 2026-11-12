using ACA.Application.DTOs;
using ACA.Application.Services;
using ACA.Domain.Entities;
using ACA.Domain.Enums;
using ACA.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ACA.Tests;

public class PostServiceTests
{
    private static PostService CreateService(AppDbContext db) => new(db, new FakeImageStorage());

    [Fact]
    public async Task GetFeedAsync_ReturnsNewestFirst_AndPaginates()
    {
        using var db = TestDb.Create();
        var author = Guid.NewGuid();
        TestData.Member(db, author, "Feed Author");

        var start = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        for (var i = 1; i <= 5; i++)
        {
            TestData.Post(db, author, $"Post {i}", start.AddMinutes(i));
        }

        var service = CreateService(db);

        var first = await service.GetFeedAsync(1, 2, null, null);

        Assert.Equal(5, first.TotalCount);
        Assert.Equal(3, first.TotalPages);
        Assert.Equal(2, first.Items.Count);
        Assert.Equal(new[] { "Post 5", "Post 4" }, first.Items.Select(p => p.Content));
        Assert.True(first.HasNext);
        Assert.False(first.HasPrevious);

        var third = await service.GetFeedAsync(3, 2, null, null);

        Assert.Single(third.Items);
        Assert.Equal("Post 1", third.Items[0].Content);
        Assert.False(third.HasNext);
        Assert.True(third.HasPrevious);
    }

    [Fact]
    public async Task GetFeedAsync_ExcludesSoftDeletedPosts_AndDetailReturnsNull()
    {
        using var db = TestDb.Create();
        var author = Guid.NewGuid();
        TestData.Member(db, author, "Author");

        var visible = TestData.Post(db, author, "Visible", DateTime.UtcNow.AddMinutes(-1));
        var deleted = TestData.Post(db, author, "Hidden", DateTime.UtcNow.AddMinutes(-2), isDeleted: true);

        var service = CreateService(db);
        var feed = await service.GetFeedAsync(1, 10, null, null);

        Assert.Equal(1, feed.TotalCount);
        Assert.Equal(visible.Id, Assert.Single(feed.Items).Id);
        Assert.Null(await service.GetByIdAsync(deleted.Id, null));
        Assert.NotNull(await service.GetByIdAsync(visible.Id, null));
    }

    [Fact]
    public async Task GetFeedAsync_ProjectsCountsAndViewerReaction()
    {
        using var db = TestDb.Create();
        var author = Guid.NewGuid();
        var viewer = Guid.NewGuid();
        TestData.Member(db, author, "Feed Author");
        TestData.Member(db, viewer, "Viewer");

        var post = TestData.Post(db, author, "Projection test");

        db.Comments.Add(new Comment { PostId = post.Id, AuthorId = viewer, Content = "Nice" });
        db.Comments.Add(new Comment { PostId = post.Id, AuthorId = viewer, Content = "Gone", IsDeleted = true });
        db.Reactions.Add(new Reaction { PostId = post.Id, UserId = viewer, Type = ReactionType.Love });
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var item = Assert.Single((await service.GetFeedAsync(1, 10, null, viewer)).Items);

        Assert.Equal("Feed Author", item.AuthorName);
        Assert.Equal(1, item.CommentCount);
        Assert.Equal(1, item.ReactionCount);
        Assert.Equal(ReactionType.Love, item.MyReaction);

        var anonymous = Assert.Single((await service.GetFeedAsync(1, 10, null, null)).Items);
        Assert.Null(anonymous.MyReaction);
    }

    [Fact]
    public async Task GetFeedAsync_FiltersByAuthorBatch()
    {
        using var db = TestDb.Create();
        var batchA = TestData.Batch(db, "Batch A", "2023-2024", 12);
        var batchB = TestData.Batch(db, "Batch B", "2024-2025", 13);
        var authorA = Guid.NewGuid();
        var authorB = Guid.NewGuid();
        TestData.Member(db, authorA, "A", batchA.Id);
        TestData.Member(db, authorB, "B", batchB.Id);

        TestData.Post(db, authorA, "From A", DateTime.UtcNow.AddMinutes(-2));
        TestData.Post(db, authorB, "From B", DateTime.UtcNow.AddMinutes(-1));

        var service = CreateService(db);
        var filtered = await service.GetFeedAsync(1, 10, batchA.Id, null);

        Assert.Equal(1, filtered.TotalCount);
        Assert.Equal("From A", Assert.Single(filtered.Items).Content);
    }

    [Fact]
    public async Task DeleteAsync_SoftDeletesWithoutRemovingTheRow()
    {
        using var db = TestDb.Create();
        var author = Guid.NewGuid();
        TestData.Member(db, author);
        var post = TestData.Post(db, author, "Delete me");

        var service = CreateService(db);
        await service.DeleteAsync(post.Id, author, isAdmin: false);

        var stored = await db.Posts.SingleAsync(p => p.Id == post.Id);
        Assert.True(stored.IsDeleted);
        Assert.Equal(0, (await service.GetFeedAsync(1, 10, null, null)).TotalCount);
    }

    [Fact]
    public async Task UpdateAndDelete_ByNonOwner_ThrowUnauthorizedAccessException()
    {
        using var db = TestDb.Create();
        var owner = Guid.NewGuid();
        var intruder = Guid.NewGuid();
        TestData.Member(db, owner);
        TestData.Member(db, intruder);
        var post = TestData.Post(db, owner, "Original");

        var service = CreateService(db);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            service.UpdateAsync(post.Id, new UpdatePostDto { Content = "Hijacked" }, intruder, isAdmin: false));

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            service.DeleteAsync(post.Id, intruder, isAdmin: false));

        var stored = await db.Posts.SingleAsync(p => p.Id == post.Id);
        Assert.Equal("Original", stored.Content);
        Assert.False(stored.IsDeleted);
    }

    [Fact]
    public async Task Admin_CanUpdateAndDeleteAnotherMembersPost()
    {
        using var db = TestDb.Create();
        var owner = Guid.NewGuid();
        var admin = Guid.NewGuid();
        TestData.Member(db, owner);
        TestData.Member(db, admin);
        var post = TestData.Post(db, owner, "Original", DateTime.UtcNow.AddHours(-1));

        var service = CreateService(db);

        await service.UpdateAsync(
            post.Id,
            new UpdatePostDto { Content = "  Edited by admin  " },
            admin,
            isAdmin: true);

        var stored = await db.Posts.SingleAsync(p => p.Id == post.Id);
        Assert.Equal("Edited by admin", stored.Content);
        Assert.True(stored.UpdatedAtUtc > stored.CreatedAtUtc);

        await service.DeleteAsync(post.Id, admin, isAdmin: true);
        Assert.True((await db.Posts.SingleAsync(p => p.Id == post.Id)).IsDeleted);
    }

    [Fact]
    public async Task CreateAsync_TrimsContentAndPersistsImages()
    {
        using var db = TestDb.Create();
        var author = Guid.NewGuid();
        TestData.Member(db, author);

        var storage = new FakeImageStorage();
        var service = new PostService(db, storage);

        var postId = await service.CreateAsync(
            new CreatePostDto
            {
                Content = "   Hello ACA   ",
                Images = new[]
                {
                    new ImageUpload { FileName = "one.png", ContentType = "image/png", Length = 1, Content = new MemoryStream(new byte[] { 1 }) },
                    new ImageUpload { FileName = "two.png", ContentType = "image/png", Length = 1, Content = new MemoryStream(new byte[] { 2 }) }
                }
            },
            author);

        Assert.Equal(2, storage.SavedUrls.Count);

        var stored = await db.Posts.Include(p => p.Images).SingleAsync(p => p.Id == postId);
        Assert.Equal("Hello ACA", stored.Content);
        Assert.Equal(2, stored.Images.Count);
        Assert.Equal(storage.SavedUrls, stored.Images.OrderBy(i => i.SortOrder).Select(i => i.Url));
    }

    [Fact]
    public async Task CreateAsync_RejectsEmptyContent()
    {
        using var db = TestDb.Create();
        var author = Guid.NewGuid();
        TestData.Member(db, author);

        var service = CreateService(db);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreateAsync(new CreatePostDto { Content = "   " }, author));
    }
}
