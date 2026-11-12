using System.Security.Claims;
using ACA.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ACA.Web.Controllers;

[Authorize]
public class CommentsController : Controller
{
    private const int MaxContentLength = 2000;

    private readonly ICommentService _comments;

    public CommentsController(ICommentService comments)
    {
        _comments = comments;
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Add(Guid postId, string? content, Guid? parentCommentId, CancellationToken ct)
    {
        var userId = CurrentUserId();
        if (userId is null)
        {
            return Challenge();
        }

        if (postId == Guid.Empty)
        {
            TempData["Error"] = "This post no longer exists.";
            return RedirectToAction("Index", "Posts");
        }

        var text = (content ?? string.Empty).Trim();
        if (text.Length == 0)
        {
            TempData["Error"] = "Comment cannot be empty.";
            return RedirectToPost(postId);
        }

        if (text.Length > MaxContentLength)
        {
            TempData["Error"] = $"Comment must be {MaxContentLength} characters or fewer.";
            return RedirectToPost(postId);
        }

        try
        {
            await _comments.AddAsync(postId, userId.Value, text, parentCommentId, ct);
            TempData["Success"] = parentCommentId.HasValue ? "Reply posted." : "Comment posted.";
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ex.Message;
        }
        catch (UnauthorizedAccessException ex)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToPost(postId);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(Guid id, string? returnUrl, CancellationToken ct)
    {
        var userId = CurrentUserId();
        if (userId is null)
        {
            return Challenge();
        }

        try
        {
            await _comments.DeleteAsync(id, userId.Value, User.IsInRole("Admin"), ct);
            TempData["Success"] = "Comment deleted.";
        }
        catch (UnauthorizedAccessException ex)
        {
            TempData["Error"] = ex.Message;
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ex.Message;
        }

        return SafeRedirect(returnUrl);
    }

    private Guid? CurrentUserId()
        => Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;

    private IActionResult RedirectToPost(Guid postId)
        => RedirectToAction("Details", "Posts", new { id = postId });

    private IActionResult SafeRedirect(string? returnUrl)
        => Url.IsLocalUrl(returnUrl) ? Redirect(returnUrl!) : RedirectToAction("Index", "Posts");
}
