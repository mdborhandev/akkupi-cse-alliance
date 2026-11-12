using ACA.Application.Interfaces;
using ACA.Domain.Enums;
using ACA.Infrastructure.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace ACA.Web.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = "Admin,Moderator")]
public class ReportsController : Controller
{
    private const int PageSize = 20;

    private readonly IReportService _reports;
    private readonly UserManager<ApplicationUser> _userManager;

    public ReportsController(IReportService reports, UserManager<ApplicationUser> userManager)
    {
        _reports = reports;
        _userManager = userManager;
    }

    public async Task<IActionResult> Index(int page = 1, CancellationToken ct = default)
    {
        if (page < 1) page = 1;
        return View(await _reports.GetPendingAsync(page, PageSize, ct));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Resolve(
        Guid id,
        ReportStatus status,
        bool removeContent,
        int page = 1,
        CancellationToken ct = default)
    {
        if (page < 1) page = 1;

        if (status is not (ReportStatus.Reviewed or ReportStatus.Dismissed))
        {
            TempData["Error"] = "Choose a valid resolution status (Reviewed or Dismissed).";
            return RedirectToAction(nameof(Index), new { page });
        }

        var reviewerId = _userManager.GetUserId(User);
        if (!Guid.TryParse(reviewerId, out var reviewer))
        {
            TempData["Error"] = "Could not identify the signed-in moderator. Please sign in again.";
            return RedirectToAction(nameof(Index), new { page });
        }

        try
        {
            await _reports.ResolveAsync(id, status, reviewer, removeContent, ct);
            TempData["Success"] = removeContent
                ? "Report resolved and the reported content was removed."
                : "Report resolved.";
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToAction(nameof(Index), new { page });
    }
}
