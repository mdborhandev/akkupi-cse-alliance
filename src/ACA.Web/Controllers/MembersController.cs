using ACA.Application.DTOs;
using ACA.Application.DTOs.Common;
using ACA.Application.Interfaces;
using ACA.Domain.Enums;
using Microsoft.AspNetCore.Mvc;

namespace ACA.Web.Controllers;

public class MembersController : Controller
{
    private const int PageSize = 12;

    private readonly IMemberSearchService _searchService;
    private readonly IProfileService _profileService;
    private readonly IBatchService _batchService;

    public MembersController(
        IMemberSearchService searchService,
        IProfileService profileService,
        IBatchService batchService)
    {
        _searchService = searchService;
        _profileService = profileService;
        _batchService = batchService;
    }

    [HttpGet]
    public async Task<IActionResult> Index(string? q, Guid? batchId, MemberType? memberType, int page = 1, CancellationToken ct = default)
    {
        if (page < 1) page = 1;

        var results = await _searchService.SearchAsync(q, batchId, memberType, page, PageSize, ct);

        return View(new MembersIndexViewModel
        {
            Results = results,
            Batches = await _batchService.GetAllAsync(ct),
            Query = q,
            BatchId = batchId,
            MemberType = memberType
        });
    }

    [HttpGet]
    public async Task<IActionResult> Details(Guid id, CancellationToken ct)
    {
        if (id == Guid.Empty) return NotFound();

        var profile = await _profileService.GetAsync(id, ct);
        if (profile is null) return NotFound();

        // Unapproved profiles stay hidden from the public directory unless you moderate the community.
        if (!profile.IsApproved && !User.IsInRole("Admin") && !User.IsInRole("Moderator"))
        {
            return NotFound();
        }

        return View(profile);
    }

    public sealed class MembersIndexViewModel
    {
        public PagedResult<MemberSummaryDto> Results { get; init; } = PagedResult<MemberSummaryDto>.Empty(1, PageSize);
        public IReadOnlyList<BatchDto> Batches { get; init; } = Array.Empty<BatchDto>();
        public string? Query { get; init; }
        public Guid? BatchId { get; init; }
        public MemberType? MemberType { get; init; }
    }
}
