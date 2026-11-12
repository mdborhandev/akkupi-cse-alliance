using ACA.Application.DTOs;
using ACA.Application.Services;
using ACA.Domain.Enums;
using ACA.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ACA.Tests;

public class ReportServiceTests
{
    [Fact]
    public async Task CreateAsync_RequiresExactlyOneTarget()
    {
        using var db = TestDb.Create();
        var reporter = Guid.NewGuid();
        TestData.Member(db, reporter);
        var service = new ReportService(db);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.CreateAsync(new CreateReportDto { Reason = "Spam content" }, reporter));

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.CreateAsync(
                new CreateReportDto
                {
                    PostId = Guid.NewGuid(),
                    CommentId = Guid.NewGuid(),
                    Reason = "Spam content"
                },
                reporter));

        Assert.Empty(db.Reports);
    }

    [Fact]
    public async Task CreateAsync_RequiresAReasonWithinLengthLimits()
    {
        using var db = TestDb.Create();
        var reporter = Guid.NewGuid();
        TestData.Member(db, reporter);
        var service = new ReportService(db);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.CreateAsync(new CreateReportDto { PostId = Guid.NewGuid(), Reason = "  " }, reporter));

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.CreateAsync(new CreateReportDto { PostId = Guid.NewGuid(), Reason = "ab" }, reporter));

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.CreateAsync(
                new CreateReportDto { PostId = Guid.NewGuid(), Reason = new string('x', 1001) },
                reporter));

        Assert.Empty(db.Reports);
    }

    [Fact]
    public async Task CreateAsync_StoresAPendingReportWithTrimmedReason()
    {
        using var db = TestDb.Create();
        var reporter = Guid.NewGuid();
        TestData.Member(db, reporter);
        var service = new ReportService(db);

        var reportId = await service.CreateAsync(
            new CreateReportDto { TargetUserId = Guid.NewGuid(), Reason = "  Harassment  " },
            reporter);

        var report = await db.Reports.SingleAsync(r => r.Id == reportId);
        Assert.Equal("Harassment", report.Reason);
        Assert.Equal(ReportStatus.Pending, report.Status);
        Assert.Equal(reporter, report.ReporterId);
        Assert.Null(report.ReviewedAtUtc);
        Assert.Null(report.ReviewedByUserId);
    }

    [Fact]
    public async Task ResolveAsync_SetsStatusReviewerAndSoftDeletesThePost()
    {
        using var db = TestDb.Create();
        var reporter = Guid.NewGuid();
        var reviewer = Guid.NewGuid();
        TestData.Member(db, reporter);
        TestData.Member(db, reviewer);
        var post = TestData.Post(db, Guid.NewGuid(), "Reported post");

        var service = new ReportService(db);
        var reportId = await service.CreateAsync(
            new CreateReportDto { PostId = post.Id, Reason = "Abusive post" },
            reporter);

        await service.ResolveAsync(reportId, ReportStatus.Reviewed, reviewer, removeContent: true);

        var report = await db.Reports.SingleAsync(r => r.Id == reportId);
        Assert.Equal(ReportStatus.Reviewed, report.Status);
        Assert.Equal(reviewer, report.ReviewedByUserId);
        Assert.NotNull(report.ReviewedAtUtc);

        var storedPost = await db.Posts.SingleAsync(p => p.Id == post.Id);
        Assert.True(storedPost.IsDeleted);

        var pending = await service.GetPendingAsync(1, 20);
        Assert.Equal(0, pending.TotalCount);
    }

    [Fact]
    public async Task ResolveAsync_WithoutRemoveContent_KeepsThePostVisible()
    {
        using var db = TestDb.Create();
        var reporter = Guid.NewGuid();
        var reviewer = Guid.NewGuid();
        var postAuthor = Guid.NewGuid();
        TestData.Member(db, reporter);
        TestData.Member(db, reviewer);
        TestData.Member(db, postAuthor);
        var post = TestData.Post(db, postAuthor, "Probably fine");

        var service = new ReportService(db);
        var reportId = await service.CreateAsync(
            new CreateReportDto { PostId = post.Id, Reason = "Needs review" },
            reporter);

        await service.ResolveAsync(reportId, ReportStatus.Dismissed, reviewer, removeContent: false);

        var report = await db.Reports.SingleAsync(r => r.Id == reportId);
        Assert.Equal(ReportStatus.Dismissed, report.Status);
        Assert.Equal(reviewer, report.ReviewedByUserId);
        Assert.False((await db.Posts.SingleAsync(p => p.Id == post.Id)).IsDeleted);
    }

    [Fact]
    public async Task ResolveAsync_RemoveContent_SoftDeletesTheReportedComment()
    {
        using var db = TestDb.Create();
        var reporter = Guid.NewGuid();
        var reviewer = Guid.NewGuid();
        TestData.Member(db, reporter);
        TestData.Member(db, reviewer);

        var post = TestData.Post(db, Guid.NewGuid());
        var comment = TestData.Comment(db, post.Id, Guid.NewGuid(), "Reported comment");

        var service = new ReportService(db);
        var reportId = await service.CreateAsync(
            new CreateReportDto { CommentId = comment.Id, Reason = "Offensive comment" },
            reporter);

        await service.ResolveAsync(reportId, ReportStatus.Reviewed, reviewer, removeContent: true);

        Assert.True((await db.Comments.SingleAsync(c => c.Id == comment.Id)).IsDeleted);
        Assert.False((await db.Posts.SingleAsync(p => p.Id == post.Id)).IsDeleted);
    }

    [Fact]
    public async Task ResolveAsync_RemoveContent_UnapprovesTheReportedMember()
    {
        using var db = TestDb.Create();
        var reporter = Guid.NewGuid();
        var reviewer = Guid.NewGuid();
        var target = Guid.NewGuid();
        TestData.Member(db, reporter);
        TestData.Member(db, reviewer);
        TestData.Member(db, target, "Target Member");

        var service = new ReportService(db);
        var reportId = await service.CreateAsync(
            new CreateReportDto { TargetUserId = target, Reason = "Fake profile" },
            reporter);

        await service.ResolveAsync(reportId, ReportStatus.Reviewed, reviewer, removeContent: true);

        Assert.False((await db.MemberProfiles.SingleAsync(p => p.UserId == target)).IsApproved);
    }

    [Fact]
    public async Task ResolveAsync_UnknownReport_Throws()
    {
        using var db = TestDb.Create();
        var service = new ReportService(db);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.ResolveAsync(Guid.NewGuid(), ReportStatus.Reviewed, Guid.NewGuid(), removeContent: false));
    }
}
