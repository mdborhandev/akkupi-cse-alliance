using ACA.Application.DTOs;
using ACA.Application.Services;
using ACA.Domain.Enums;
using ACA.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ACA.Tests;

public class ProfileServiceTests
{
    private static ProfileService CreateService(AppDbContext db, FakeImageStorage? storage = null)
        => new(db, storage ?? new FakeImageStorage());

    private static UpdateProfileDto ValidUpdate(Guid? batchId = null) => new()
    {
        FullName = "Valid Name",
        Bio = "Valid bio",
        BatchId = batchId,
        MemberType = MemberType.CurrentStudent,
        CurrentStatus = "Studying"
    };

    [Fact]
    public async Task UpdateAsync_RejectsEmptyFullName()
    {
        using var db = TestDb.Create();
        var userId = Guid.NewGuid();
        TestData.Member(db, userId, "Original Name");
        var service = CreateService(db);

        var dto = ValidUpdate();
        dto.FullName = "   ";

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.UpdateAsync(userId, dto));

        Assert.Contains("Full name is required", ex.Message);
        Assert.Equal("Original Name", (await db.MemberProfiles.SingleAsync(p => p.UserId == userId)).FullName);
    }

    [Fact]
    public async Task UpdateAsync_RejectsBioLongerThanTheLimit()
    {
        using var db = TestDb.Create();
        var userId = Guid.NewGuid();
        TestData.Member(db, userId, "Original Name");
        var service = CreateService(db);

        var dto = ValidUpdate();
        dto.Bio = new string('x', ProfileService.MaxBioLength + 1);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.UpdateAsync(userId, dto));

        Assert.Contains("Bio must be", ex.Message);
        Assert.Null((await db.MemberProfiles.SingleAsync(p => p.UserId == userId)).Bio);
    }

    [Fact]
    public async Task UpdateAsync_RejectsUnknownBatch()
    {
        using var db = TestDb.Create();
        var userId = Guid.NewGuid();
        TestData.Member(db, userId);
        var service = CreateService(db);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.UpdateAsync(userId, ValidUpdate(Guid.NewGuid())));

        Assert.Contains("batch no longer exists", ex.Message);
    }

    [Fact]
    public async Task UpdateAsync_AssignsBatchAndPersistsNormalizedValues()
    {
        using var db = TestDb.Create();
        var userId = Guid.NewGuid();
        var batch = TestData.Batch(db, "CSE 12th", "2023-2024", 12);
        TestData.Member(db, userId, "Old Name");
        var service = CreateService(db);

        await service.UpdateAsync(userId, new UpdateProfileDto
        {
            FullName = "  New Name  ",
            Bio = "  Hello ACA  ",
            BatchId = batch.Id,
            MemberType = MemberType.Alumni,
            CurrentStatus = "  Working at Acme  "
        });

        var stored = await db.MemberProfiles.SingleAsync(p => p.UserId == userId);
        Assert.Equal("New Name", stored.FullName);
        Assert.Equal("Hello ACA", stored.Bio);
        Assert.Equal(batch.Id, stored.BatchId);
        Assert.Equal(MemberType.Alumni, stored.MemberType);
        Assert.Equal("Working at Acme", stored.CurrentStatus);

        var detail = await service.GetAsync(userId);
        Assert.NotNull(detail);
        Assert.Equal("CSE 12th", detail!.BatchName);
        Assert.Equal("2023-2024", detail.Session);
        Assert.Equal(MemberType.Alumni, detail.MemberType);
    }

    [Fact]
    public async Task UpdateAsync_SavesNewPhotoAndDeletesThePreviousOne()
    {
        using var db = TestDb.Create();
        var userId = Guid.NewGuid();
        var profile = TestData.Member(db, userId);
        profile.ProfilePhotoUrl = "/uploads/posts/old.png";
        await db.SaveChangesAsync();

        var storage = new FakeImageStorage { UrlFactory = _ => "/uploads/posts/new.png" };
        var service = CreateService(db, storage);

        var dto = ValidUpdate();
        dto.ProfilePhoto = new ImageUpload
        {
            FileName = "new.png",
            ContentType = "image/png",
            Length = 1,
            Content = new MemoryStream(new byte[] { 1 })
        };

        await service.UpdateAsync(userId, dto);

        Assert.Equal(new[] { "/uploads/posts/new.png" }, storage.SavedUrls);
        Assert.Contains("/uploads/posts/old.png", storage.DeletedUrls);
        Assert.Equal("/uploads/posts/new.png",
            (await db.MemberProfiles.SingleAsync(p => p.UserId == userId)).ProfilePhotoUrl);
    }

    [Fact]
    public async Task EnsureProfileAsync_IsIdempotentAndAssignsTheBatch()
    {
        using var db = TestDb.Create();
        var userId = Guid.NewGuid();
        var batch = TestData.Batch(db, "CSE 11th", "2022-2023", 11);
        var service = CreateService(db);

        await service.EnsureProfileAsync(userId, "  First Name  ", batch.Id, MemberType.CurrentStudent, "  Active  ");
        await service.EnsureProfileAsync(userId, "Second Name", null, MemberType.Alumni, null);

        var profiles = await db.MemberProfiles.Where(p => p.UserId == userId).ToListAsync();
        var profile = Assert.Single(profiles);
        Assert.Equal("First Name", profile.FullName);
        Assert.Equal(batch.Id, profile.BatchId);
        Assert.True(profile.IsApproved);
        Assert.Equal("Active", profile.CurrentStatus);
    }
}
