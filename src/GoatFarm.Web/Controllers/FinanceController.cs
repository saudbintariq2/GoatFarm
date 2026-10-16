using GoatFarm.Application.Common;
using GoatFarm.Application.Interfaces;
using GoatFarm.Application.ViewModels.Finance;
using GoatFarm.Application.ViewModels.Ledger;
using GoatFarm.Domain.Constants;
using GoatFarm.Web.Helpers;
using Microsoft.AspNetCore.Mvc;

namespace GoatFarm.Web.Controllers;

public class FinanceController : Controller
{
    private readonly IFinanceService _financeService;
    private readonly ILedgerService _ledgerService;
    private readonly ILookupService _lookupService;
    private readonly IDoubleEntryGuardService _doubleEntryGuard;
    private readonly IApprovalService _approvalService;
    private readonly IPermissionService _permissionService;

    public FinanceController(
        IFinanceService financeService,
        ILedgerService ledgerService,
        ILookupService lookupService,
        IDoubleEntryGuardService doubleEntryGuard,
        IApprovalService approvalService,
        IPermissionService permissionService)
    {
        _financeService = financeService;
        _ledgerService = ledgerService;
        _lookupService = lookupService;
        _doubleEntryGuard = doubleEntryGuard;
        _approvalService = approvalService;
        _permissionService = permissionService;
    }

    [HttpGet]
    public async Task<IActionResult> Index(string? month, CancellationToken cancellationToken)
    {
        ViewData["ActiveTab"] = "finance";
        ViewBag.IncomeTypes = await _lookupService.GetListAsync(LookupSettingKeys.IncomeTypes, cancellationToken);
        ViewBag.ExpenseTypes = await _lookupService.GetListAsync(LookupSettingKeys.ExpenseTypes, cancellationToken);
        ViewBag.AssetTypes = await _lookupService.GetListAsync(LookupSettingKeys.AssetTypes, cancellationToken);
        ViewBag.SalaryReconciliation = await _financeService.GetSalaryReconciliationAsync(cancellationToken);
        ViewBag.LedgerPage = await _ledgerService.GetPageAsync(month, cancellationToken);
        ViewBag.Statements = await _ledgerService.GetStatementsAsync(month, cancellationToken);
        return View(await _financeService.GetFinancePageAsync(month, cancellationToken));
    }

    [HttpGet]
    public async Task<IActionResult> LedgerData(string? month, CancellationToken cancellationToken) =>
        Json(new
        {
            ledger = await _ledgerService.GetPageAsync(month, cancellationToken),
            statements = await _ledgerService.GetStatementsAsync(month, cancellationToken)
        });

    [HttpPost]
    public async Task<IActionResult> SaveMoneyAccount([FromBody] SaveMoneyAccountViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        return Json(await _ledgerService.SaveAccountAsync(model, cancellationToken));
    }

    [HttpPost]
    public async Task<IActionResult> SaveLedgerTransaction([FromBody] SaveLedgerTransactionViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        return Json(await _ledgerService.SaveTransactionAsync(model, cancellationToken));
    }

    [HttpPost]
    public async Task<IActionResult> SaveCustomerInvoice([FromBody] SaveCustomerInvoiceViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        return Json(await _ledgerService.SaveInvoiceAsync(model, cancellationToken));
    }

    [HttpPost]
    public async Task<IActionResult> SaveSupplierPayable([FromBody] SaveSupplierPayableViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        return Json(await _ledgerService.SavePayableAsync(model, cancellationToken));
    }

    [HttpPost]
    public async Task<IActionResult> SaveBudgetLine([FromBody] SaveBudgetLineViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        return Json(await _ledgerService.SaveBudgetAsync(model, cancellationToken));
    }

    [HttpPost]
    public async Task<IActionResult> SaveProfitPartner([FromBody] SaveProfitSharePartnerViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        return Json(await _ledgerService.SaveProfitPartnerAsync(model, cancellationToken));
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
        var pending = await ApprovalGate.GuardEditAsync(this, User, _approvalService, _permissionService,
            FarmTabs.Finance, "Asset", id, model, cancellationToken);
        if (pending is not null) return pending;
        var result = await _financeService.UpdateAssetAsync(id, model, cancellationToken);
        return result is null ? NotFound() : Json(result);
    }

