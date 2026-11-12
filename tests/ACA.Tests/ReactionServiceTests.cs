using ACA.Application.Services;
using ACA.Domain.Enums;
using ACA.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ACA.Tests;

public class ReactionServiceTests
{
    private static ReactionService CreateService(AppDbContext db) => new(db, new NotificationService(db));

    [Fact]
    public async Task ToggleAsync_SameTypeTwice_RemovesTheReaction()
    {
        using var db = TestDb.Create();
        var author = Guid.NewGuid();
        var reactor = Guid.NewGuid();
        TestData.Member(db, author, "Author");
        TestData.Member(db, reactor, "Reactor");
        var post = TestData.Post(db, author);

        var service = CreateService(db);

        await service.ToggleAsync(post.Id, reactor, ReactionType.Like);
        Assert.Equal(ReactionType.Like, Assert.Single(db.Reactions).Type);

        await service.ToggleAsync(post.Id, reactor, ReactionType.Like);

        Assert.Empty(db.Reactions);
        // Only the first, brand-new reaction generated a notification.
        Assert.Equal(1, await db.Notifications.CountAsync());
    }

    [Fact]
    public async Task ToggleAsync_DifferentType_ReplacesTheExistingRow()
    {
        using var db = TestDb.Create();
        var author = Guid.NewGuid();
        var reactor = Guid.NewGuid();
        TestData.Member(db, author);
        TestData.Member(db, reactor);
        var post = TestData.Post(db, author);

        var service = CreateService(db);

        await service.ToggleAsync(post.Id, reactor, ReactionType.Like);
        await service.ToggleAsync(post.Id, reactor, ReactionType.Love);

        var reaction = Assert.Single(db.Reactions);
        Assert.Equal(ReactionType.Love, reaction.Type);
        Assert.Equal(1, await db.Notifications.CountAsync());

        await service.ToggleAsync(post.Id, reactor, ReactionType.Support);

        Assert.Equal(ReactionType.Support, Assert.Single(db.Reactions).Type);
        Assert.Equal(1, await db.Notifications.CountAsync());
    }

    [Fact]
    public async Task ToggleAsync_KeepsOneRowPerPostAndUser()
    {
        using var db = TestDb.Create();
        var author = Guid.NewGuid();
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        TestData.Member(db, author);
        TestData.Member(db, first);
        TestData.Member(db, second);
        var post = TestData.Post(db, author);
        var otherPost = TestData.Post(db, author);

        var service = CreateService(db);

        await service.ToggleAsync(post.Id, first, ReactionType.Like);
        await service.ToggleAsync(post.Id, first, ReactionType.Love);
        await service.ToggleAsync(post.Id, first, ReactionType.Support);
        await service.ToggleAsync(post.Id, second, ReactionType.Like);
        await service.ToggleAsync(otherPost.Id, first, ReactionType.Like);

        Assert.Single(await db.Reactions.Where(r => r.PostId == post.Id && r.UserId == first).ToListAsync());
        Assert.Equal(3, await db.Reactions.CountAsync());
    }

    [Fact]
    public async Task ToggleAsync_RejectsUnknownTypeAndMissingPost()
    {
        using var db = TestDb.Create();
        var author = Guid.NewGuid();
        TestData.Member(db, author);
        var post = TestData.Post(db, author);

        var service = CreateService(db);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.ToggleAsync(post.Id, Guid.NewGuid(), (ReactionType)99));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.ToggleAsync(Guid.NewGuid(), Guid.NewGuid(), ReactionType.Like));
    }

    [Fact]
    public async Task RemoveAsync_RemovesOnlyTheCallersReaction()
    {
        using var db = TestDb.Create();
        var author = Guid.NewGuid();
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        TestData.Member(db, author);
        TestData.Member(db, first);
        TestData.Member(db, second);
        var post = TestData.Post(db, author);

        var service = CreateService(db);
        await service.ToggleAsync(post.Id, first, ReactionType.Like);
        await service.ToggleAsync(post.Id, second, ReactionType.Love);

        await service.RemoveAsync(post.Id, first);
        Assert.Equal(second, Assert.Single(db.Reactions).UserId);

        // Removing again is a no-op.
        await service.RemoveAsync(post.Id, first);
        Assert.Single(db.Reactions);
    }
}
