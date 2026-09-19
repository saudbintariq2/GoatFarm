using GoatFarm.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace GoatFarm.Web.Controllers;

[IgnoreAntiforgeryToken]
public class ReportsController : Controller
{
    private readonly IReportsService _reportsService;

    public ReportsController(IReportsService reportsService) => _reportsService = reportsService;

    [HttpGet]
    public async Task<IActionResult> Index(string? period, string? from, string? to, string? groupBy, CancellationToken cancellationToken)
    {
        ViewData["ActiveTab"] = "reports";
        return View(await _reportsService.GetReportsPageAsync(period, from, to, groupBy, cancellationToken));
    }

    [HttpGet]
    public async Task<IActionResult> GetData(string? period, string? from, string? to, string? groupBy, CancellationToken cancellationToken) =>
        Json(await _reportsService.GetReportsPageAsync(period, from, to, groupBy, cancellationToken));
}
