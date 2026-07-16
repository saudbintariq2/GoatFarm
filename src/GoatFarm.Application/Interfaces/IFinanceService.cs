using GoatFarm.Application.ViewModels.Finance;

namespace GoatFarm.Application.Interfaces;

public interface IFinanceService
{
    Task<FinancePageViewModel> GetFinancePageAsync(string? month, CancellationToken cancellationToken = default);
    Task<AssetViewModel> AddAssetAsync(CreateAssetViewModel model, CancellationToken cancellationToken = default);
    Task<IncomeViewModel> AddIncomeAsync(CreateIncomeViewModel model, CancellationToken cancellationToken = default);
    Task<ExpenseViewModel> AddExpenseAsync(CreateExpenseViewModel model, CancellationToken cancellationToken = default);
    Task<OwnerInvestmentViewModel> AddOwnerInvestmentAsync(CreateOwnerInvestmentViewModel model, CancellationToken cancellationToken = default);
    Task<AssetViewModel?> UpdateAssetAsync(int id, CreateAssetViewModel model, CancellationToken cancellationToken = default);
    Task<IncomeViewModel?> UpdateIncomeAsync(int id, CreateIncomeViewModel model, CancellationToken cancellationToken = default);
    Task<ExpenseViewModel?> UpdateExpenseAsync(int id, CreateExpenseViewModel model, CancellationToken cancellationToken = default);
    Task<OwnerInvestmentViewModel?> UpdateOwnerInvestmentAsync(int id, CreateOwnerInvestmentViewModel model, CancellationToken cancellationToken = default);
    Task<bool> DeleteAssetAsync(int id, CancellationToken cancellationToken = default);
    Task<bool> DeleteIncomeAsync(int id, CancellationToken cancellationToken = default);
    Task<bool> DeleteExpenseAsync(int id, CancellationToken cancellationToken = default);
    Task<bool> DeleteOwnerInvestmentAsync(int id, CancellationToken cancellationToken = default);
    decimal GetLivestockValue();
    decimal GetAssetsValue();
    decimal GetCapital();
}
