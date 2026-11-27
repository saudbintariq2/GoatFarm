using System.Text.Json;
using GoatFarm.Application.Common;
using GoatFarm.Application.Interfaces;
using GoatFarm.Application.ViewModels.Breeding;
using GoatFarm.Domain.Constants;
using GoatFarm.Domain.Entities;
using GoatFarm.Domain.Enums;
using GoatFarm.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace GoatFarm.Infrastructure.Services;

public class FarmMetricsService : IFarmMetricsService
{
    private readonly GoatFarmDbContext _db;
    private readonly IGoatService _goats;
    private readonly IServiceProvider _services;

    public FarmMetricsService(GoatFarmDbContext db, IGoatService goats, IServiceProvider services)
    {
        _db = db;
        _goats = goats;
        _services = services;
    }

    public async Task<IReadOnlyDictionary<int, DoeStageResult>> DoeStagesAsync(
        DateOnly? today = null,
        IReadOnlyCollection<int>? onlyDoeIds = null,
        CancellationToken cancellationToken = default)
    {
        var day = today ?? DateOnly.FromDateTime(DateTime.Today);
        var query = _db.Goats.AsNoTracking()
            .Include(g => g.HeatEvents)
            .Where(g => !g.IsArchived && g.Gender == GoatGender.Female && g.Status != GoatStatus.Kid);
        if (onlyDoeIds is { Count: > 0 })
            query = query.Where(g => onlyDoeIds.Contains(g.Id));
        var does = await query.ToListAsync(cancellationToken);
        return await DoeStagesForGoatsAsync(does, day, cancellationToken);
    }

    public Task<IReadOnlyDictionary<int, DoeStageResult>> DoeStagesForGoatsAsync(
        IReadOnlyList<Goat> does,
        DateOnly? today = null,
        CancellationToken cancellationToken = default)
    {
        var day = today ?? DateOnly.FromDateTime(DateTime.Today);
        return ResolveDoeStagesAsync(does, day, cancellationToken);
    }

