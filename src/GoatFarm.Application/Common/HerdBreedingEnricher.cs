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
    public IReadOnlyDictionary<int, decimal?> LatestBcs { get; set; } = new Dictionary<int, decimal?>();
}

public static class HerdBreedingEnricher
{
    public static string? EffectiveMotherGroupId(Goat g, HerdBreedingContext ctx)
    {
        ctx.LatestKidding.TryGetValue(g.Id, out var latest);
        return MotherGroupHelper.EffectiveGroupId(g.MotherGroupId, latest);
    }

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
            KidsAliveNow = new Dictionary<int, int>(),
            TimesPregnant = timesPregnant,
            MotherGroups = groups,
            LowMotherScoreDoeIds = lowScore,
            HighMotherScoreDoeIds = highScore,
            CullDecisionDoeIds = cull
        };
    }

    public static void Apply(
        GoatViewModel vm,
        Goat g,
        HerdBreedingContext ctx,
        DateOnly today,
        DoeStageResult? stage = null)
    {
        vm.KiddingsBeforePurchase = g.KiddingsBeforePurchase;

        if (g.Status == GoatStatus.Kid)
        {
            vm.KidMotherTag = g.Dam?.Tag ?? g.DamTagText;
            vm.KidMilkTypeDisplay = KidLabels.MilkTypeLabel(g.KidMilkType);
            vm.KidMilkStatus = g.WeanDate.HasValue ? "Weaned" : "On milk";
            vm.KidWeanDateDisplay = g.WeanDate?.ToString("yyyy-MM-dd");
            vm.KidStageLine = KidLabels.KidMilkLine(g, today);
            return;
        }

        if (g.Gender != GoatGender.Female)
            return;

        stage ??= DoeStageResolver.Resolve(
            g,
            null,
            ctx.LatestKidding.GetValueOrDefault(g.Id),
            today,
            null,
            null,
            ctx.KiddingsHere.GetValueOrDefault(g.Id));

        vm.HerdBreedingStatus = stage.Label;
        vm.HerdBreedingStatusCss = BreedingDoeFlowHelper.FlowStageChipClass(stage.StatusKey);
        vm.HerdNextJob = stage.NextJob ?? "None";
        vm.HerdPregnantText = stage.IsPregnant
            ? $"Yes · due {stage.DueDate:dd MMM yyyy}"
            : stage.Stage is FarmDoeStage.WaitingScan or FarmDoeStage.Crossed or FarmDoeStage.RescanDue
                ? "Waiting scan"
                : "No";

        var here = ctx.KiddingsHere.GetValueOrDefault(g.Id);
        vm.KiddingsTotal = here + g.KiddingsBeforePurchase;
        vm.TimesPregnant = ctx.TimesPregnant.GetValueOrDefault(g.Id);

        if (ctx.LitterTotals.TryGetValue(g.Id, out var lit))
        {
            vm.KidsBornTotal = lit.Born;
            vm.KidsAliveTotal = lit.LiveAtBirth;
        }

        if (ctx.LatestKidding.TryGetValue(g.Id, out var lk))
        {
            vm.LastKiddingDate = lk.KiddingDate.ToString("yyyy-MM-dd");
            var born = lk.LiveKids + lk.Stillborn;
            vm.LastLitterDisplay = $"{born} born · {lk.LiveKids} alive";
            vm.DamAfterKiddingHealth = lk.DamHealth.ToString();
            vm.KeepDecision = lk.KeepDecision.ToString();
        }

        if (ctx.LatestBcs.TryGetValue(g.Id, out var bcs) && bcs.HasValue)
            vm.LatestBcs = (int)Math.Round(bcs.Value);

        if (ctx.KiddingsByDoe.TryGetValue(g.Id, out var kd))
        {
            var avg = MotherScoreHelper.DoeAverage(kd);
            vm.MotherScoreHtml = avg.HasValue ? MotherScoreHelper.StarsHtml(avg) : null;
        }

        var effectiveMg = EffectiveMotherGroupId(g, ctx);
        if (!string.IsNullOrWhiteSpace(effectiveMg) &&
            ctx.MotherGroups.TryGetValue(effectiveMg, out var mg))
        {
            vm.MotherGroupName = mg.Name;
            vm.MotherGroupColor = mg.Color;
        }
    }
}