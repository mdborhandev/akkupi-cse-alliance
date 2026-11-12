using ACA.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ACA.Application.Interfaces;

/// <summary>Persistence surface exposed to application services (implemented by ACA.Infrastructure).</summary>
public interface IAppDbContext
{
    DbSet<Post> Posts { get; }
    DbSet<PostImage> PostImages { get; }
    DbSet<Comment> Comments { get; }
    DbSet<Reaction> Reactions { get; }
    DbSet<Notification> Notifications { get; }
    DbSet<Report> Reports { get; }
    DbSet<Batch> Batches { get; }
    DbSet<MemberProfile> MemberProfiles { get; }
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