    private async Task<IReadOnlyDictionary<int, DoeStageResult>> ResolveDoeStagesAsync(
        IReadOnlyList<Goat> does,
        DateOnly day,
        CancellationToken cancellationToken)
    {
        if (does.Count == 0)
            return new Dictionary<int, DoeStageResult>();

        var ids = does.Select(d => d.Id).ToList();

        var crosses = await _db.BreedingEvents.AsNoTracking()
            .Where(b => ids.Contains(b.DoeId) && b.Outcome != BreedingOutcome.Cancelled)
            .GroupBy(b => b.DoeId)
            .Select(g => g.OrderByDescending(x => x.MatedDate).ThenByDescending(x => x.Id).First())
            .ToDictionaryAsync(x => x.DoeId, cancellationToken);

        var kiddings = await _db.KiddingRecords.AsNoTracking()
            .Where(k => ids.Contains(k.DoeId))
            .GroupBy(k => k.DoeId)
            .Select(g => g.OrderByDescending(x => x.KiddingDate).ThenByDescending(x => x.Id).First())
            .ToDictionaryAsync(x => x.DoeId, cancellationToken);

        var rescanDue = await _db.FarmTasks.AsNoTracking()
            .Where(t => t.Status == FarmTaskStatus.ToDo && t.Title.StartsWith("Rescan") && t.GoatId != null)
            .Where(t => ids.Contains(t.GoatId!.Value))
            .GroupBy(t => t.GoatId!.Value)
            .Select(g => new { GoatId = g.Key, Due = g.Min(x => x.DueDate) })
            .ToDictionaryAsync(x => x.GoatId, x => x.Due ?? day, cancellationToken);

        var goatsById = does.ToDictionary(d => d.Id);
        var groups = await LoadBreedingGroupsAsync(cancellationToken);
        var buckIn = BreedingGroupHelper.DoesWithBuckIn(groups, goatsById, day);

        var kiddingsHere = await _db.KiddingRecords.AsNoTracking()
            .Where(k => ids.Contains(k.DoeId))
            .GroupBy(k => k.DoeId)
            .Select(g => new { DoeId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.DoeId, x => x.Count, cancellationToken);

        var seasonCross = await NextSeasonCrossByBatchAsync(day, cancellationToken);

        return does.ToDictionary(
            d => d.Id,
            d => DoeStageResolver.Resolve(
                d,
                crosses.GetValueOrDefault(d.Id),
                kiddings.GetValueOrDefault(d.Id),
                day,
                buckIn,
                rescanDue.TryGetValue(d.Id, out var due) ? due : (DateOnly?)null,
                kiddingsHere.GetValueOrDefault(d.Id),
                NextSeasonCross(seasonCross, d.BirthBatchName)));
    }

    private static DateOnly? NextSeasonCross(
        (Dictionary<string, DateOnly> ByBatch, DateOnly? Earliest) season, string? birthBatchName) =>
        !string.IsNullOrWhiteSpace(birthBatchName) && season.ByBatch.TryGetValue(birthBatchName, out var own)
            ? own
            : season.Earliest;

    /// <summary>Start of each season batch's next crossing window that has not ended yet.</summary>
    private async Task<(Dictionary<string, DateOnly> ByBatch, DateOnly? Earliest)> NextSeasonCrossByBatchAsync(
        DateOnly day, CancellationToken cancellationToken)
    {
        var byBatch = new Dictionary<string, DateOnly>(StringComparer.OrdinalIgnoreCase);
        var row = await _db.AppSettings.AsNoTracking()
            .FirstOrDefaultAsync(s => s.Key == AppSettingKeys.BreedingSeason, cancellationToken);
        if (row?.Value is null) return (byBatch, null);
        BreedingSeasonSettings? season;
        try { season = JsonSerializer.Deserialize<BreedingSeasonSettings>(row.Value); }
        catch (JsonException) { return (byBatch, null); }
        if (season is null || season.Batches.Count == 0) return (byBatch, null);

        foreach (var batch in season.Batches)
        {
            var plan = BreedingSeasonHelper.SeasonPlan(batch, season, day);
            if (plan.CrossTo < day) continue;
            var start = plan.CrossFrom < day ? day : plan.CrossFrom;
            if (!string.IsNullOrWhiteSpace(batch.Name))
                byBatch[batch.Name] = start;
        }
        return (byBatch, byBatch.Count == 0 ? null : byBatch.Values.Min());
    }

    public async Task<IReadOnlyDictionary<FarmDoeStage, int>> DoeStageCountsAsync(
        DateOnly? today = null,
        CancellationToken cancellationToken = default)
    {
        var stages = await DoeStagesAsync(today, cancellationToken: cancellationToken);
        return stages.Values
            .GroupBy(v => v.Stage)
            .ToDictionary(g => g.Key, g => g.Count());
    }

    public async Task<Dictionary<string, int>> DoeFlowKeyCountsAsync(
        DateOnly? today = null,
        CancellationToken cancellationToken = default)
    {
        var stages = await DoeStagesAsync(today, cancellationToken: cancellationToken);
        var counts = BreedingDoeFlowHelper.Steps.ToDictionary(s => s.Key, _ => 0);
        foreach (var r in stages.Values)
        {
            if (counts.ContainsKey(r.StatusKey))
                counts[r.StatusKey]++;
        }
        return counts;
    }

    public async Task<BreedingStageCountSummary> BreedingStageSummaryAsync(
        DateOnly? today = null,
        CancellationToken cancellationToken = default)
    {
        var counts = await DoeStageCountsAsync(today, cancellationToken);
        return DoeStageResolver.SummarizeCounts(counts);
    }

    public async Task<decimal> MilkLitresAsync(DateOnly from, DateOnly toInclusive, CancellationToken cancellationToken = default)
    {
        var legacy = await _db.MilkProductions.AsNoTracking()
            .Where(m => m.Date >= from && m.Date <= toInclusive)
            .SumAsync(m => m.Liters, cancellationToken);
        var sessions = await _db.MilkingSessions.AsNoTracking()
            .Where(s => s.Date >= from && s.Date <= toInclusive)
            .Select(s => Math.Max(0, s.Liters - s.RejectedLiters))
            .SumAsync(cancellationToken);
        return legacy + sessions;
    }

    public async Task<IReadOnlyDictionary<DateOnly, decimal>> MilkByDayAsync(
        DateOnly from,
        DateOnly toInclusive,
        CancellationToken cancellationToken = default)
    {
        var byDay = new Dictionary<DateOnly, decimal>();
        var legacyRows = await _db.MilkProductions.AsNoTracking()
            .Where(m => m.Date >= from && m.Date <= toInclusive)
            .GroupBy(m => m.Date)
            .Select(g => new { Day = g.Key, Liters = g.Sum(x => x.Liters) })
            .ToListAsync(cancellationToken);
        foreach (var row in legacyRows)
            byDay[row.Day] = row.Liters;

        var sessionRows = await _db.MilkingSessions.AsNoTracking()
            .Where(s => s.Date >= from && s.Date <= toInclusive)
            .GroupBy(s => s.Date)
            .Select(g => new { Day = g.Key, Liters = g.Sum(x => Math.Max(0, x.Liters - x.RejectedLiters)) })
            .ToListAsync(cancellationToken);
        foreach (var row in sessionRows)
            byDay[row.Day] = byDay.GetValueOrDefault(row.Day) + row.Liters;

        return byDay;
    }

    public Task<decimal> IncomeAsync(DateOnly from, DateOnly toInclusive, CancellationToken cancellationToken = default)
        => _db.Incomes.AsNoTracking()
            .Where(e => e.Date >= from && e.Date <= toInclusive)
            .SumAsync(e => e.Amount, cancellationToken);

    public Task<decimal> ExpenseAsync(DateOnly from, DateOnly toInclusive, CancellationToken cancellationToken = default)
        => _db.Expenses.AsNoTracking()
            .Where(e => e.Date >= from && e.Date <= toInclusive)
            .SumAsync(e => e.Amount, cancellationToken);

    public Task<(decimal PerDay, int GoatCount)> FeedCostPerDayAsync(CancellationToken cancellationToken = default)
    {
        var feed = _services.GetRequiredService<IFeedService>();
        var perDay = feed.CalculateFarmFeedDaily();
        var goats = _goats.GetLiveStatusCounts().Values.Sum();
        return Task.FromResult((perDay, goats));
    }

    public async Task<decimal?> FeedCostPerLitreAsync(DateOnly monthStart, CancellationToken cancellationToken = default)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var monthEnd = monthStart.AddMonths(1).AddDays(-1);
        var to = today < monthEnd ? today : monthEnd;
        var litres = await MilkLitresAsync(monthStart, to, cancellationToken);
        if (litres <= 0)
            return null;

        var (perDay, _) = await FeedCostPerDayAsync(cancellationToken);
        var days = to.DayNumber - monthStart.DayNumber + 1;
        return Math.Round(perDay * days / litres, 1);
    }

