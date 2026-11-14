using GoatFarm.Application.ViewModels.Breeding;
using GoatFarm.Application.ViewModels.Goats;
using GoatFarm.Domain.Entities;
using GoatFarm.Domain.Enums;

namespace GoatFarm.Application.Common;

public sealed class HerdBreedingContext
{
    public IReadOnlyDictionary<int, int> KiddingsHere { get; init; } = new Dictionary<int, int>();
    public IReadOnlyDictionary<int, KiddingRecord> LatestKidding { get; init; } = new Dictionary<int, KiddingRecord>();
    public IReadOnlyDictionary<int, KiddingRecord[]> KiddingsByDoe { get; init; } = new Dictionary<int, KiddingRecord[]>();
    public IReadOnlyDictionary<int, (int Born, int LiveAtBirth)> LitterTotals { get; init; } = new Dictionary<int, (int, int)>();
    public IReadOnlyDictionary<int, int> KidsAliveNow { get; init; } = new Dictionary<int, int>();
    public IReadOnlyDictionary<int, int> TimesPregnant { get; init; } = new Dictionary<int, int>();
    public IReadOnlyDictionary<string, MotherGroupDto> MotherGroups { get; init; } = new Dictionary<string, MotherGroupDto>();
    public HashSet<int> LowMotherScoreDoeIds { get; init; } = [];
    public HashSet<int> HighMotherScoreDoeIds { get; init; } = [];
    public HashSet<int> CullDecisionDoeIds { get; init; } = [];
}

public static class HerdBreedingEnricher
{
    public static HerdBreedingContext Build(
        IReadOnlyList<KiddingRecord> allKiddings,
        IReadOnlyList<BreedingEvent> allEvents,
        IReadOnlyList<Goat> liveKidsByDam,
        IReadOnlyList<MotherGroupDto> motherGroups)
    {
        var groups = motherGroups.ToDictionary(g => g.Id, StringComparer.OrdinalIgnoreCase);
        var byDoe = allKiddings.GroupBy(k => k.DoeId).ToDictionary(g => g.Key, g => g.ToArray());
        var kiddingsHere = byDoe.ToDictionary(kv => kv.Key, kv => kv.Value.Length);
        var latest = byDoe.ToDictionary(kv => kv.Key, kv => kv.Value.OrderByDescending(k => k.KiddingDate).First());
        var litterTotals = byDoe.ToDictionary(
            kv => kv.Key,
            kv => (Born: kv.Value.Sum(x => x.LiveKids + x.Stillborn), LiveAtBirth: kv.Value.Sum(x => x.LiveKids)));
        var kidsAlive = liveKidsByDam
            .Where(g => g.DamId is { } did)
            .GroupBy(g => g.DamId!.Value)
            .ToDictionary(g => g.Key, g => g.Count());
        var timesPregnant = allEvents
            .Where(e => e.Outcome is BreedingOutcome.ConfirmedPregnant or BreedingOutcome.Kidded || e.ScanKids is > 0)
            .GroupBy(e => e.DoeId)
            .ToDictionary(g => g.Key, g => g.Count());

        var lowScore = latest.Values
            .Where(k => MotherScoreHelper.ScoreForKidding(k) is { } s && s < 3.0)
            .Select(k => k.DoeId)
            .ToHashSet();
        var highScore = byDoe
            .Where(kv => MotherScoreHelper.DoeAverage(kv.Value) is { } s && s >= 4.0)
            .Select(kv => kv.Key)
            .ToHashSet();
        var cull = latest.Values.Where(k => k.KeepDecision == DamKeepDecision.Cull).Select(k => k.DoeId).ToHashSet();

        return new HerdBreedingContext
        {
            KiddingsHere = kiddingsHere,
            LatestKidding = latest,
            KiddingsByDoe = byDoe,
            LitterTotals = litterTotals,
            KidsAliveNow = kidsAlive,
            TimesPregnant = timesPregnant,
            MotherGroups = groups,
            LowMotherScoreDoeIds = lowScore,
            HighMotherScoreDoeIds = highScore,
            CullDecisionDoeIds = cull
        };
    }

    public static void Apply(GoatViewModel vm, Goat g, HerdBreedingContext ctx, DateOnly today)
    {
        vm.KiddingsBeforePurchase = g.KiddingsBeforePurchase;

        if (g.Status == GoatStatus.Kid)
        {
            vm.KidMotherTag = g.Dam?.Tag ?? g.DamTagText;
            vm.KidMilkTypeDisplay = g.KidMilkType.ToString();
            vm.KidMilkStatus = g.WeanDate.HasValue ? "Weaned" : "On milk";
            vm.KidWeanDateDisplay = g.WeanDate?.ToString("yyyy-MM-dd");
            return;
        }

        if (g.Gender != GoatGender.Female)
            return;

        var flow = BreedingDoeFlowHelper.FlowStage(g, today) ?? "getting_ready";
        vm.HerdBreedingStatus = BreedingDoeFlowHelper.FlowStageLabel(flow);
        vm.HerdBreedingStatusCss = BreedingDoeFlowHelper.FlowStageChipClass(flow);
        vm.HerdNextJob = vm.NextStepLabel;

        var here = ctx.KiddingsHere.GetValueOrDefault(g.Id);
        vm.KiddingsTotal = here + g.KiddingsBeforePurchase;
        vm.TimesPregnant = ctx.TimesPregnant.GetValueOrDefault(g.Id);

        if (ctx.LitterTotals.TryGetValue(g.Id, out var lit))
        {
            vm.KidsBornTotal = lit.Born;
            vm.KidsAliveTotal = ctx.KidsAliveNow.GetValueOrDefault(g.Id, lit.LiveAtBirth);
        }

        if (ctx.LatestKidding.TryGetValue(g.Id, out var lk))
        {
            vm.LastKiddingDate = lk.KiddingDate.ToString("yyyy-MM-dd");
            vm.LastLitterDisplay = $"{lk.LiveKids + lk.Stillborn} born · {lk.LiveKids} alive";
            vm.LatestBcs = lk.BodyCondition;
            vm.DamAfterKiddingHealth = lk.DamHealth.ToString();
            vm.KeepDecision = lk.KeepDecision.ToString();
        }

        if (ctx.KiddingsByDoe.TryGetValue(g.Id, out var kd))
        {
            var avg = MotherScoreHelper.DoeAverage(kd);
            vm.MotherScoreHtml = avg.HasValue ? MotherScoreHelper.StarsHtml(avg) : null;
        }

        if (!string.IsNullOrWhiteSpace(g.MotherGroupId) &&
            ctx.MotherGroups.TryGetValue(g.MotherGroupId, out var mg))
        {
            vm.MotherGroupName = mg.Name;
            vm.MotherGroupColor = mg.Color;
        }
    }
}