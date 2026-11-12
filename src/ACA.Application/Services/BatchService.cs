using ACA.Application.DTOs;
using ACA.Application.Interfaces;
using ACA.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ACA.Application.Services;

/// <summary>Batch catalogue management. Uniqueness of (BatchNo, Session) is enforced with friendly errors.</summary>
public class BatchService : IBatchService
{
    public const int MaxNameLength = 120;
    public const int MaxSessionLength = 40;

    private readonly IAppDbContext _db;

    public BatchService(IAppDbContext db)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
    }

    public async Task<IReadOnlyList<BatchDto>> GetAllAsync(CancellationToken ct = default)
    {
        return await _db.Batches
            .AsNoTracking()
            .OrderBy(b => b.BatchNo)
            .ThenBy(b => b.Session)
            .Select(b => new BatchDto
            {
                Id = b.Id,
                Name = b.Name,
                Session = b.Session,
                BatchNo = b.BatchNo,
                MemberCount = _db.MemberProfiles.Count(p => p.BatchId == b.Id && p.IsApproved)
            })
            .ToListAsync(ct);
    }

    public async Task<BatchDto> CreateAsync(string name, string session, int batchNo, CancellationToken ct = default)
    {
        var (validName, validSession) = Validate(name, session, batchNo);
        await EnsureUniqueAsync(validSession, batchNo, null, ct);

        var batch = new Batch
        {
            Name = validName,
            Session = validSession,
            BatchNo = batchNo,
            CreatedAtUtc = DateTime.UtcNow
        };

        _db.Batches.Add(batch);
        await _db.SaveChangesAsync(ct);

        return new BatchDto
        {
            Id = batch.Id,
            Name = batch.Name,
            Session = batch.Session,
            BatchNo = batch.BatchNo,
            MemberCount = 0
        };
    }

    public async Task UpdateAsync(Guid id, string name, string session, int batchNo, CancellationToken ct = default)
    {
        var batch = await _db.Batches.FirstOrDefaultAsync(b => b.Id == id, ct)
            ?? throw new InvalidOperationException("Batch not found.");

        var (validName, validSession) = Validate(name, session, batchNo);
        await EnsureUniqueAsync(validSession, batchNo, id, ct);

        batch.Name = validName;
        batch.Session = validSession;
        batch.BatchNo = batchNo;

        await _db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var batch = await _db.Batches.FirstOrDefaultAsync(b => b.Id == id, ct)
            ?? throw new InvalidOperationException("Batch not found.");

        _db.Batches.Remove(batch);

        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            throw new InvalidOperationException("This batch could not be deleted. Remove its members first and try again.");
        }
    }

    private static (string Name, string Session) Validate(string? name, string? session, int batchNo)
    {
        var validName = (name ?? string.Empty).Trim();
        if (validName.Length == 0) throw new InvalidOperationException("Batch name is required.");
        if (validName.Length > MaxNameLength)
        {
            throw new InvalidOperationException($"Batch name must be {MaxNameLength} characters or fewer.");
        }

        var validSession = (session ?? string.Empty).Trim();
        if (validSession.Length == 0) throw new InvalidOperationException("Session is required.");
        if (validSession.Length > MaxSessionLength)
        {
            throw new InvalidOperationException($"Session must be {MaxSessionLength} characters or fewer.");
        }

        if (batchNo <= 0) throw new InvalidOperationException("Batch number must be a positive number.");

        return (validName, validSession);
    }

    private async Task EnsureUniqueAsync(string session, int batchNo, Guid? excludeId, CancellationToken ct)
    {
        var sessionKey = session.ToLower();
        var duplicate = await _db.Batches.AnyAsync(
            b => b.BatchNo == batchNo
                 && b.Session.ToLower() == sessionKey
                 && (excludeId == null || b.Id != excludeId),
            ct);

        if (duplicate)
        {
            throw new InvalidOperationException(
                $"Batch {batchNo} already exists for session {session}. Choose a different batch number or session.");
        }
    }
}
