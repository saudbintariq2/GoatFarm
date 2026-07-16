using GoatFarm.Application.ViewModels.Vaccines;

namespace GoatFarm.Application.Interfaces;

public interface IVaccineService
{
    Task<VaccinePageViewModel> GetVaccinePageAsync(int? remindDays, string? month = null, CancellationToken cancellationToken = default);
    Task<VaccineViewModel> AddVaccineAsync(CreateVaccineViewModel model, CancellationToken cancellationToken = default);
    Task<VaccineViewModel?> UpdateVaccineAsync(int id, CreateVaccineViewModel model, CancellationToken cancellationToken = default);
    Task<bool> DeleteVaccineAsync(int id, CancellationToken cancellationToken = default);
    Task MarkVaccineDoneAsync(int vaccineId, CancellationToken cancellationToken = default);
    Task<bool> DeleteVaccinationBatchAsync(int vaccineId, DateOnly date, CancellationToken cancellationToken = default);
    Task<bool> UpdateVaccinationBatchAsync(UpdateVaccinationBatchViewModel model, CancellationToken cancellationToken = default);
    Task SetReminderWindowAsync(int days, CancellationToken cancellationToken = default);
    Task<VaccinePurchaseViewModel> AddVaccinePurchaseAsync(CreateVaccinePurchaseViewModel model, CancellationToken cancellationToken = default);
    Task<VaccinePurchaseViewModel?> UpdateVaccinePurchaseAsync(int id, CreateVaccinePurchaseViewModel model, CancellationToken cancellationToken = default);
    Task<bool> DeleteVaccinePurchaseAsync(int id, CancellationToken cancellationToken = default);
    decimal GetVaccinePurchasedMonthly(string month);
}
