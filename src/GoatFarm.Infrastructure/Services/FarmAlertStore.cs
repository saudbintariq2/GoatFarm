using System.Text.Json;
using GoatFarm.Domain.Constants;
using GoatFarm.Domain.Entities;
using GoatFarm.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GoatFarm.Infrastructure.Services;

public sealed class FarmAlert
{
    public string Key { get; set; } = string.Empty;
    public string Severity { get; set; } = "warning";
    public string Message { get; set; } = string.Empty;
    public string? Href { get; set; }
    public DateTime RaisedUtc { get; set; }
}

/// <summary>
/// Server-raised alerts (failed backup, feed ran out) kept in one setting row so they survive restarts.
/// One alert per key: raising an existing key only refreshes its text, so the farmer is not told twice.
/// </summary>
public static class FarmAlertStore
{
    public static async Task<List<FarmAlert>> LoadAsync(GoatFarmDbContext context, CancellationToken cancellationToken = default)
    {
        var row = await context.AppSettings.AsNoTracking()
            .FirstOrDefaultAsync(s => s.Key == AppSettingKeys.FarmAlerts, cancellationToken);
        return Parse(row?.Value);
    }

    /// <returns>True when the alert is new; false when the same key was already active.</returns>
    public static async Task<bool> RaiseAsync(GoatFarmDbContext context, FarmAlert alert, CancellationToken cancellationToken = default)
    {
        var row = await GetRowAsync(context, cancellationToken);
        var alerts = Parse(row.Value);
        var existing = alerts.FirstOrDefault(a => a.Key == alert.Key);
        if (existing is not null)
        {
            existing.Severity = alert.Severity;
            existing.Message = alert.Message;
            existing.Href = alert.Href;
        }
        else
        {
            alert.RaisedUtc = alert.RaisedUtc == default ? DateTime.UtcNow : alert.RaisedUtc;
            alerts.Add(alert);
        }
        row.Value = JsonSerializer.Serialize(alerts);
        await context.SaveChangesAsync(cancellationToken);
        return existing is null;
    }

    public static async Task ClearAsync(GoatFarmDbContext context, string key, CancellationToken cancellationToken = default)
    {
        var row = await context.AppSettings.FirstOrDefaultAsync(s => s.Key == AppSettingKeys.FarmAlerts, cancellationToken);
        if (row is null) return;
        var alerts = Parse(row.Value);
        if (alerts.RemoveAll(a => a.Key == key) == 0) return;
        row.Value = JsonSerializer.Serialize(alerts);
        await context.SaveChangesAsync(cancellationToken);
    }

    public static async Task<bool> IsActiveAsync(GoatFarmDbContext context, string key, CancellationToken cancellationToken = default) =>
        (await LoadAsync(context, cancellationToken)).Any(a => a.Key == key);

    private static async Task<AppSetting> GetRowAsync(GoatFarmDbContext context, CancellationToken cancellationToken)
    {
        var row = context.AppSettings.Local.FirstOrDefault(s => s.Key == AppSettingKeys.FarmAlerts)
            ?? await context.AppSettings.FirstOrDefaultAsync(s => s.Key == AppSettingKeys.FarmAlerts, cancellationToken);
        if (row is not null) return row;
        row = new AppSetting { Key = AppSettingKeys.FarmAlerts, Value = "[]" };
        context.AppSettings.Add(row);
        return row;
    }

    private static List<FarmAlert> Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return [];
        try { return JsonSerializer.Deserialize<List<FarmAlert>>(json) ?? []; }
        catch (JsonException) { return []; }
    }
}
