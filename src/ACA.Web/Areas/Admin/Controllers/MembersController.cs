using ACA.Application.DTOs.Common;
using ACA.Application.Interfaces;
using ACA.Domain.Entities;
using ACA.Domain.Enums;
using ACA.Infrastructure.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ACA.Web.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = "Admin,Moderator")]
public class MembersController : Controller
{
    private const int PageSize = 20;
    private const int MaxSearchLength = 120;

    private readonly IAppDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;

    public MembersController(IAppDbContext db, UserManager<ApplicationUser> userManager)
    {
        _db = db;
        _userManager = userManager;
    }

    public async Task<IActionResult> Index(string? q, int page = 1, CancellationToken ct = default)
    {
        if (page < 1) page = 1;
        var term = (q ?? string.Empty).Trim();
        if (term.Length > MaxSearchLength) term = term[..MaxSearchLength];

        IQueryable<MemberProfile> query = _db.MemberProfiles.AsNoTracking();

        if (term.Length > 0)
        {
            var lowered = term.ToLowerInvariant();
            var matchingUserIds = await _userManager.Users
                .Where(u => (u.Email != null && u.Email.ToLower().Contains(lowered))
                            || (u.UserName != null && u.UserName.ToLower().Contains(lowered)))
                .Select(u => u.Id)
                .ToListAsync(ct);

            query = query.Where(p => p.FullName.ToLower().Contains(lowered)
                                     || matchingUserIds.Contains(p.UserId));
        }

        var totalCount = await query.CountAsync(ct);

        var profiles = await query
            .Include(p => p.Batch)
            .OrderBy(p => p.IsApproved)
            .ThenBy(p => p.FullName)
            .Skip((page - 1) * PageSize)
            .Take(PageSize)
            .ToListAsync(ct);

        var userIds = profiles.Select(p => p.UserId).ToList();
        var users = await _userManager.Users
            .Where(u => userIds.Contains(u.Id))
            .Select(u => new { u.Id, u.Email, u.UserName })
            .ToListAsync(ct);
        var usersById = users.ToDictionary(u => u.Id);

        var rows = profiles.Select(p =>
        {
            usersById.TryGetValue(p.UserId, out var user);
            return new AdminMemberRow
            {
                ProfileId = p.Id,
                UserId = p.UserId,
                FullName = p.FullName,
                Email = user?.Email ?? user?.UserName,
                ProfilePhotoUrl = p.ProfilePhotoUrl,
                BatchName = p.Batch?.Name,
                MemberType = p.MemberType,
                CurrentStatus = p.CurrentStatus,
                IsApproved = p.IsApproved,
                JoinedAtUtc = p.CreatedAtUtc
            };
        }).ToList();

        return View(PagedResult<AdminMemberRow>.Create(rows, page, PageSize, totalCount));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> ToggleApproval(Guid id, string? q, int page = 1, CancellationToken ct = default)
    {
        var profile = await _db.MemberProfiles.FirstOrDefaultAsync(p => p.Id == id, ct);
        if (profile is null)
        {
            TempData["Error"] = "That member could not be found.";
            return RedirectToAction(nameof(Index), new { q, page });
        }

        profile.IsApproved = !profile.IsApproved;
        profile.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        TempData["Success"] = profile.IsApproved
            ? $"{profile.FullName} has been approved."
            : $"{profile.FullName}'s approval was revoked.";

        return RedirectToAction(nameof(Index), new { q, page });
    }
}

public sealed class AdminMemberRow
{
    public Guid ProfileId { get; init; }
    public Guid UserId { get; init; }
    public string FullName { get; init; } = string.Empty;
    public string? Email { get; init; }
    public string? ProfilePhotoUrl { get; init; }
    public string? BatchName { get; init; }
    public MemberType MemberType { get; init; }
    public string? CurrentStatus { get; init; }
    public bool IsApproved { get; init; }
    public DateTime JoinedAtUtc { get; init; }
}
