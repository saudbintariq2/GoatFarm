using GoatFarm.Application.Common;
using GoatFarm.Application.Interfaces;
using GoatFarm.Application.ViewModels.Reports;
using GoatFarm.Domain.Constants;
using Microsoft.AspNetCore.Mvc;

namespace GoatFarm.Web.Controllers;

public class ReportsController : Controller
{
    private readonly IReportsService _reportsService;
    private readonly IPermissionService _permissionService;

    public ReportsController(IReportsService reportsService, IPermissionService permissionService)
    {
        _reportsService = reportsService;
        _permissionService = permissionService;
    }

    [HttpGet]
    public async Task<IActionResult> Index(string? period, string? from, string? to, string? groupBy, CancellationToken cancellationToken)
    {
        ViewData["ActiveTab"] = "reports";
        return View(await GetReportsForUserAsync(period, from, to, groupBy, cancellationToken));
    }

    [HttpGet]
    public async Task<IActionResult> GetData(string? period, string? from, string? to, string? groupBy, CancellationToken cancellationToken) =>
        Json(await GetReportsForUserAsync(period, from, to, groupBy, cancellationToken));

    /// <summary>The CSV export and print are built from this data in the browser, so they inherit the redaction.</summary>
    private async Task<ReportsPageViewModel> GetReportsForUserAsync(
        string? period, string? from, string? to, string? groupBy, CancellationToken cancellationToken)
    {
        var page = await _reportsService.GetReportsPageAsync(period, from, to, groupBy, cancellationToken);
        return await _permissionService.CanAsync(FarmTabs.Finance, FarmActions.View, cancellationToken)
            ? page
            : FinanceVisibility.WithoutFinance(page);
    }
}
