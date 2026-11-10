using System.Text.Json;
using GoatFarm.Application.Common;
using GoatFarm.Application.Interfaces;
using GoatFarm.Domain.Constants;
using GoatFarm.Domain.Entities;
using GoatFarm.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GoatFarm.Infrastructure.Services;

public class LookupService : ILookupService
{
    private static readonly IReadOnlyDictionary<string, string[]> Defaults =
        new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            [LookupSettingKeys.IncomeTypes] = LookupConstants.IncomeTypes,
            [LookupSettingKeys.ExpenseTypes] = LookupConstants.ExpenseTypes,
            [LookupSettingKeys.AssetTypes] = LookupConstants.AssetTypes,
            [LookupSettingKeys.Breeds] = LookupConstants.Breeds,
            [LookupSettingKeys.VaccineNames] = LookupConstants.VaccineNames,
            [LookupSettingKeys.VaccineUnits] = LookupConstants.VaccineUnits,
            [LookupSettingKeys.Owners] = LookupConstants.Owners,
            [LookupSettingKeys.HealthStates] = LookupConstants.HealthStates,
            [LookupSettingKeys.SaleTypes] = LookupConstants.SaleTypes,
            [LookupSettingKeys.FundingShareholders] = LookupConstants.FundingShareholders,
            [LookupSettingKeys.ProjectCategories] = LookupConstants.ProjectCategories,
            [LookupSettingKeys.CapexCostTypes] = LookupConstants.CapexCostTypes,
            [LookupSettingKeys.ProjectAssetCategories] = LookupConstants.ProjectAssetCategories,
            [LookupSettingKeys.ProjectFundingSources] = LookupConstants.ProjectFundingSources
        };

    private readonly GoatFarmDbContext _context;

    public LookupService(GoatFarmDbContext context) => _context = context;

    public async Task EnsureDefaultsAsync(CancellationToken cancellationToken = default)
    {
        foreach (var (key, defaults) in Defaults)
        {
            if (await _context.AppSettings.AnyAsync(s => s.Key == key, cancellationToken))
                continue;

            _context.AppSettings.Add(new AppSetting
            {
                Key = key,
                Value = JsonSerializer.Serialize(defaults)
            });
        }

        await MergeMissingLookupOptionsAsync(LookupSettingKeys.HealthStates, LookupConstants.HealthStates, cancellationToken);
        await MergeMissingLookupOptionsAsync(LookupSettingKeys.ProjectCategories, LookupConstants.ProjectCategories, cancellationToken);
        await MergeMissingLookupOptionsAsync(LookupSettingKeys.CapexCostTypes, LookupConstants.CapexCostTypes, cancellationToken);
        await MergeMissingLookupOptionsAsync(LookupSettingKeys.ProjectAssetCategories, LookupConstants.ProjectAssetCategories, cancellationToken);
        await MergeMissingLookupOptionsAsync(LookupSettingKeys.ProjectFundingSources, LookupConstants.ProjectFundingSources, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    private async Task MergeMissingLookupOptionsAsync(string key, string[] canonical, CancellationToken cancellationToken)
    {
        var setting = await _context.AppSettings.FirstOrDefaultAsync(s => s.Key == key, cancellationToken);
        if (setting is null) return;

        List<string> list;
        try
        {
            list = JsonSerializer.Deserialize<List<string>>(setting.Value) ?? [];
        }
        catch
        {
            return;
        }

        var changed = false;
        foreach (var item in canonical)
        {
            if (list.Any(x => string.Equals(x, item, StringComparison.OrdinalIgnoreCase))) continue;
            list.Add(item);
            changed = true;
        }

        if (!changed) return;
        setting.Value = JsonSerializer.Serialize(list);
    }

    /// <summary>
    /// Options offered to users. Salary, wage and salary-advance expense types are left out because the guard rejects them;
    /// they stay in the saved list (no silent rewrite) and existing expenses keep their type.
    /// </summary>
    public async Task<IReadOnlyList<string>> GetListAsync(string key, CancellationToken cancellationToken = default)
    {
        var list = await ReadStoredListAsync(key, cancellationToken);
        return key == LookupSettingKeys.ExpenseTypes
            ? list.Where(t => SalaryExpenseRules.Classify(t) == SalaryExpenseKind.None
                && !FinanceExpenseRules.IsDuplicateVaccinePurchase(t)).ToArray()
            : list;
    }

    private async Task<IReadOnlyList<string>> ReadStoredListAsync(string key, CancellationToken cancellationToken)
    {
        await EnsureDefaultsAsync(cancellationToken);
        var setting = await _context.AppSettings.AsNoTracking()
            .FirstOrDefaultAsync(s => s.Key == key, cancellationToken);

        if (setting is null)
            return Defaults.GetValueOrDefault(key, []);

        try
        {
            return JsonSerializer.Deserialize<string[]>(setting.Value) ?? [];
        }
        catch
        {
            return Defaults.GetValueOrDefault(key, []);
        }
    }

    public async Task<IReadOnlyList<string>> AddOptionAsync(string key, string value, CancellationToken cancellationToken = default)
    {
        var trimmed = value.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
            throw new ArgumentException("Value is required.");

        if (!Defaults.ContainsKey(key))
            throw new ArgumentException("Unknown lookup key.");
        if (key == LookupSettingKeys.ExpenseTypes && SalaryExpenseRules.Classify(trimmed) != SalaryExpenseKind.None)
            throw new ArgumentException(SalaryExpenseRules.SalaryBlockedMessage);
        if (key == LookupSettingKeys.ExpenseTypes && FinanceExpenseRules.IsDuplicateVaccinePurchase(trimmed))
            throw new ArgumentException("Vaccine purchases are recorded on the Health tab — do not add a separate \"Vaccines\" expense.");

        var list = (await ReadStoredListAsync(key, cancellationToken)).ToList();
        if (!list.Contains(trimmed, StringComparer.OrdinalIgnoreCase))
            list.Add(trimmed);

        var setting = await _context.AppSettings.FirstOrDefaultAsync(s => s.Key == key, cancellationToken);
        if (setting is null)
        {
            setting = new AppSetting { Key = key, Value = JsonSerializer.Serialize(list) };
            _context.AppSettings.Add(setting);
        }
        else
        {
            setting.Value = JsonSerializer.Serialize(list);
            setting.UpdatedDate = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync(cancellationToken);
        return await GetListAsync(key, cancellationToken);
    }
}
