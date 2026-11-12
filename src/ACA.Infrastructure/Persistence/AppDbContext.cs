using ACA.Application.Interfaces;
using ACA.Domain.Entities;
using ACA.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace ACA.Infrastructure.Persistence;

public class AppDbContext : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>, IAppDbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<Batch> Batches => Set<Batch>();
    public DbSet<MemberProfile> MemberProfiles => Set<MemberProfile>();
    public DbSet<Post> Posts => Set<Post>();
    public DbSet<PostImage> PostImages => Set<PostImage>();
    public DbSet<Comment> Comments => Set<Comment>();
    public DbSet<Reaction> Reactions => Set<Reaction>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<Report> Reports => Set<Report>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<Batch>(e =>
        {
            e.HasIndex(b => new { b.BatchNo, b.Session }).IsUnique();
            e.Property(b => b.Name).HasMaxLength(120).IsRequired();
            e.Property(b => b.Session).HasMaxLength(40).IsRequired();
        });

        builder.Entity<MemberProfile>(e =>
        {
            e.HasIndex(p => p.UserId).IsUnique();
            e.HasIndex(p => p.BatchId);
            e.HasIndex(p => p.MemberType);
            e.HasIndex(p => p.FullName);
            e.Property(p => p.FullName).HasMaxLength(120).IsRequired();
            e.Property(p => p.Bio).HasMaxLength(600);
            e.Property(p => p.CurrentStatus).HasMaxLength(120);
            e.Property(p => p.ProfilePhotoUrl).HasMaxLength(500);
            e.HasOne<ApplicationUser>()
                .WithOne()
                .HasForeignKey<MemberProfile>(p => p.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(p => p.Batch)
                .WithMany()
                .HasForeignKey(p => p.BatchId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<Post>(e =>
        {
            e.HasIndex(p => p.CreatedAtUtc);
            e.HasIndex(p => p.AuthorId);
            e.Property(p => p.Content).HasMaxLength(5000).IsRequired();
            e.HasOne<ApplicationUser>()
                .WithMany()
                .HasForeignKey(p => p.AuthorId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<PostImage>(e =>
        {
            e.HasIndex(i => new { i.PostId, i.SortOrder });
            e.Property(i => i.Url).HasMaxLength(500).IsRequired();
            e.HasOne(i => i.Post).WithMany(p => p.Images).HasForeignKey(i => i.PostId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<Comment>(e =>
        {
            e.HasIndex(c => new { c.PostId, c.CreatedAtUtc });
            e.HasIndex(c => c.ParentCommentId);
            e.Property(c => c.Content).HasMaxLength(2000).IsRequired();
            e.HasOne(c => c.Post).WithMany(p => p.Comments).HasForeignKey(c => c.PostId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<ApplicationUser>().WithMany().HasForeignKey(c => c.AuthorId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(c => c.ParentComment).WithMany().HasForeignKey(c => c.ParentCommentId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<Reaction>(e =>
        {
            e.HasIndex(r => new { r.PostId, r.UserId }).IsUnique();
            e.HasOne(r => r.Post).WithMany(p => p.Reactions).HasForeignKey(r => r.PostId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<ApplicationUser>().WithMany().HasForeignKey(r => r.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<Notification>(e =>
        {
            e.HasIndex(n => new { n.RecipientId, n.IsRead, n.CreatedAtUtc });
            e.HasOne<ApplicationUser>().WithMany().HasForeignKey(n => n.RecipientId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<Report>(e =>
        {
            e.HasIndex(r => new { r.Status, r.CreatedAtUtc });
            e.Property(r => r.Reason).HasMaxLength(1000).IsRequired();
            e.HasOne<ApplicationUser>().WithMany().HasForeignKey(r => r.ReporterId).OnDelete(DeleteBehavior.Cascade);
        });
    }
}
