using GoatFarm.Application.Common;
using GoatFarm.Domain.Entities;

namespace GoatFarm.Application.Interfaces;

/// <summary>Milk cost per litre with estimate flag when based on fewer than 7 milking days.</summary>
public sealed record FeedCostPerLitreResult(decimal? CostPerLitre, bool IsEstimate);

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

    /// <summary>
    /// Milk cost/L: Products › Pricing landed cost when set on the milk product;
    /// otherwise milking-group feed cost ÷ litres. <see cref="FeedCostPerLitreResult.IsEstimate"/>
    /// is true until there are 7+ distinct milking days (feed-derived path only).
    /// </summary>
    Task<FeedCostPerLitreResult> FeedCostPerLitreAsync(
        DateOnly monthStart,
        int? productId = null,
        CancellationToken cancellationToken = default);

    Task<(int Scanned, int Pregnant, int Waiting, decimal? Rate)> PregnancyRateAsync(
        int? buckId = null,
        string? birthBatchName = null,
        CancellationToken cancellationToken = default);
}