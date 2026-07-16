using System.Text.Json;
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

    public async Task<IReadOnlyList<string>> GetListAsync(string key, CancellationToken cancellationToken = default)
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

        await EnsureDefaultsAsync(cancellationToken);
        var list = (await GetListAsync(key, cancellationToken)).ToList();
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
        return list;
    }
}
