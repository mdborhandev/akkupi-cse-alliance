using System.ComponentModel.DataAnnotations;
using ACA.Application.DTOs;
using ACA.Application.Interfaces;
using ACA.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace ACA.Web.Controllers;

public class AccountController : Controller
{
    private const int MaxFullNameLength = 120;
    private const int MaxStatusLength = 120;
    private const int MinPasswordLength = 8;

    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly IProfileService _profileService;
    private readonly IBatchService _batchService;
    private readonly ILogger<AccountController> _logger;

    public AccountController(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        IProfileService profileService,
        IBatchService batchService,
        ILogger<AccountController> logger)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _profileService = profileService;
        _batchService = batchService;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Register(CancellationToken ct)
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToAction("Index", "Posts");
        }

        await PopulateBatchesAsync(ct);
        return View(new RegisterDto());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterDto model, CancellationToken ct)
    {
        ValidateRegistration(model);

        if (!ModelState.IsValid)
        {
            await PopulateBatchesAsync(ct);
            return View(model);
        }

        var email = model.Email.Trim();
        var fullName = model.FullName.Trim();

        var existing = await _userManager.FindByEmailAsync(email);
        if (existing is not null)
        {
            ModelState.AddModelError(nameof(model.Email), "An account with this email already exists.");
            await PopulateBatchesAsync(ct);
            return View(model);
        }

        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            CreatedAtUtc = DateTime.UtcNow
        };

        var createResult = await _userManager.CreateAsync(user, model.Password);
        if (!createResult.Succeeded)
        {
            foreach (var error in createResult.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }

            await PopulateBatchesAsync(ct);
            return View(model);
        }

        try
        {
            await _profileService.EnsureProfileAsync(user.Id, fullName, model.BatchId, model.MemberType, model.CurrentStatus, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Could not create a member profile for {UserId}; rolling back the account.", user.Id);
            await _userManager.DeleteAsync(user);
            ModelState.AddModelError(string.Empty, "We could not finish creating your profile. Please try again.");
            await PopulateBatchesAsync(ct);
            return View(model);
        }

        // Best effort: the "Member" role is seeded at startup, but registration must not fail if it is missing.
        try
        {
            var roleResult = await _userManager.AddToRoleAsync(user, "Member");
            if (!roleResult.Succeeded)
            {
                _logger.LogWarning("Could not add {UserId} to the Member role: {Errors}",
                    user.Id, string.Join("; ", roleResult.Errors.Select(e => e.Description)));
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not add {UserId} to the Member role.", user.Id);
        }

        await _signInManager.SignInAsync(user, isPersistent: false);
        TempData["Success"] = $"Welcome to Akkupi CSE Alliance, {fullName}!";
        return RedirectToAction("Index", "Posts");
    }

    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToAction("Index", "Posts");
        }

        ViewData["ReturnUrl"] = returnUrl;
        return View(new LoginViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl = null)
    {
        ViewData["ReturnUrl"] = returnUrl;

        if (!ModelState.IsValid) return View(model);

        var email = model.Email.Trim();
        var result = await _signInManager.PasswordSignInAsync(email, model.Password, model.RememberMe, lockoutOnFailure: true);

        if (result.Succeeded)
        {
            return RedirectAfterLogin(returnUrl);
        }

        if (result.IsLockedOut)
        {
            ModelState.AddModelError(string.Empty, "This account is temporarily locked because of repeated failed attempts. Try again later.");
            return View(model);
        }

        if (result.RequiresTwoFactor)
        {
            ModelState.AddModelError(string.Empty, "Two-factor authentication is not available on this site yet.");
            return View(model);
        }

        ModelState.AddModelError(string.Empty, "Invalid email or password.");
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await _signInManager.SignOutAsync();
        TempData["Success"] = "You have been logged out.";
        return RedirectToAction("Index", "Home");
    }

    [HttpGet]
    public IActionResult AccessDenied()
    {
        Response.StatusCode = StatusCodes.Status403Forbidden;
        return View();
    }

    private IActionResult RedirectAfterLogin(string? returnUrl)
    {
        if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            return LocalRedirect(returnUrl);
        }

        return RedirectToAction("Index", "Posts");
    }

    private void ValidateRegistration(RegisterDto model)
    {
        if (string.IsNullOrWhiteSpace(model.FullName))
        {
            ModelState.AddModelError(nameof(model.FullName), "Full name is required.");
        }
        else if (model.FullName.Trim().Length > MaxFullNameLength)
        {
            ModelState.AddModelError(nameof(model.FullName), $"Full name must be {MaxFullNameLength} characters or fewer.");
        }

        if (string.IsNullOrWhiteSpace(model.Email))
        {
            ModelState.AddModelError(nameof(model.Email), "Email is required.");
        }
        else if (!new EmailAddressAttribute().IsValid(model.Email.Trim()))
        {
            ModelState.AddModelError(nameof(model.Email), "Enter a valid email address.");
        }

        if (string.IsNullOrEmpty(model.Password))
        {
            ModelState.AddModelError(nameof(model.Password), "Password is required.");
        }
        else if (model.Password.Length < MinPasswordLength)
        {
            ModelState.AddModelError(nameof(model.Password), $"Password must be at least {MinPasswordLength} characters long.");
        }

        if (model.CurrentStatus is { Length: > MaxStatusLength })
        {
            ModelState.AddModelError(nameof(model.CurrentStatus), $"Current status must be {MaxStatusLength} characters or fewer.");
        }

        if (!Enum.IsDefined(model.MemberType))
        {
            ModelState.AddModelError(nameof(model.MemberType), "Select a valid member type.");
        }
    }

    private async Task PopulateBatchesAsync(CancellationToken ct)
    {
        ViewData["Batches"] = await _batchService.GetAllAsync(ct);
    }

    /// <summary>Form model for the login page (kept with its controller; the shared DTOs are frozen).</summary>
    public sealed class LoginViewModel
    {
        [Required(ErrorMessage = "Email is required.")]
        [EmailAddress(ErrorMessage = "Enter a valid email address.")]
        [StringLength(256, ErrorMessage = "Email is too long.")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Password is required.")]
        [DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;

        [Display(Name = "Remember me")]
        public bool RememberMe { get; set; }
    }
}
