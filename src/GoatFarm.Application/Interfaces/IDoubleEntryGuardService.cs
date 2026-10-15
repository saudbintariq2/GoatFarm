using GoatFarm.Application.ViewModels.Finance;

namespace GoatFarm.Application.Interfaces;

/// <summary>
/// Stops manual expenses that would count a cost the app already adds automatically
/// (staff salaries, vaccine purchases, feed purchases) or that repeat an existing entry.
/// Throws <see cref="Common.DoubleEntryValidationException"/>.
/// </summary>
public interface IDoubleEntryGuardService
{
    Task ValidateExpenseAsync(CreateExpenseViewModel model, int? excludeExpenseId = null, CancellationToken cancellationToken = default);
    /// <summary>A recurring cost for a salary, wage or advance is counted every month on top of the Employees list.</summary>
    void ValidateRecurringCost(CreateRecurringCostViewModel model);
}
