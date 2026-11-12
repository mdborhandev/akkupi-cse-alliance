using ACA.Application.Services;
using ACA.Domain.Enums;
using ACA.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ACA.Tests;

public class NotificationServiceTests
{
    [Fact]
    public async Task NotifyAsync_SkipsSelfNotificationsAndEmptyIds()
    {
        using var db = TestDb.Create();
        var user = Guid.NewGuid();
        TestData.Member(db, user);

        var service = new NotificationService(db);

        await service.NotifyAsync(user, user, NotificationType.Comment, null, null);
        await service.NotifyAsync(user, Guid.Empty, NotificationType.Comment, null, null);
        await service.NotifyAsync(Guid.Empty, user, NotificationType.Comment, null, null);

        Assert.Empty(db.Notifications);
    }

    [Fact]
    public async Task NotifyAsync_CreatesAnUnreadNotificationForAnotherMember()
    {
        using var db = TestDb.Create();
        var recipient = Guid.NewGuid();
        var actor = Guid.NewGuid();
        TestData.Member(db, recipient);
        TestData.Member(db, actor, "Actor Name");
        var postId = Guid.NewGuid();

        var service = new NotificationService(db);
        await service.NotifyAsync(recipient, actor, NotificationType.Reaction, postId, null);

        var notification = Assert.Single(db.Notifications);
        Assert.Equal(recipient, notification.RecipientId);
        Assert.Equal(actor, notification.ActorId);
        Assert.Equal(NotificationType.Reaction, notification.Type);
        Assert.Equal(postId, notification.PostId);
        Assert.False(notification.IsRead);
    }

    [Fact]
    public async Task GetUnreadCountAsync_CountsOnlyTheRecipientsUnreadRows()
    {
        using var db = TestDb.Create();
        var user = Guid.NewGuid();
        var other = Guid.NewGuid();
        TestData.Member(db, user);
        TestData.Member(db, other);

        TestData.Notification(db, user, isRead: false);
        TestData.Notification(db, user, isRead: false);
        TestData.Notification(db, user, isRead: true);
        TestData.Notification(db, other, isRead: false);

        var service = new NotificationService(db);
        var count = await service.GetUnreadCountAsync(user);

        Assert.Equal(2, count);
    }

    [Fact]
    public async Task MarkAllReadAsync_MarksOnlyTheUsersNotifications()
    {
        using var db = TestDb.Create();
        var user = Guid.NewGuid();
        var other = Guid.NewGuid();
        TestData.Member(db, user);
        TestData.Member(db, other);

        TestData.Notification(db, user, isRead: false);
        TestData.Notification(db, user, isRead: false);
        TestData.Notification(db, other, isRead: false);

        var service = new NotificationService(db);
        await service.MarkAllReadAsync(user);

        Assert.Equal(0, await service.GetUnreadCountAsync(user));
        Assert.Equal(1, await service.GetUnreadCountAsync(other));
    }

    [Fact]
    public async Task MarkReadAsync_IgnoresNotificationsOwnedByAnotherUser()
    {
        using var db = TestDb.Create();
        var user = Guid.NewGuid();
        var other = Guid.NewGuid();
        TestData.Member(db, user);
        TestData.Member(db, other);

        var foreign = TestData.Notification(db, other, isRead: false);
        var mine = TestData.Notification(db, user, isRead: false);

        var service = new NotificationService(db);

        await service.MarkReadAsync(foreign.Id, user);
        Assert.False((await db.Notifications.SingleAsync(n => n.Id == foreign.Id)).IsRead);

        await service.MarkReadAsync(mine.Id, user);
        Assert.True((await db.Notifications.SingleAsync(n => n.Id == mine.Id)).IsRead);
    }

    [Fact]
    public async Task GetForUserAsync_ReturnsNewestFirstWithActorAndMessage()
    {
        using var db = TestDb.Create();
        var user = Guid.NewGuid();
        var actor = Guid.NewGuid();
        TestData.Member(db, user);
        TestData.Member(db, actor, "Nadia");

        var older = TestData.Notification(db, user, actor, isRead: true);
        older.CreatedAtUtc = DateTime.UtcNow.AddMinutes(-5);
        var newer = TestData.Notification(db, user, actor, isRead: false);
        newer.CreatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync();

        var service = new NotificationService(db);
        var page = await service.GetForUserAsync(user, 1, 10);

        Assert.Equal(2, page.TotalCount);
        Assert.Equal(new[] { newer.Id, older.Id }, page.Items.Select(n => n.Id));
        Assert.All(page.Items, n =>
        {
            Assert.Equal("Nadia", n.ActorName);
            Assert.Contains("Nadia", n.Message);
        });
    }
}
