using System.Security.Claims;
using ACA.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ACA.Web.Controllers;

[Authorize]
public class NotificationsController : Controller
{
    private const int PageSize = 20;

    private readonly INotificationService _notifications;

    public NotificationsController(INotificationService notifications)
    {
        _notifications = notifications;
    }

    [HttpGet]
    public async Task<IActionResult> Index(int page = 1, CancellationToken ct = default)
    {
        var userId = CurrentUserId();
        if (userId is null)
        {
            return Challenge();
        }

        var result = await _notifications.GetForUserAsync(userId.Value, page < 1 ? 1 : page, PageSize, ct);
        return View(result);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkRead(Guid id, int page = 1, CancellationToken ct = default)
    {
        var userId = CurrentUserId();
        if (userId is null)
        {
            return Challenge();
        }

        try
        {
            await _notifications.MarkReadAsync(id, userId.Value, ct);
        }
        catch (UnauthorizedAccessException ex)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToAction(nameof(Index), new { page = page < 1 ? 1 : page });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkAllRead(int page = 1, CancellationToken ct = default)
    {
        var userId = CurrentUserId();
        if (userId is null)
        {
            return Challenge();
        }

        await _notifications.MarkAllReadAsync(userId.Value, ct);
        TempData["Success"] = "All notifications marked as read.";
        return RedirectToAction(nameof(Index), new { page = page < 1 ? 1 : page });
    }

    [HttpGet]
    public async Task<IActionResult> UnreadCount(CancellationToken ct = default)
    {
        var userId = CurrentUserId();
        if (userId is null)
        {
            return Json(new { count = 0 });
        }

        var count = await _notifications.GetUnreadCountAsync(userId.Value, ct);
        return Json(new { count });
    }

    private Guid? CurrentUserId()
        => Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;
}
