using ACA.Application.DTOs;
using ACA.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ACA.Web.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = "Admin,Moderator")]
public class BatchesController : Controller
{
    private const int MaxNameLength = 120;
    private const int MaxSessionLength = 40;

    private readonly IBatchService _batches;
    private readonly IAppDbContext _db;

    public BatchesController(IBatchService batches, IAppDbContext db)
    {
        _batches = batches;
        _db = db;
    }

    public async Task<IActionResult> Index(CancellationToken ct)
    {
        return View(await _batches.GetAllAsync(ct));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(string? name, string? session, int batchNo, CancellationToken ct)
    {
        var validationError = Validate(name, session, batchNo);
        if (validationError is not null)
        {
            TempData["Error"] = validationError;
            return RedirectToAction(nameof(Index));
        }

        try
        {
            await _batches.CreateAsync(name!.Trim(), session!.Trim(), batchNo, ct);
            TempData["Success"] = $"Batch \"{name.Trim()}\" was created.";
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(Guid id, CancellationToken ct)
    {
        var batch = (await _batches.GetAllAsync(ct)).FirstOrDefault(b => b.Id == id);
        return batch is null ? NotFound() : View(batch);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(Guid id, string? name, string? session, int batchNo, CancellationToken ct)
    {
        var validationError = Validate(name, session, batchNo);
        if (validationError is not null)
        {
            TempData["Error"] = validationError;
            return View(new BatchDto { Id = id, Name = name ?? string.Empty, Session = session ?? string.Empty, BatchNo = batchNo });
        }

        try
        {
            await _batches.UpdateAsync(id, name!.Trim(), session!.Trim(), batchNo, ct);
            TempData["Success"] = $"Batch \"{name.Trim()}\" was updated.";
            return RedirectToAction(nameof(Index));
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ex.Message;
            return View(new BatchDto { Id = id, Name = name!.Trim(), Session = session!.Trim(), BatchNo = batchNo });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var batch = (await _batches.GetAllAsync(ct)).FirstOrDefault(b => b.Id == id);
        if (batch is null)
        {
            TempData["Error"] = "That batch could not be found.";
            return RedirectToAction(nameof(Index));
        }

        var memberCount = await _db.MemberProfiles.CountAsync(p => p.BatchId == id, ct);
        if (memberCount > 0)
        {
            TempData["Error"] = $"Cannot delete \"{batch.Name}\": {memberCount} member(s) still reference it. Reassign them first.";
            return RedirectToAction(nameof(Index));
        }

        try
        {
            await _batches.DeleteAsync(id, ct);
            TempData["Success"] = $"Batch \"{batch.Name}\" was deleted.";
        }
        catch (Exception ex) when (ex is InvalidOperationException or DbUpdateException)
        {
            TempData["Error"] = $"Cannot delete \"{batch.Name}\": {ex.Message}";
        }

        return RedirectToAction(nameof(Index));
    }

    private static string? Validate(string? name, string? session, int batchNo)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return "Batch name is required.";
        }

        if (name.Trim().Length > MaxNameLength)
        {
            return $"Batch name must be at most {MaxNameLength} characters.";
        }

        if (string.IsNullOrWhiteSpace(session))
        {
            return "Session is required (for example, 2023-2024).";
        }

        if (session.Trim().Length > MaxSessionLength)
        {
            return $"Session must be at most {MaxSessionLength} characters.";
        }

        if (batchNo < 1 || batchNo > 999)
        {
            return "Batch number must be between 1 and 999.";
        }

        return null;
    }
}
