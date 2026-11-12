using ACA.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace ACA.Web.Controllers;

public class BatchesController : Controller
{
    private readonly IBatchService _batchService;

    public BatchesController(IBatchService batchService)
    {
        _batchService = batchService;
    }

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var batches = await _batchService.GetAllAsync(ct);
        return View(batches);
    }
}
