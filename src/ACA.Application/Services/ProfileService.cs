using ACA.Application.DTOs;
using ACA.Application.Interfaces;
using ACA.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ACA.Application.Services;

/// <summary>Reads and updates member profiles. Profile photos are delegated to <see cref="IImageStorage"/>.</summary>
public class ProfileService : IProfileService
{
    public const int MaxFullNameLength = 120;
    public const int MaxBioLength = 600;
    public const int MaxStatusLength = 120;

    private readonly IAppDbContext _db;
    private readonly IImageStorage _images;

    public ProfileService(IAppDbContext db, IImageStorage images)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _images = images ?? throw new ArgumentNullException(nameof(images));
    }

    public async Task<MemberDetailDto?> GetAsync(Guid userId, CancellationToken ct = default)
    {
        if (userId == Guid.Empty) return null;

        return await _db.MemberProfiles
            .AsNoTracking()
            .Where(p => p.UserId == userId)
            .Select(p => new MemberDetailDto
            {
                UserId = p.UserId,
                ProfileId = p.Id,
                FullName = p.FullName,
                Bio = p.Bio,
                ProfilePhotoUrl = p.ProfilePhotoUrl,
                BatchId = p.BatchId,
                BatchName = p.Batch != null ? p.Batch.Name : null,
                Session = p.Batch != null ? p.Batch.Session : null,
                MemberType = p.MemberType,
                CurrentStatus = p.CurrentStatus,
                IsApproved = p.IsApproved,
                JoinedAtUtc = p.CreatedAtUtc
            })
            .FirstOrDefaultAsync(ct);
    }

    public async Task EnsureProfileAsync(Guid userId, string fullName, Guid? batchId, Domain.Enums.MemberType memberType, string? currentStatus, CancellationToken ct = default)
    {
        if (userId == Guid.Empty) throw new ArgumentException("A valid user id is required.", nameof(userId));

        // Idempotent: never create a second profile for the same user.
        if (await _db.MemberProfiles.AnyAsync(p => p.UserId == userId, ct)) return;

        var name = NormalizeName(fullName);
        var status = NormalizeStatus(currentStatus);
        var batch = await ResolveBatchIdAsync(batchId, ct);

        _db.MemberProfiles.Add(new MemberProfile
        {
            UserId = userId,
            FullName = name,
            BatchId = batch,
            MemberType = Enum.IsDefined(memberType) ? memberType : Domain.Enums.MemberType.CurrentStudent,
            CurrentStatus = status,
            IsApproved = true,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        });

        await _db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(Guid userId, UpdateProfileDto dto, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        if (userId == Guid.Empty) throw new InvalidOperationException("Your profile could not be found.");

        var profile = await _db.MemberProfiles.FirstOrDefaultAsync(p => p.UserId == userId, ct)
            ?? throw new InvalidOperationException("Your profile could not be found.");

        var fullName = NormalizeName(dto.FullName);
        var bio = NormalizeBio(dto.Bio);
        var status = NormalizeStatus(dto.CurrentStatus);

        if (!Enum.IsDefined(dto.MemberType))
        {
            throw new InvalidOperationException("Select a valid member type.");
        }

        var batch = await ResolveBatchIdAsync(dto.BatchId, ct, throwWhenMissing: true);

        var previousPhotoUrl = profile.ProfilePhotoUrl;
        string? savedPhotoUrl = null;

        if (dto.ProfilePhoto is { Length: > 0 } upload)
        {
            savedPhotoUrl = await _images.SaveAsync(upload, ct);
            profile.ProfilePhotoUrl = savedPhotoUrl;
        }

        profile.FullName = fullName;
        profile.Bio = bio;
        profile.BatchId = batch;
        profile.MemberType = dto.MemberType;
        profile.CurrentStatus = status;
        profile.UpdatedAtUtc = DateTime.UtcNow;

        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch
        {
            // The database did not accept the update: do not leave an orphaned upload behind.
            if (savedPhotoUrl is not null) await TryDeleteImageAsync(savedPhotoUrl, ct);
            throw;
        }

        if (savedPhotoUrl is not null
            && !string.IsNullOrWhiteSpace(previousPhotoUrl)
            && !string.Equals(previousPhotoUrl, savedPhotoUrl, StringComparison.Ordinal))
        {
            await TryDeleteImageAsync(previousPhotoUrl, ct);
        }
    }

    private async Task<Guid?> ResolveBatchIdAsync(Guid? batchId, CancellationToken ct, bool throwWhenMissing = false)
    {
        if (batchId is null || batchId.Value == Guid.Empty) return null;

        var exists = await _db.Batches.AnyAsync(b => b.Id == batchId.Value, ct);
        if (!exists)
        {
            if (throwWhenMissing) throw new InvalidOperationException("The selected batch no longer exists.");
            return null;
        }

        return batchId.Value;
    }

    private static string NormalizeName(string? fullName)
    {
        var name = (fullName ?? string.Empty).Trim();
        if (name.Length == 0) throw new InvalidOperationException("Full name is required.");
        if (name.Length > MaxFullNameLength)
        {
            throw new InvalidOperationException($"Full name must be {MaxFullNameLength} characters or fewer.");
        }

        return name;
    }

    private static string? NormalizeBio(string? bio)
    {
        if (string.IsNullOrWhiteSpace(bio)) return null;
        var value = bio.Trim();
        if (value.Length > MaxBioLength)
        {
            throw new InvalidOperationException($"Bio must be {MaxBioLength} characters or fewer.");
        }

        return value;
    }

    private static string? NormalizeStatus(string? status)
    {
        if (string.IsNullOrWhiteSpace(status)) return null;
        var value = status.Trim();
        if (value.Length > MaxStatusLength)
        {
            throw new InvalidOperationException($"Current status must be {MaxStatusLength} characters or fewer.");
        }

        return value;
    }

    private async Task TryDeleteImageAsync(string url, CancellationToken ct)
    {
        try
        {
            await _images.DeleteAsync(url, ct);
        }
        catch
        {
            // A stale file must not fail the profile update; the storage layer reports its own problems.
        }
    }
}
