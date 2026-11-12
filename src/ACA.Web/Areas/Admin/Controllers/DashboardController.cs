using ACA.Application.Interfaces;
using ACA.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ACA.Web.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = "Admin,Moderator")]
public class DashboardController : Controller
{
    private readonly IAppDbContext _db;

    public DashboardController(IAppDbContext db)
    {
        _db = db;
    }

    public async Task<IActionResult> Index(CancellationToken ct)
    {
        ViewBag.MemberCount = await _db.MemberProfiles.CountAsync(ct);
        ViewBag.PendingApprovalCount = await _db.MemberProfiles.CountAsync(p => !p.IsApproved, ct);
        ViewBag.PostCount = await _db.Posts.CountAsync(p => !p.IsDeleted, ct);
        ViewBag.PendingReportCount = await _db.Reports.CountAsync(r => r.Status == ReportStatus.Pending, ct);

        return View();
    }
}
