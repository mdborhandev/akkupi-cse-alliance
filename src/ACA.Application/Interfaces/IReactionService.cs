using ACA.Domain.Enums;

namespace ACA.Application.Interfaces;

public interface IReactionService
{
    /// <summary>Adds, replaces, or removes the member's reaction (same type toggles off).</summary>
    Task ToggleAsync(Guid postId, Guid userId, ReactionType type, CancellationToken ct = default);
    Task RemoveAsync(Guid postId, Guid userId, CancellationToken ct = default);
}
