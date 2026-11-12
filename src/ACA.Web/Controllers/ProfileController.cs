using System.Security.Claims;
using ACA.Application.DTOs;
using ACA.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ACA.Web.Controllers;

[Authorize]
public class ProfileController : Controller
{
    private const int MaxFullNameLength = 120;
    private const int MaxBioLength = 600;
    private const int MaxStatusLength = 120;

    private readonly IProfileService _profileService;
    private readonly IBatchService _batchService;

    public ProfileController(IProfileService profileService, IBatchService batchService)
    {
        _profileService = profileService;
        _batchService = batchService;
    }

    private Guid CurrentUserId
    {
        get
        {
            var raw = User.FindFirstValue(ClaimTypes.NameIdentifier);
            return Guid.TryParse(raw, out var id) ? id : Guid.Empty;
        }
    }

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var profile = await _profileService.GetAsync(CurrentUserId, ct);
        if (profile is null) return NotFound();

        return View(profile);
    }

    [HttpGet]
    public async Task<IActionResult> Edit(CancellationToken ct)
    {
        var profile = await _profileService.GetAsync(CurrentUserId, ct);
        if (profile is null) return NotFound();

        var model = new UpdateProfileDto
        {
            FullName = profile.FullName,
            Bio = profile.Bio,
            BatchId = profile.BatchId,
            MemberType = profile.MemberType,
            CurrentStatus = profile.CurrentStatus
        };

        await PopulateViewDataAsync(profile.ProfilePhotoUrl, ct);
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(UpdateProfileDto model, IFormFile? photo, CancellationToken ct)
    {
        Validate(model);

        if (!ModelState.IsValid)
        {
            await PopulateViewDataAsync(null, ct);
            return View(model);
        }

        try
        {
            model.ProfilePhoto = ToImageUpload(photo);
            await _profileService.UpdateAsync(CurrentUserId, model, ct);
            TempData["Success"] = "Your profile has been updated.";
            return RedirectToAction(nameof(Index));
        }
        catch (InvalidOperationException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
        }
        catch (ArgumentException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
        }

        await PopulateViewDataAsync(null, ct);
        return View(model);
    }

    private void Validate(UpdateProfileDto model)
    {
        if (string.IsNullOrWhiteSpace(model.FullName))
        {
            ModelState.AddModelError(nameof(model.FullName), "Full name is required.");
        }
        else if (model.FullName.Trim().Length > MaxFullNameLength)
        {
            ModelState.AddModelError(nameof(model.FullName), $"Full name must be {MaxFullNameLength} characters or fewer.");
        }

        if (!string.IsNullOrWhiteSpace(model.Bio) && model.Bio.Trim().Length > MaxBioLength)
        {
            ModelState.AddModelError(nameof(model.Bio), $"Bio must be {MaxBioLength} characters or fewer.");
        }

        if (!string.IsNullOrWhiteSpace(model.CurrentStatus) && model.CurrentStatus.Trim().Length > MaxStatusLength)
        {
            ModelState.AddModelError(nameof(model.CurrentStatus), $"Current status must be {MaxStatusLength} characters or fewer.");
        }

        if (!Enum.IsDefined(model.MemberType))
        {
            ModelState.AddModelError(nameof(model.MemberType), "Select a valid member type.");
        }
    }

    private async Task PopulateViewDataAsync(string? currentPhotoUrl, CancellationToken ct)
    {
        ViewData["Batches"] = await _batchService.GetAllAsync(ct);

        if (currentPhotoUrl is null)
        {
            var profile = await _profileService.GetAsync(CurrentUserId, ct);
            currentPhotoUrl = profile?.ProfilePhotoUrl;
        }

        ViewData["CurrentPhotoUrl"] = currentPhotoUrl;
    }

    private static ImageUpload? ToImageUpload(IFormFile? photo)
    {
        if (photo is null || photo.Length <= 0) return null;

        return new ImageUpload
        {
            Content = photo.OpenReadStream(),
            FileName = photo.FileName,
            ContentType = photo.ContentType ?? string.Empty,
            Length = photo.Length
        };
    }
}
