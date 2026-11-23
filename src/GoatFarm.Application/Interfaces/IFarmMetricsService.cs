using GoatFarm.Application.Common;
using GoatFarm.Domain.Entities;

namespace GoatFarm.Application.Interfaces;

/// <summary>Step 0 — single source for milk, finance totals, feed cost/litre, and doe stage counts.</summary>
public interface IFarmMetricsService
{
    Task<IReadOnlyDictionary<int, DoeStageResult>> DoeStagesAsync(
        DateOnly? today = null,
        IReadOnlyCollection<int>? onlyDoeIds = null,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyDictionary<int, DoeStageResult>> DoeStagesForGoatsAsync(
        IReadOnlyList<Goat> does,
        DateOnly? today = null,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyDictionary<FarmDoeStage, int>> DoeStageCountsAsync(DateOnly? today = null, CancellationToken cancellationToken = default);

    Task<Dictionary<string, int>> DoeFlowKeyCountsAsync(DateOnly? today = null, CancellationToken cancellationToken = default);

    Task<BreedingStageCountSummary> BreedingStageSummaryAsync(DateOnly? today = null, CancellationToken cancellationToken = default);

    Task<decimal> MilkLitresAsync(DateOnly from, DateOnly toInclusive, CancellationToken cancellationToken = default);

    Task<IReadOnlyDictionary<DateOnly, decimal>> MilkByDayAsync(DateOnly from, DateOnly toInclusive, CancellationToken cancellationToken = default);

    Task<decimal> IncomeAsync(DateOnly from, DateOnly toInclusive, CancellationToken cancellationToken = default);

    Task<decimal> ExpenseAsync(DateOnly from, DateOnly toInclusive, CancellationToken cancellationToken = default);

    Task<(decimal PerDay, int GoatCount)> FeedCostPerDayAsync(CancellationToken cancellationToken = default);

    Task<decimal?> FeedCostPerLitreAsync(DateOnly monthStart, CancellationToken cancellationToken = default);

    Task<(int Scanned, int Pregnant, int Waiting, decimal? Rate)> PregnancyRateAsync(
        int? buckId = null,
        string? birthBatchName = null,
        CancellationToken cancellationToken = default);
}