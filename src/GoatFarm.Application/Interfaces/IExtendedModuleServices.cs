using GoatFarm.Application.ViewModels.Admin;
using GoatFarm.Application.ViewModels.Approval;
using GoatFarm.Application.ViewModels.Ledger;
using GoatFarm.Application.ViewModels.Products;
using GoatFarm.Application.ViewModels.Projects;
using GoatFarm.Application.ViewModels.Trading;
using GoatFarm.Application.ViewModels.Treatments;
using GoatFarm.Domain.Constants;
using GoatFarm.Domain.Enums;

namespace GoatFarm.Application.Interfaces;

public interface IAuditService
{
    Task LogAsync(string module, AuditAction action, string summary, string? userId, string? userDisplay,
        string? entityType = null, int? entityId = null, string? detailJson = null, bool showInNotices = true,
        CancellationToken cancellationToken = default);
    Task<AdminPageViewModel> GetAdminPageAsync(int auditTake, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AuditNoticeViewModel>> GetRecentNoticesAsync(int take, CancellationToken cancellationToken = default);
}

public interface IApprovalService
{
    Task<int> CountPendingAsync(CancellationToken cancellationToken = default);
    Task<ApprovalPageViewModel> GetPageAsync(CancellationToken cancellationToken = default);
    Task<ApprovalRequestViewModel?> SubmitAsync(string module, string actionKey, string payloadJson,
        string userId, string? userDisplay, CancellationToken cancellationToken = default);
    Task<bool> ReviewAsync(int id, bool approve, string reviewerId, string? reviewerDisplay, string? note,
        CancellationToken cancellationToken = default);
    Task<bool> NeedsApprovalAsync(string userId, string role, string action, CancellationToken cancellationToken = default);
}

public interface ITradingService
{
    Task<TradingPageViewModel> GetPageAsync(string? month, CancellationToken cancellationToken = default);
    Task EnsureSeededAsync(CancellationToken cancellationToken = default);
    Task<object> SaveEntryAsync(SaveTradeEntryViewModel model, CancellationToken cancellationToken = default);
    Task<bool> DeleteEntryAsync(int id, CancellationToken cancellationToken = default);
    Task<object> SaveMasterAsync(SaveTradeMasterViewModel model, CancellationToken cancellationToken = default);
}

public interface IProjectsService
{
    Task<ProjectsPageViewModel> GetPageAsync(CancellationToken cancellationToken = default);
    Task<object> SaveProjectAsync(SaveProjectViewModel model, CancellationToken cancellationToken = default);
    Task<bool> DeleteProjectAsync(int id, CancellationToken cancellationToken = default);
    Task<object> SaveExpenditureAsync(SaveProjectExpenditureViewModel model, CancellationToken cancellationToken = default);
    Task<object> SaveFixedAssetAsync(SaveFixedAssetViewModel model, CancellationToken cancellationToken = default);
    Task<object> SaveSolarLogAsync(SaveSolarLogViewModel model, CancellationToken cancellationToken = default);
}

public interface ILedgerService
{
    Task<LedgerPageViewModel> GetPageAsync(string? month, CancellationToken cancellationToken = default);
    Task<object> SaveAccountAsync(SaveMoneyAccountViewModel model, CancellationToken cancellationToken = default);
    Task<object> SaveTransactionAsync(SaveLedgerTransactionViewModel model, CancellationToken cancellationToken = default);
    Task<object> SaveInvoiceAsync(SaveCustomerInvoiceViewModel model, CancellationToken cancellationToken = default);
    Task<object> SavePayableAsync(SaveSupplierPayableViewModel model, CancellationToken cancellationToken = default);
    Task<object> SaveBudgetAsync(SaveBudgetLineViewModel model, CancellationToken cancellationToken = default);
    Task<object> SaveProfitPartnerAsync(SaveProfitSharePartnerViewModel model, CancellationToken cancellationToken = default);
    Task<object> SaveCustomerAsync(SaveBusinessCustomerViewModel model, CancellationToken cancellationToken = default);
    Task<FinancialStatementsViewModel> GetStatementsAsync(string? month, CancellationToken cancellationToken = default);
}

public interface IProductsService
{
    Task<ProductsPageViewModel> GetPageAsync(string? month, CancellationToken cancellationToken = default);
    Task<object> SaveCatalogItemAsync(SaveProductCatalogViewModel model, CancellationToken cancellationToken = default);
    Task<object> SaveWarehouseAsync(SaveWarehouseViewModel model, CancellationToken cancellationToken = default);
    Task<object> SaveStockMovementAsync(SaveStockMovementViewModel model, CancellationToken cancellationToken = default);
    Task<object> SaveMilkingSessionAsync(SaveMilkingSessionViewModel model, CancellationToken cancellationToken = default);
    Task<object> SaveSlaughterAsync(SaveSlaughterViewModel model, CancellationToken cancellationToken = default);
    Task<object> SaveProductSaleAsync(SaveProductSaleViewModel model, CancellationToken cancellationToken = default);
}

public interface ITreatmentService
{
    Task<TreatmentsPageViewModel> GetPageAsync(CancellationToken cancellationToken = default);
    Task<object> SaveAsync(SaveTreatmentViewModel model, CancellationToken cancellationToken = default);
    Task<bool> RecoverAsync(int treatmentId, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default);
}
