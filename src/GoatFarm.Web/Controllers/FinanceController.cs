using GoatFarm.Application.Common;
using GoatFarm.Application.Interfaces;
using GoatFarm.Application.ViewModels.Finance;
using GoatFarm.Domain.Constants;
using Microsoft.AspNetCore.Mvc;

namespace GoatFarm.Web.Controllers;

public class FinanceController : Controller
{
    private readonly IFinanceService _financeService;
    private readonly ILookupService _lookupService;
    private readonly IDoubleEntryGuardService _doubleEntryGuard;

    public FinanceController(
        IFinanceService financeService,
        ILookupService lookupService,
        IDoubleEntryGuardService doubleEntryGuard)
    {
        _financeService = financeService;
        _lookupService = lookupService;
        _doubleEntryGuard = doubleEntryGuard;
    }

    [HttpGet]
    public async Task<IActionResult> Index(string? month, CancellationToken cancellationToken)
    {
        ViewData["ActiveTab"] = "finance";
        ViewBag.IncomeTypes = await _lookupService.GetListAsync(LookupSettingKeys.IncomeTypes, cancellationToken);
        ViewBag.ExpenseTypes = await _lookupService.GetListAsync(LookupSettingKeys.ExpenseTypes, cancellationToken);
        ViewBag.AssetTypes = await _lookupService.GetListAsync(LookupSettingKeys.AssetTypes, cancellationToken);
        ViewBag.SalaryReconciliation = await _financeService.GetSalaryReconciliationAsync(cancellationToken);
        return View(await _financeService.GetFinancePageAsync(month, cancellationToken));
    }

    [HttpGet]
    public async Task<IActionResult> GetData(string? month, CancellationToken cancellationToken) =>
        Json(await _financeService.GetFinancePageAsync(month, cancellationToken));

    [HttpGet]
    public async Task<IActionResult> SalaryReconciliation(CancellationToken cancellationToken) =>
        Json(await _financeService.GetSalaryReconciliationAsync(cancellationToken));

    [HttpPost]
    public async Task<IActionResult> AddAsset([FromBody] CreateAssetViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        return Json(await _financeService.AddAssetAsync(model, cancellationToken));
    }

    [HttpPost]
    public async Task<IActionResult> AddIncome([FromBody] CreateIncomeViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        return Json(await _financeService.AddIncomeAsync(model, cancellationToken));
    }

    [HttpPost]
    public async Task<IActionResult> AddExpense([FromBody] CreateExpenseViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        try
        {
            await _doubleEntryGuard.ValidateExpenseAsync(model, null, cancellationToken);
        }
        catch (DoubleEntryValidationException ex)
        {
            return DoubleEntryResult(ex);
        }
        return Json(await _financeService.AddExpenseAsync(model, cancellationToken));
    }

    private ObjectResult DoubleEntryResult(DoubleEntryValidationException ex) =>
        StatusCode(StatusCodes.Status409Conflict, new
        {
            error = ex.Message,
            severity = ex.Severity.ToString().ToLowerInvariant(),
            requiresConfirmation = ex.Severity == DoubleEntrySeverity.Warn
        });

    [HttpPost]
    public async Task<IActionResult> AddOwnerInvestment([FromBody] CreateOwnerInvestmentViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        return Json(await _financeService.AddOwnerInvestmentAsync(model, cancellationToken));
    }

    [HttpPut]
    public async Task<IActionResult> UpdateAsset(int id, [FromBody] CreateAssetViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var result = await _financeService.UpdateAssetAsync(id, model, cancellationToken);
        return result is null ? NotFound() : Json(result);
    }

    [HttpPut]
    public async Task<IActionResult> UpdateIncome(int id, [FromBody] CreateIncomeViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var result = await _financeService.UpdateIncomeAsync(id, model, cancellationToken);
        return result is null ? NotFound() : Json(result);
    }

    [HttpPut]
    public async Task<IActionResult> UpdateExpense(int id, [FromBody] CreateExpenseViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        try
        {
            await _doubleEntryGuard.ValidateExpenseAsync(model, id, cancellationToken);
        }
        catch (DoubleEntryValidationException ex)
        {
            return DoubleEntryResult(ex);
        }
        var result = await _financeService.UpdateExpenseAsync(id, model, cancellationToken);
        return result is null ? NotFound() : Json(result);
    }

    [HttpPut]
    public async Task<IActionResult> UpdateOwnerInvestment(int id, [FromBody] CreateOwnerInvestmentViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var result = await _financeService.UpdateOwnerInvestmentAsync(id, model, cancellationToken);
        return result is null ? NotFound() : Json(result);
    }

    [HttpDelete]
    public async Task<IActionResult> DeleteAsset(int id, CancellationToken cancellationToken)
    {
        var ok = await _financeService.DeleteAssetAsync(id, cancellationToken);
        return ok ? Ok(new { success = true }) : NotFound();
    }

    [HttpDelete]
    public async Task<IActionResult> DeleteIncome(int id, CancellationToken cancellationToken)
    {
        var ok = await _financeService.DeleteIncomeAsync(id, cancellationToken);
        return ok ? Ok(new { success = true }) : NotFound();
    }

    [HttpDelete]
    public async Task<IActionResult> DeleteExpense(int id, CancellationToken cancellationToken)
    {
        var ok = await _financeService.DeleteExpenseAsync(id, cancellationToken);
        return ok ? Ok(new { success = true }) : NotFound();
    }

    [HttpDelete]
    public async Task<IActionResult> DeleteOwnerInvestment(int id, CancellationToken cancellationToken)
    {
        var ok = await _financeService.DeleteOwnerInvestmentAsync(id, cancellationToken);
        return ok ? Ok(new { success = true }) : NotFound();
    }

    [HttpPost]
    public async Task<IActionResult> AddRecurringCost([FromBody] CreateRecurringCostViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        try { _doubleEntryGuard.ValidateRecurringCost(model); }
        catch (DoubleEntryValidationException ex) { return DoubleEntryResult(ex); }
        return Json(await _financeService.AddRecurringCostAsync(model, cancellationToken));
    }

    [HttpPut]
    public async Task<IActionResult> UpdateRecurringCost(int id, [FromBody] CreateRecurringCostViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        try { _doubleEntryGuard.ValidateRecurringCost(model); }
        catch (DoubleEntryValidationException ex) { return DoubleEntryResult(ex); }
        var result = await _financeService.UpdateRecurringCostAsync(id, model, cancellationToken);
        return result is null ? NotFound() : Json(result);
    }

    [HttpDelete]
    public async Task<IActionResult> DeleteRecurringCost(int id, CancellationToken cancellationToken)
    {
        var ok = await _financeService.DeleteRecurringCostAsync(id, cancellationToken);
        return ok ? Ok(new { success = true }) : NotFound();
    }

    [HttpPost]
    public async Task<IActionResult> AddEmployee([FromBody] CreateEmployeeViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        return Json(await _financeService.AddEmployeeAsync(model, cancellationToken));
    }

    [HttpPut]
    public async Task<IActionResult> UpdateEmployee(int id, [FromBody] CreateEmployeeViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var result = await _financeService.UpdateEmployeeAsync(id, model, cancellationToken);
        return result is null ? NotFound() : Json(result);
    }

    [HttpDelete]
    public async Task<IActionResult> DeleteEmployee(int id, CancellationToken cancellationToken)
    {
        var ok = await _financeService.DeleteEmployeeAsync(id, cancellationToken);
        return ok ? Ok(new { success = true }) : NotFound();
    }
}
