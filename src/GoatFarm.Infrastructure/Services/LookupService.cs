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
            [LookupSettingKeys.VaccineUnits] = LookupConstants.VaccineUnits
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

        await _context.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Options offered to users. Salary, wage and salary-advance expense types are left out because the guard rejects them;
    /// they stay in the saved list (no silent rewrite) and existing expenses keep their type.
    /// </summary>
    public async Task<IReadOnlyList<string>> GetListAsync(string key, CancellationToken cancellationToken = default)
    {
        var list = await ReadStoredListAsync(key, cancellationToken);
        return key == LookupSettingKeys.ExpenseTypes
            ? list.Where(t => SalaryExpenseRules.Classify(t) == SalaryExpenseKind.None).ToArray()
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
