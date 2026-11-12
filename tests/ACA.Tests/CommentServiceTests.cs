using ACA.Application.Services;
using ACA.Domain.Entities;
using ACA.Domain.Enums;
using ACA.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ACA.Tests;

public class CommentServiceTests
{
    private static CommentService CreateService(AppDbContext db) => new(db, new NotificationService(db));

    [Fact]
    public async Task AddAsync_RejectsEmptyOrWhitespaceContent()
    {
        using var db = TestDb.Create();
        var author = Guid.NewGuid();
        var commenter = Guid.NewGuid();
        TestData.Member(db, author);
        TestData.Member(db, commenter);
        var post = TestData.Post(db, author);

        var service = CreateService(db);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.AddAsync(post.Id, commenter, "   ", null));

        Assert.Contains("empty", ex.Message);
        Assert.Empty(db.Comments);
    }

    [Fact]
    public async Task AddAsync_RejectsParentCommentFromAnotherPost()
    {
        using var db = TestDb.Create();
        var author = Guid.NewGuid();
        var commenter = Guid.NewGuid();
        TestData.Member(db, author);
        TestData.Member(db, commenter);

        var postA = TestData.Post(db, author, "Post A");
        var postB = TestData.Post(db, author, "Post B");
        var commentOnB = TestData.Comment(db, postB.Id, author, "On B");

        var service = CreateService(db);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.AddAsync(postA.Id, commenter, "Reply to B", commentOnB.Id));

        Assert.Empty(db.Comments.Where(c => c.PostId == postA.Id));
    }

    [Fact]
    public async Task AddAsync_DoesNotNotifyAuthorAboutTheirOwnComment()
    {
        using var db = TestDb.Create();
        var author = Guid.NewGuid();
        TestData.Member(db, author);
        var post = TestData.Post(db, author);

        var service = CreateService(db);
        await service.AddAsync(post.Id, author, "Talking to myself", null);

        Assert.Equal(0, await db.Notifications.CountAsync());
    }

    [Fact]
    public async Task AddAsync_NotifiesPostAuthor_WhenSomeoneElseComments()
    {
        using var db = TestDb.Create();
        var author = Guid.NewGuid();
        var commenter = Guid.NewGuid();
        TestData.Member(db, author);
        TestData.Member(db, commenter);
        var post = TestData.Post(db, author);

        var service = CreateService(db);
        var commentId = await service.AddAsync(post.Id, commenter, "Nice post", null);

        var notification = Assert.Single(db.Notifications);
        Assert.Equal(author, notification.RecipientId);
        Assert.Equal(commenter, notification.ActorId);
        Assert.Equal(NotificationType.Comment, notification.Type);
        Assert.Equal(post.Id, notification.PostId);
        Assert.Equal(commentId, notification.CommentId);
        Assert.False(notification.IsRead);
    }

    [Fact]
    public async Task AddAsync_ReplyNotifiesParentAuthor_ButNotWhenReplyingToSelf()
    {
        using var db = TestDb.Create();
        var postAuthor = Guid.NewGuid();
        var commenter = Guid.NewGuid();
        TestData.Member(db, postAuthor);
        TestData.Member(db, commenter);
        var post = TestData.Post(db, postAuthor);

        var service = CreateService(db);

        var topLevelId = await service.AddAsync(post.Id, commenter, "Top level", null);
        var replyId = await service.AddAsync(post.Id, postAuthor, "Reply to commenter", topLevelId);

        var notifications = await db.Notifications.OrderBy(n => n.CreatedAtUtc).ToListAsync();
        Assert.Equal(2, notifications.Count);
        Assert.Equal(NotificationType.Reply, notifications[1].Type);
        Assert.Equal(commenter, notifications[1].RecipientId);
        Assert.Equal(postAuthor, notifications[1].ActorId);
        Assert.Equal(replyId, notifications[1].CommentId);

        // The commenter replies to their own top-level comment: no new notification.
        await service.AddAsync(post.Id, commenter, "Replying to myself", topLevelId);
        Assert.Equal(2, await db.Notifications.CountAsync());
    }

    [Fact]
    public async Task DeleteAsync_RejectsNonOwner_ButAllowsAdmin()
    {
        using var db = TestDb.Create();
        var author = Guid.NewGuid();
        var owner = Guid.NewGuid();
        var intruder = Guid.NewGuid();
        var admin = Guid.NewGuid();
        TestData.Member(db, author);
        TestData.Member(db, owner);
        TestData.Member(db, intruder);
        TestData.Member(db, admin);
        var post = TestData.Post(db, author);

        var comment = TestData.Comment(db, post.Id, owner, "Mine");
        var service = CreateService(db);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            service.DeleteAsync(comment.Id, intruder, isAdmin: false));

        Assert.False((await db.Comments.SingleAsync(c => c.Id == comment.Id)).IsDeleted);

        await service.DeleteAsync(comment.Id, admin, isAdmin: true);
        Assert.True((await db.Comments.SingleAsync(c => c.Id == comment.Id)).IsDeleted);
    }
}
