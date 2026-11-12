using System.Security.Claims;
using ACA.Application.DTOs;
using ACA.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ACA.Web.Controllers;

/// <summary>Member-facing reporting form; moderators resolve reports in the Admin area.</summary>
[Authorize]
public class ReportsController : Controller
{
    private readonly IReportService _reports;

    public ReportsController(IReportService reports)
    {
        _reports = reports ?? throw new ArgumentNullException(nameof(reports));
    }

    [HttpGet]
    public IActionResult Create(Guid? postId, Guid? commentId, Guid? userId, string? returnUrl)
    {
        var targets = new[] { postId.HasValue, commentId.HasValue, userId.HasValue }.Count(x => x);
        if (targets != 1)
        {
            return BadRequest();
        }

        ViewData["ReturnUrl"] = SafeReturnUrl(returnUrl);
        return View(new CreateReportDto
        {
            PostId = postId,
            CommentId = commentId,
            TargetUserId = userId
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateReportDto model, string? returnUrl, CancellationToken ct)
    {
        ViewData["ReturnUrl"] = SafeReturnUrl(returnUrl);

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var rawUserId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(rawUserId, out var reporterId))
        {
            return Challenge();
        }

        try
        {
            await _reports.CreateAsync(model, reporterId, ct);
        }
        catch (ArgumentException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            return View(model);
        }

        TempData["Success"] = "Thanks — a moderator will review this report.";
        return LocalRedirect(SafeReturnUrl(returnUrl));
    }

    private string SafeReturnUrl(string? returnUrl) =>
        !string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl) ? returnUrl : "/Posts";
}