    [HttpPut]
    public async Task<IActionResult> UpdateIncome(int id, [FromBody] CreateIncomeViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var pending = await ApprovalGate.GuardEditAsync(this, User, _approvalService, _permissionService,
            FarmTabs.Finance, "Income", id, model, cancellationToken);
        if (pending is not null) return pending;
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
        var pending = await ApprovalGate.GuardEditAsync(this, User, _approvalService, _permissionService,
            FarmTabs.Finance, "Expense", id, model, cancellationToken);
        if (pending is not null) return pending;
        var result = await _financeService.UpdateExpenseAsync(id, model, cancellationToken);
        return result is null ? NotFound() : Json(result);
    }

    [HttpPut]
    public async Task<IActionResult> UpdateOwnerInvestment(int id, [FromBody] CreateOwnerInvestmentViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var pending = await ApprovalGate.GuardEditAsync(this, User, _approvalService, _permissionService,
            FarmTabs.Finance, "OwnerInvestment", id, model, cancellationToken);
        if (pending is not null) return pending;
        var result = await _financeService.UpdateOwnerInvestmentAsync(id, model, cancellationToken);
        return result is null ? NotFound() : Json(result);
    }

    [HttpDelete]
    public async Task<IActionResult> DeleteAsset(int id, CancellationToken cancellationToken)
    {
        var pending = await ApprovalGate.GuardDeleteAsync(this, User, _approvalService, _permissionService,
            FarmTabs.Finance, "Asset", id, cancellationToken);
        if (pending is not null) return pending;
        var ok = await _financeService.DeleteAssetAsync(id, cancellationToken);
        return ok ? Ok(new { success = true }) : NotFound();
    }

    [HttpDelete]
    public async Task<IActionResult> DeleteIncome(int id, CancellationToken cancellationToken)
    {
        var pending = await ApprovalGate.GuardDeleteAsync(this, User, _approvalService, _permissionService,
            FarmTabs.Finance, "Income", id, cancellationToken);
        if (pending is not null) return pending;
        var ok = await _financeService.DeleteIncomeAsync(id, cancellationToken);
        return ok ? Ok(new { success = true }) : NotFound();
    }

    [HttpDelete]
    public async Task<IActionResult> DeleteExpense(int id, CancellationToken cancellationToken)
    {
        var pending = await ApprovalGate.GuardDeleteAsync(this, User, _approvalService, _permissionService,
            FarmTabs.Finance, "Expense", id, cancellationToken);
        if (pending is not null) return pending;
        var ok = await _financeService.DeleteExpenseAsync(id, cancellationToken);
        return ok ? Ok(new { success = true }) : NotFound();
    }

    [HttpDelete]
    public async Task<IActionResult> DeleteOwnerInvestment(int id, CancellationToken cancellationToken)
    {
        var pending = await ApprovalGate.GuardDeleteAsync(this, User, _approvalService, _permissionService,
            FarmTabs.Finance, "OwnerInvestment", id, cancellationToken);
        if (pending is not null) return pending;
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
        var pending = await ApprovalGate.GuardEditAsync(this, User, _approvalService, _permissionService,
            FarmTabs.Finance, "RecurringCost", id, model, cancellationToken);
        if (pending is not null) return pending;
        var result = await _financeService.UpdateRecurringCostAsync(id, model, cancellationToken);
        return result is null ? NotFound() : Json(result);
    }

    [HttpDelete]
    public async Task<IActionResult> DeleteRecurringCost(int id, CancellationToken cancellationToken)
    {
        var pending = await ApprovalGate.GuardDeleteAsync(this, User, _approvalService, _permissionService,
            FarmTabs.Finance, "RecurringCost", id, cancellationToken);
        if (pending is not null) return pending;
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
        var pending = await ApprovalGate.GuardEditAsync(this, User, _approvalService, _permissionService,
            FarmTabs.Finance, "Employee", id, model, cancellationToken);
        if (pending is not null) return pending;
        var result = await _financeService.UpdateEmployeeAsync(id, model, cancellationToken);
        return result is null ? NotFound() : Json(result);
    }

    [HttpDelete]
    public async Task<IActionResult> DeleteEmployee(int id, CancellationToken cancellationToken)
    {
        var pending = await ApprovalGate.GuardDeleteAsync(this, User, _approvalService, _permissionService,
            FarmTabs.Finance, "Employee", id, cancellationToken);
        if (pending is not null) return pending;
        var ok = await _financeService.DeleteEmployeeAsync(id, cancellationToken);
        return ok ? Ok(new { success = true }) : NotFound();
    }

    [HttpPost]
    public async Task<IActionResult> SaveBusinessCustomer([FromBody] SaveBusinessCustomerViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        return Json(await _ledgerService.SaveCustomerAsync(model, cancellationToken));
    }
}
