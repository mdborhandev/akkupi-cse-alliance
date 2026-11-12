using ACA.Application.Interfaces;
using ACA.Domain.Entities;
using ACA.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace ACA.Application.Services;

public class ReactionService : IReactionService
{
    private readonly IAppDbContext _db;
    private readonly INotificationService _notifications;

    public ReactionService(IAppDbContext db, INotificationService notifications)
    {
        _db = db;
        _notifications = notifications;
    }

    public async Task ToggleAsync(Guid postId, Guid userId, ReactionType type, CancellationToken ct = default)
    {
        if (!Enum.IsDefined(type))
        {
            throw new InvalidOperationException("Unknown reaction type.");
        }

        var post = await _db.Posts.AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == postId && !p.IsDeleted, ct);

        if (post is null)
        {
            throw new InvalidOperationException("This post no longer exists.");
        }

        var existing = await _db.Reactions
            .FirstOrDefaultAsync(r => r.PostId == postId && r.UserId == userId, ct);

        if (existing is not null)
        {
            if (existing.Type == type)
            {
                // Same reaction again: toggle it off.
                _db.Reactions.Remove(existing);
            }
            else
            {
                existing.Type = type;
            }

            await _db.SaveChangesAsync(ct);
            return;
        }

        _db.Reactions.Add(new Reaction
        {
            PostId = postId,
            UserId = userId,
            Type = type,
            CreatedAtUtc = DateTime.UtcNow
        });

        await _db.SaveChangesAsync(ct);

        // Only notify on a brand new reaction (not when switching type or toggling off).
        await _notifications.NotifyAsync(post.AuthorId, userId, NotificationType.Reaction, postId, null, ct);
    }

    public async Task RemoveAsync(Guid postId, Guid userId, CancellationToken ct = default)
    {
        var existing = await _db.Reactions
            .FirstOrDefaultAsync(r => r.PostId == postId && r.UserId == userId, ct);

        if (existing is null)
        {
            return;
        }

        _db.Reactions.Remove(existing);
        await _db.SaveChangesAsync(ct);
    }
}
