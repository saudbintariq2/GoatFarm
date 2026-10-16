using System.Text.Json;
using GoatFarm.Application.Interfaces;
using GoatFarm.Application.ViewModels.Finance;
using GoatFarm.Domain.Entities;
using GoatFarm.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GoatFarm.Infrastructure.Services.Extended;

public interface IApprovalExecutionService
{
    Task<(bool Ok, string? Error)> ExecuteAsync(ApprovalRequest request, CancellationToken cancellationToken = default);
}

public class ApprovalExecutionService : IApprovalExecutionService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private readonly GoatFarmDbContext _context;
    private readonly IGoatService _goatService;
    private readonly IFinanceService _financeService;
    private readonly ITradingService _tradingService;
    private readonly IMilkService _milkService;
    private readonly IFeedService _feedService;
    private readonly ITaskService _taskService;
    private readonly IProjectsService _projectsService;
    private readonly ITreatmentService _treatmentService;
    private readonly IVaccineService _vaccineService;
    private readonly IReminderService _reminderService;

    public ApprovalExecutionService(
        GoatFarmDbContext context,
        IGoatService goatService,
        IFinanceService financeService,
        ITradingService tradingService,
        IMilkService milkService,
        IFeedService feedService,
        ITaskService taskService,
        IProjectsService projectsService,
        ITreatmentService treatmentService,
        IVaccineService vaccineService,
        IReminderService reminderService)
    {
        _context = context;
        _goatService = goatService;
        _financeService = financeService;
        _tradingService = tradingService;
        _milkService = milkService;
        _feedService = feedService;
        _taskService = taskService;
        _projectsService = projectsService;
        _treatmentService = treatmentService;
        _vaccineService = vaccineService;
        _reminderService = reminderService;
    }

    public async Task<(bool Ok, string? Error)> ExecuteAsync(ApprovalRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            using var doc = JsonDocument.Parse(request.PayloadJson);
            var root = doc.RootElement;
            var entity = root.TryGetProperty("entity", out var e) ? e.GetString() : null;
            var action = root.TryGetProperty("action", out var a) ? a.GetString() : request.ActionKey;
            var id = root.TryGetProperty("id", out var idEl) && idEl.TryGetInt32(out var n) ? n : (int?)null;

            switch ($"{entity}.{action}".ToLowerInvariant())
            {
                case "goat.delete":
                    if (id is null) return (false, "Missing goat id.");
                    try
                    {
                        var ok = await _goatService.DeleteAsync(id.Value, cancellationToken);
                        return ok ? (true, null) : (false, "Goat not found.");
                    }
                    catch (InvalidOperationException ex) { return (false, ex.Message); }

                case "expense.delete":
                    if (id is null) return (false, "Missing expense id.");
                    return await _financeService.DeleteExpenseAsync(id.Value, cancellationToken)
                        ? (true, null) : (false, "Expense not found.");

                case "income.delete":
                    if (id is null) return (false, "Missing income id.");
                    return await _financeService.DeleteIncomeAsync(id.Value, cancellationToken)
                        ? (true, null) : (false, "Income not found.");

                case "asset.delete":
                    if (id is null) return (false, "Missing asset id.");
                    return await _financeService.DeleteAssetAsync(id.Value, cancellationToken)
                        ? (true, null) : (false, "Asset not found.");

                case "ownerinvestment.delete":
                    if (id is null) return (false, "Missing id.");
                    return await _financeService.DeleteOwnerInvestmentAsync(id.Value, cancellationToken)
                        ? (true, null) : (false, "Entry not found.");

                case "recurringcost.delete":
                    if (id is null) return (false, "Missing id.");
                    return await _financeService.DeleteRecurringCostAsync(id.Value, cancellationToken)
                        ? (true, null) : (false, "Entry not found.");

                case "employee.delete":
                    if (id is null) return (false, "Missing id.");
                    return await _financeService.DeleteEmployeeAsync(id.Value, cancellationToken)
                        ? (true, null) : (false, "Employee not found.");

                case "tradeentry.delete":
                    if (id is null) return (false, "Missing id.");
                    return await _tradingService.DeleteEntryAsync(id.Value, cancellationToken)
                        ? (true, null) : (false, "Entry not found.");

                case "milkproduction.delete":
                    if (id is null) return (false, "Missing id.");
                    return await _milkService.DeleteProductionAsync(id.Value, cancellationToken)
                        ? (true, null) : (false, "Entry not found.");

                case "milksale.delete":
                    if (id is null) return (false, "Missing id.");
                    return await _milkService.DeleteSaleAsync(id.Value, cancellationToken)
                        ? (true, null) : (false, "Entry not found.");

                case "milkwaste.delete":
                    if (id is null) return (false, "Missing id.");
                    return await _milkService.DeleteWasteAsync(id.Value, cancellationToken)
                        ? (true, null) : (false, "Entry not found.");

                case "feedpurchase.delete":
                    if (id is null) return (false, "Missing id.");
                    return await _feedService.DeleteFeedPurchaseAsync(id.Value, cancellationToken)
                        ? (true, null) : (false, "Entry not found.");

                case "feedtype.delete":
                {
                    var feedType = root.TryGetProperty("feedType", out var ft) ? ft.GetString() : null;
                    if (string.IsNullOrWhiteSpace(feedType)) return (false, "Missing feed type.");
                    return await _feedService.DeleteFeedTypeAsync(feedType, cancellationToken)
                        ? (true, null) : (false, "Feed type not found.");
                }

                case "farmtask.delete":
                    if (id is null) return (false, "Missing id.");
                    return await _taskService.DeleteAsync(id.Value, cancellationToken)
                        ? (true, null) : (false, "Task not found.");

                case "farmproject.delete":
                    if (id is null) return (false, "Missing id.");
                    return await _projectsService.DeleteProjectAsync(id.Value, cancellationToken)
                        ? (true, null) : (false, "Project not found.");

                case "goattreatment.delete":
                    if (id is null) return (false, "Missing id.");
                    return await _treatmentService.DeleteAsync(id.Value, cancellationToken)
                        ? (true, null) : (false, "Treatment not found.");

                case "vaccine.delete":
                    if (id is null) return (false, "Missing id.");
                    return await _vaccineService.DeleteVaccineAsync(id.Value, cancellationToken)
                        ? (true, null) : (false, "Vaccine not found.");

                case "vaccinepurchase.delete":
                    if (id is null) return (false, "Missing id.");
                    return await _vaccineService.DeleteVaccinePurchaseAsync(id.Value, cancellationToken)
                        ? (true, null) : (false, "Purchase not found.");

                case "vaccinationbatch.delete":
                {
                    if (!root.TryGetProperty("vaccineId", out var vidEl) || !vidEl.TryGetInt32(out var vaccineId))
                        return (false, "Missing vaccine id.");
                    var dateStr = root.TryGetProperty("date", out var dEl) ? dEl.GetString() : null;
                    if (!DateOnly.TryParse(dateStr, out var date)) return (false, "Missing or invalid date.");
                    return await _vaccineService.DeleteVaccinationBatchAsync(vaccineId, date, cancellationToken)
                        ? (true, null) : (false, "Batch not found.");
                }

                case "reminder.delete":
                    if (id is null) return (false, "Missing id.");
                    return await _reminderService.DeleteReminderAsync(id.Value, cancellationToken)
                        ? (true, null) : (false, "Reminder not found.");

                case "income.update":
                    if (id is null || !root.TryGetProperty("data", out var incData))
                        return (false, "Missing income update payload.");
                    var incModel = JsonSerializer.Deserialize<CreateIncomeViewModel>(incData.GetRawText(), JsonOptions);
                    if (incModel is null) return (false, "Invalid income data.");
                    return await _financeService.UpdateIncomeAsync(id.Value, incModel, cancellationToken) is not null
                        ? (true, null) : (false, "Income not found.");

                case "expense.update":
                    if (id is null || !root.TryGetProperty("data", out var expData))
                        return (false, "Missing expense update payload.");
                    var expModel = JsonSerializer.Deserialize<CreateExpenseViewModel>(expData.GetRawText(), JsonOptions);
                    if (expModel is null) return (false, "Invalid expense data.");
                    return await _financeService.UpdateExpenseAsync(id.Value, expModel, cancellationToken) is not null
                        ? (true, null) : (false, "Expense not found.");

                case "asset.update":
                    if (id is null || !root.TryGetProperty("data", out var astData))
                        return (false, "Missing asset update payload.");
                    var astModel = JsonSerializer.Deserialize<CreateAssetViewModel>(astData.GetRawText(), JsonOptions);
                    if (astModel is null) return (false, "Invalid asset data.");
                    return await _financeService.UpdateAssetAsync(id.Value, astModel, cancellationToken) is not null
                        ? (true, null) : (false, "Asset not found.");

                case "ownerinvestment.update":
                    if (id is null || !root.TryGetProperty("data", out var ownData))
                        return (false, "Missing update payload.");
                    var ownModel = JsonSerializer.Deserialize<CreateOwnerInvestmentViewModel>(ownData.GetRawText(), JsonOptions);
                    if (ownModel is null) return (false, "Invalid data.");
                    return await _financeService.UpdateOwnerInvestmentAsync(id.Value, ownModel, cancellationToken) is not null
                        ? (true, null) : (false, "Entry not found.");

                case "recurringcost.update":
                    if (id is null || !root.TryGetProperty("data", out var rcData))
                        return (false, "Missing update payload.");
                    var rcModel = JsonSerializer.Deserialize<CreateRecurringCostViewModel>(rcData.GetRawText(), JsonOptions);
                    if (rcModel is null) return (false, "Invalid data.");
                    return await _financeService.UpdateRecurringCostAsync(id.Value, rcModel, cancellationToken) is not null
                        ? (true, null) : (false, "Entry not found.");

                case "employee.update":
                    if (id is null || !root.TryGetProperty("data", out var empData))
                        return (false, "Missing update payload.");
                    var empModel = JsonSerializer.Deserialize<CreateEmployeeViewModel>(empData.GetRawText(), JsonOptions);
                    if (empModel is null) return (false, "Invalid data.");
                    return await _financeService.UpdateEmployeeAsync(id.Value, empModel, cancellationToken) is not null
                        ? (true, null) : (false, "Employee not found.");

                default:
                    return (false, $"Unsupported approval action: {entity}.{action}");
            }
        }
        catch (JsonException)
        {
            return (false, "Invalid approval payload.");
        }
    }
}
