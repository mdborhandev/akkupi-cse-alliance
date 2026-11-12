using System.Security.Claims;
using ACA.Application.DTOs;
using ACA.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ACA.Web.Controllers;

public class PostsController : Controller
{
    private const int FeedPageSize = 10;
    private const int MaxImagesPerPost = 4;
    private const int MaxContentLength = 5000;

    private readonly IPostService _posts;

    public PostsController(IPostService posts)
    {
        _posts = posts;
    }

    private Guid? CurrentUserId
        => Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;

    /// <summary>Public, newest-first feed. Optionally filtered by the author's batch.</summary>
    [HttpGet]
    public async Task<IActionResult> Index(
        [FromQuery] int page = 1,
        [FromQuery] Guid? batchId = null,
        CancellationToken ct = default)
    {
        var feed = await _posts.GetFeedAsync(page, FeedPageSize, batchId, CurrentUserId, ct);
        ViewData["BatchId"] = batchId;
        return View(feed);
    }

    [HttpGet]
    public async Task<IActionResult> Details(Guid id, CancellationToken ct = default)
    {
        var post = await _posts.GetByIdAsync(id, CurrentUserId, ct);
        if (post is null)
        {
            return NotFound();
        }

        return View(post);
    }

    [Authorize]
    [HttpGet]
    public IActionResult Create()
    {
        return View(new CreatePostDto());
    }

    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(
        string? content,
        IFormFileCollection? images,
        CancellationToken ct = default)
    {
        if (!TryValidateContent(content, out _))
        {
            return View(new CreatePostDto { Content = content ?? string.Empty });
        }

        if (CurrentUserId is not Guid authorId)
        {
            return Challenge();
        }

        var files = images is null
            ? new List<IFormFile>()
            : images.Where(f => f is { Length: > 0 }).ToList();

        if (files.Count > MaxImagesPerPost)
        {
            ModelState.AddModelError(string.Empty, $"A post can include at most {MaxImagesPerPost} images.");
            return View(new CreatePostDto { Content = content ?? string.Empty });
        }

        var streams = new List<Stream>(files.Count);
        try
        {
            var uploads = new List<ImageUpload>(files.Count);
            foreach (var file in files)
            {
                var stream = file.OpenReadStream();
                streams.Add(stream);
                uploads.Add(new ImageUpload
                {
                    Content = stream,
                    FileName = file.FileName,
                    ContentType = file.ContentType ?? string.Empty,
                    Length = file.Length
                });
            }

            var postId = await _posts.CreateAsync(
                new CreatePostDto { Content = content ?? string.Empty, Images = uploads },
                authorId,
                ct);

            TempData["Success"] = "Your post was published.";
            return RedirectToAction(nameof(Details), new { id = postId });
        }
        catch (InvalidOperationException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            return View(new CreatePostDto { Content = content ?? string.Empty });
        }
        finally
        {
            foreach (var stream in streams)
            {
                await stream.DisposeAsync();
            }
        }
    }

    [Authorize]
    [HttpGet]
    public async Task<IActionResult> Edit(Guid id, CancellationToken ct = default)
    {
        var post = await _posts.GetByIdAsync(id, CurrentUserId, ct);
        if (post is null)
        {
            return NotFound();
        }

        if (!CanManage(post.AuthorId))
        {
            return Forbid();
        }

        return View(post);
    }

    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(Guid id, string? content, CancellationToken ct = default)
    {
        if (CurrentUserId is not Guid userId)
        {
            return Challenge();
        }

        var existing = await _posts.GetByIdAsync(id, userId, ct);
        if (existing is null)
        {
            return NotFound();
        }

        if (!CanManage(existing.AuthorId))
        {
            return Forbid();
        }

        if (!TryValidateContent(content, out _))
        {
            return View(existing);
        }

        try
        {
            await _posts.UpdateAsync(
                id,
                new UpdatePostDto { Content = content ?? string.Empty },
                userId,
                User.IsInRole("Admin"),
                ct);

            TempData["Success"] = "Post updated.";
            return RedirectToAction(nameof(Details), new { id });
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (InvalidOperationException ex)
        {
            ModelState.AddModelError("Content", ex.Message);
            var post = await _posts.GetByIdAsync(id, CurrentUserId, ct);
            return post is null ? NotFound() : View(post);
        }
    }

    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct = default)
    {
        if (CurrentUserId is not Guid userId)
        {
            return Challenge();
        }

        try
        {
            await _posts.DeleteAsync(id, userId, User.IsInRole("Admin"), ct);
            TempData["Success"] = "Post deleted.";
            return RedirectToAction(nameof(Index));
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }

    private bool CanManage(Guid authorId)
        => CurrentUserId == authorId || User.IsInRole("Admin");

    private bool TryValidateContent(string? content, out string normalized)
    {
        normalized = (content ?? string.Empty).Trim();
        if (normalized.Length == 0)
        {
            ModelState.AddModelError("Content", "Write something before posting.");
        }
        else if (normalized.Length > MaxContentLength)
        {
            ModelState.AddModelError("Content", $"Post content cannot exceed {MaxContentLength} characters.");
        }

        return ModelState.IsValid;
    }
}
