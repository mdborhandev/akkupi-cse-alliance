using System.Security.Claims;
using ACA.Application.Interfaces;
using ACA.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ACA.Web.Controllers;

[Authorize]
public class ReactionsController : Controller
{
    private readonly IReactionService _reactions;

    public ReactionsController(IReactionService reactions)
    {
        _reactions = reactions;
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Toggle(Guid postId, ReactionType type, string? returnUrl, CancellationToken ct)
    {
        var userId = CurrentUserId();
        if (userId is null)
        {
            return Challenge();
        }

        if (!Enum.IsDefined(type))
        {
            TempData["Error"] = "Unknown reaction type.";
            return Back(returnUrl);
        }

        try
        {
            await _reactions.ToggleAsync(postId, userId.Value, type, ct);
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ex.Message;
        }
        catch (UnauthorizedAccessException ex)
        {
            TempData["Error"] = ex.Message;
        }

        return Back(returnUrl);
    }

    private Guid? CurrentUserId()
        => Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;

    private IActionResult Back(string? returnUrl)
    {
        if (Url.IsLocalUrl(returnUrl))
        {
            return Redirect(returnUrl!);
        }

        var referer = Request.Headers.Referer.ToString();
        if (Uri.TryCreate(referer, UriKind.Absolute, out var uri) && Url.IsLocalUrl(uri.PathAndQuery))
        {
            return Redirect(uri.PathAndQuery);
        }

        return RedirectToAction("Index", "Posts");
    }
}
