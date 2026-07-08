using GoatFarm.Application.ViewModels.Milk;

namespace GoatFarm.Application.Interfaces;

public interface IMilkService
{
    Task<MilkPageViewModel> GetMilkPageAsync(
        int prodPage = 1,
        int salePage = 1,
        int wastePage = 1,
        int pageSize = 10,
        CancellationToken cancellationToken = default);
    Task<MilkProductionViewModel> AddProductionAsync(CreateMilkProductionViewModel model, CancellationToken cancellationToken = default);
    Task<MilkSaleViewModel> AddSaleAsync(CreateMilkSaleViewModel model, CancellationToken cancellationToken = default);
    Task<MilkWasteViewModel> AddWasteAsync(CreateMilkWasteViewModel model, CancellationToken cancellationToken = default);
    Task<MilkProductionViewModel?> UpdateProductionAsync(int id, CreateMilkProductionViewModel model, CancellationToken cancellationToken = default);
    Task<MilkSaleViewModel?> UpdateSaleAsync(int id, CreateMilkSaleViewModel model, CancellationToken cancellationToken = default);
    Task<MilkWasteViewModel?> UpdateWasteAsync(int id, CreateMilkWasteViewModel model, CancellationToken cancellationToken = default);
    Task<bool> DeleteProductionAsync(int id, CancellationToken cancellationToken = default);
    Task<bool> DeleteSaleAsync(int id, CancellationToken cancellationToken = default);
    Task<bool> DeleteWasteAsync(int id, CancellationToken cancellationToken = default);
    decimal GetMilkLitersProduced(string month);
    decimal GetMilkLitersSold(string month);
    decimal GetMilkLitersWasted(string month);
    decimal GetMilkIncomeMonth(string month);
}
