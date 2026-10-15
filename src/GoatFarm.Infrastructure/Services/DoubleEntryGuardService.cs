using GoatFarm.Application.Common;
using GoatFarm.Application.Interfaces;
using GoatFarm.Application.ViewModels.Finance;
using GoatFarm.Domain.Constants;
using GoatFarm.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GoatFarm.Infrastructure.Services;

public class DoubleEntryGuardService : IDoubleEntryGuardService
{
    private static readonly string[] FeedWords =
    [
        "feed", "concentrate", "khali", "choker", "bhoosa", "toori", "silage",
        .. FeedTypes.All.Select(f => f.Key).Where(FeedTypes.IsMixIngredient)
    ];

    private readonly GoatFarmDbContext _context;

    public DoubleEntryGuardService(GoatFarmDbContext context) => _context = context;

    public void ValidateRecurringCost(CreateRecurringCostViewModel model)
    {
        if (SalaryExpenseRules.Classify(model.Name) != SalaryExpenseKind.None)
            throw new DoubleEntryValidationException(SalaryExpenseRules.RecurringBlockedMessage, DoubleEntrySeverity.Block);
    }

    public async Task ValidateExpenseAsync(
        CreateExpenseViewModel model,
        int? excludeExpenseId = null,
        CancellationToken cancellationToken = default)
    {
        var type = (model.Type ?? string.Empty).Trim().ToLowerInvariant();

        switch (SalaryExpenseRules.Classify(type))
        {
            case SalaryExpenseKind.SalaryAdvance:
                throw new DoubleEntryValidationException(SalaryExpenseRules.AdvanceBlockedMessage, DoubleEntrySeverity.Block);
            case SalaryExpenseKind.SalaryOrWage:
                throw new DoubleEntryValidationException(SalaryExpenseRules.SalaryBlockedMessage, DoubleEntrySeverity.Block);
        }

        if (model.ConfirmPossibleDuplicate)
            return;

        if (type.Contains("vaccine"))
            throw new DoubleEntryValidationException(
                "Vaccines bought on the Vaccines page are already counted in running costs. Only add this if it was not recorded there.",
                DoubleEntrySeverity.Warn);

        if (FeedWords.Any(type.Contains))
            throw new DoubleEntryValidationException(
                "Feed bought on the Feed page is already counted in running costs. Only add this if it was not recorded there.",
                DoubleEntrySeverity.Warn);

        var duplicate = await _context.Expenses.AsNoTracking().AnyAsync(e =>
            e.Date == model.Date &&
            e.Amount == model.Amount &&
            e.Type.ToLower() == type &&
            (excludeExpenseId == null || e.Id != excludeExpenseId), cancellationToken);

        if (duplicate)
            throw new DoubleEntryValidationException(
                $"An expense of the same type and amount is already recorded on {model.Date:yyyy-MM-dd}.",
                DoubleEntrySeverity.Warn);
    }
}