    public async Task<(int Scanned, int Pregnant, int Waiting, decimal? Rate)> PregnancyRateAsync(
        int? buckId = null,
        string? birthBatchName = null,
        CancellationToken cancellationToken = default)
    {
        var q = _db.BreedingEvents.AsNoTracking().AsQueryable();
        if (buckId.HasValue)
            q = q.Where(b => b.BuckId == buckId.Value);
        if (!string.IsNullOrWhiteSpace(birthBatchName))
        {
            var batchDoes = await _db.Goats.AsNoTracking()
                .Where(g => g.BirthBatchName == birthBatchName)
                .Select(g => g.Id)
                .ToListAsync(cancellationToken);
            q = q.Where(b => batchDoes.Contains(b.DoeId));
        }

        var r = PregnancyRateCalculator.Compute(await q.ToListAsync(cancellationToken));
        return (r.Scanned, r.Pregnant, r.Waiting, r.Rate);
    }

    private async Task<List<BreedingGroupDto>> LoadBreedingGroupsAsync(CancellationToken cancellationToken)
    {
        var row = await _db.AppSettings.AsNoTracking()
            .FirstOrDefaultAsync(s => s.Key == AppSettingKeys.BreedingGroups, cancellationToken);
        if (row?.Value is null) return [];
        return JsonSerializer.Deserialize<List<BreedingGroupDto>>(row.Value) ?? [];
    }
}