using GoatFarm.Application.Common;
using GoatFarm.Application.Interfaces;
using GoatFarm.Application.ViewModels.Breeding;
using GoatFarm.Domain.Entities;
using GoatFarm.Domain.Enums;
using GoatFarm.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GoatFarm.Infrastructure.Services;

public class BreedingService : IBreedingService
{
    private readonly GoatFarmDbContext _context;
    private readonly IGoatService _goatService;
    private readonly IFeedService _feedService;

    public BreedingService(GoatFarmDbContext context, IGoatService goatService, IFeedService feedService)
    {
        _context = context;
        _goatService = goatService;
        _feedService = feedService;
    }

    public async Task<BreedingPageViewModel> GetBreedingPageAsync(CancellationToken cancellationToken = default)
    {
        var goats = await _context.Goats.AsNoTracking().OrderBy(g => g.Tag).ToListAsync(cancellationToken);
        var prep = goats.Where(g => g.PrepCrossDate.HasValue && !g.MatedDate.HasValue).ToList();
        var exp = goats.Where(g => g.MatedDate.HasValue).ToList();

        int? soonest = exp.Count > 0
            ? exp.Min(g => BreedingHelper.DaysUntil(BreedingHelper.ExpectedKidding(g.MatedDate!.Value)))
            : null;

        var emptyLogs = await _context.BreedingEmptyLogs.AsNoTracking()
            .Include(l => l.Goat)
            .OrderByDescending(l => l.ScanDate)
            .ToListAsync(cancellationToken);

        var emptyByGoat = emptyLogs.GroupBy(l => l.GoatId).ToDictionary(g => g.Key, g => g.Count());
        var totalScans = exp.Count(e => e.KidsCount.HasValue) + emptyLogs.Count;
        var totalEmpty = emptyLogs.Count;
        var emptyRateText = totalScans > 0
            ? $"{totalEmpty} empty of {totalScans} scans ({Math.Round(totalEmpty / (double)totalScans * 100)}%)"
            : "no scans recorded yet";

        var emptyRows = emptyLogs.Select(l =>
        {
            var g = l.Goat;
            var (text, css) = DisplayHelper.GetStatusDisplay(g.Status);
            return new BreedingEmptyRowViewModel
            {
                Tag = g.Tag,
                Name = g.Name,
                MatedDate = l.MatedDate?.ToString("yyyy-MM-dd"),
                BuckTag = l.BuckTag,
                ScanDate = l.ScanDate.ToString("yyyy-MM-dd"),
                TimesEmpty = emptyByGoat.GetValueOrDefault(g.Id),
                StatusDisplay = text,
                StatusCssClass = css
            };
        }).ToList();

        var waiting = exp.Where(g => !g.KidsCount.HasValue).ToList();
        var confirmed = exp.Where(g => g.KidsCount.HasValue && g.KidsCount > 0).ToList();
        var plans = await _context.FeedPlans.AsNoTracking().ToListAsync(cancellationToken);
        var mixCostPerKg = _feedService.GetMixCostPerKg();
        var rationRows = BuildRationRows(prep, waiting, confirmed, plans, mixCostPerKg);

        return new BreedingPageViewModel
        {
            PrepCount = prep.Count,
            ExpectingCount = confirmed.Count,
            NextDueText = soonest switch
            {
                null => "next due —",
                < 0 => $"one overdue by {-soonest.Value} days",
                _ => $"next due in {soonest.Value} days"
            },
            EmptyRateText = emptyRateText,
            PrepRows = prep.Select(MapPrep).ToList(),
            ExpectingRows = confirmed
                .OrderBy(g => BreedingHelper.ExpectedKidding(g.MatedDate!.Value))
                .Select(MapExpecting)
                .ToList(),
            EmptyRows = emptyRows,
            RationRows = rationRows
        };
    }

    public async Task RecordPrepAsync(RecordPrepViewModel model, CancellationToken cancellationToken = default)
    {
        var goat = await FindGoatAsync(model.Tag, cancellationToken)
            ?? throw new InvalidOperationException("Scan or type a valid doe tag");
        goat.PrepCrossDate = model.Date;
        goat.UpdatedDate = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task RecordCrossAsync(RecordCrossViewModel model, CancellationToken cancellationToken = default)
    {
        var goat = await FindGoatAsync(model.Tag, cancellationToken)
            ?? throw new InvalidOperationException("Scan or type a valid doe tag");
        goat.MatedDate = model.Date;
        goat.BuckTag = string.IsNullOrWhiteSpace(model.BuckTag) ? null : model.BuckTag.Trim();
        goat.PrepCrossDate = null;
        goat.Status = GoatStatus.Pregnant;
        goat.UpdatedDate = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task RecordUltrasoundAsync(RecordUltrasoundViewModel model, CancellationToken cancellationToken = default)
    {
        var goat = await FindGoatAsync(model.Tag, cancellationToken)
            ?? throw new InvalidOperationException("Scan or type a valid doe tag");

        if (model.KidsCount == 0)
        {
            var scanDate = model.Date ?? DateOnly.FromDateTime(DateTime.Today);
            _context.BreedingEmptyLogs.Add(new BreedingEmptyLog
            {
                GoatId = goat.Id,
                ScanDate = scanDate,
                MatedDate = goat.MatedDate,
                BuckTag = goat.BuckTag
            });
            goat.MatedDate = null;
            goat.BuckTag = null;
            goat.KidsCount = null;
            goat.UltrasoundDate = null;
            goat.PrepCrossDate = null;
            goat.Status = GoatStatus.Dry;
        }
        else
        {
            if (!goat.MatedDate.HasValue)
                throw new InvalidOperationException("Record her cross first (she must be in the Expecting list).");
            goat.KidsCount = model.KidsCount;
            goat.UltrasoundDate = model.Date ?? DateOnly.FromDateTime(DateTime.Today);
            goat.Status = GoatStatus.Pregnant;
        }

        goat.UpdatedDate = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task MarkKiddedAsync(int goatId, CancellationToken cancellationToken = default)
    {
        var goat = await _context.Goats.FindAsync([goatId], cancellationToken)
            ?? throw new InvalidOperationException("Goat not found");
        goat.MatedDate = null;
        goat.BuckTag = null;
        goat.KidsCount = null;
        goat.UltrasoundDate = null;
        goat.PrepCrossDate = null;
        goat.Status = GoatStatus.Milking;
        goat.UpdatedDate = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task RemovePrepAsync(int goatId, CancellationToken cancellationToken = default)
    {
        var goat = await _context.Goats.FindAsync([goatId], cancellationToken);
        if (goat is null) return;
        goat.PrepCrossDate = null;
        goat.UpdatedDate = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task RemoveCrossAsync(int goatId, CancellationToken cancellationToken = default)
    {
        var goat = await _context.Goats.FindAsync([goatId], cancellationToken);
        if (goat is null) return;
        goat.MatedDate = null;
        goat.BuckTag = null;
        goat.KidsCount = null;
        goat.UltrasoundDate = null;
        goat.UpdatedDate = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task RecordCrossFromPrepAsync(int goatId, DateOnly date, string? buckTag, CancellationToken cancellationToken = default)
    {
        var goat = await _context.Goats.FindAsync([goatId], cancellationToken)
            ?? throw new InvalidOperationException("Goat not found");
        goat.MatedDate = date;
        goat.BuckTag = string.IsNullOrWhiteSpace(buckTag) ? null : buckTag.Trim();
        goat.PrepCrossDate = null;
        goat.Status = GoatStatus.Pregnant;
        goat.UpdatedDate = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);
    }

    private async Task<Goat?> FindGoatAsync(string tag, CancellationToken cancellationToken)
    {
        var vm = await _goatService.GetByTagAsync(tag, cancellationToken);
        if (vm is null) return null;
        return await _context.Goats.FindAsync([vm.Id], cancellationToken);
    }

    private static BreedingPrepRowViewModel MapPrep(Goat g)
    {
        var prep = g.PrepCrossDate!.Value;
        var start = prep.AddDays(-BreedingHelper.PrepDietLeadDays);
        var dCross = BreedingHelper.DaysUntil(prep);
        var dStart = BreedingHelper.DaysUntil(start);
        var (text, css) = DisplayHelper.GetStatusDisplay(g.Status);
        return new BreedingPrepRowViewModel
        {
            Id = g.Id,
            Tag = g.Tag,
            Name = g.Name,
            StatusDisplay = text,
            StatusCssClass = css,
            PrepCrossDate = prep.ToString("yyyy-MM-dd"),
            DietStartDate = start.ToString("yyyy-MM-dd") + (dStart <= 0 ? " · start now" : ""),
            DietStartNow = dStart <= 0,
            CrossInText = dCross < 0 ? "now" : $"in {dCross}d"
        };
    }

    private static List<BreedingRationRowViewModel> BuildRationRows(
        IReadOnlyList<Goat> prep,
        IReadOnlyList<Goat> waiting,
        IReadOnlyList<Goat> confirmed,
        IReadOnlyList<FeedPlan> plans,
        decimal mixCostPerKg)
    {
        var rows = new List<BreedingRationRowViewModel>();
        foreach (var g in prep)
            rows.Add(MapRation(g, "Ready for cross", GoatStatus.Dry, plans, mixCostPerKg, g.PrepCrossDate));
        foreach (var g in waiting)
            rows.Add(MapRation(g, "Crossed — awaiting scan", GoatStatus.Dry, plans, mixCostPerKg, null));
        foreach (var g in confirmed)
            rows.Add(MapRation(g, "Confirmed pregnant", GoatStatus.Pregnant, plans, mixCostPerKg, null, g.KidsCount));
        return rows;
    }

    private static BreedingRationRowViewModel MapRation(
        Goat g,
        string stage,
        GoatStatus planStatus,
        IReadOnlyList<FeedPlan> plans,
        decimal mixCostPerKg,
        DateOnly? prepCrossDate,
        int? kidsCount = null)
    {
        var plan = plans.FirstOrDefault(p => p.StatusKey == planStatus)
            ?? plans.FirstOrDefault(p => p.StatusKey == GoatStatus.Dry);
        var mix = plan?.MixKgPerDay ?? 0;
        var fodder = plan?.FodderKgPerDay ?? 0;
        var dailyCost = mix * mixCostPerKg;

        var note = "";
        if (prepCrossDate.HasValue)
        {
            var start = prepCrossDate.Value.AddDays(-BreedingHelper.PrepDietLeadDays);
            var dStart = BreedingHelper.DaysUntil(start);
            note = dStart <= 0
                ? "build-up feeding now"
                : $"build-up from {start:yyyy-MM-dd}";
        }
        else if (kidsCount >= 2)
            note = $"↑ {BreedingHelper.KidsLabel(kidsCount.Value)} — extra feed";
        else if (kidsCount == 1)
            note = "single kid";

        return new BreedingRationRowViewModel
        {
            Tag = g.Tag,
            Name = g.Name,
            Stage = stage,
            MixKgPerDay = mix,
            FodderKgPerDay = fodder,
            DailyCost = dailyCost,
            Note = note
        };
    }

    private static BreedingExpectingRowViewModel MapExpecting(Goat g)
    {
        var mated = g.MatedDate!.Value;
        var kd = BreedingHelper.ExpectedKidding(mated);
        var w1 = mated.AddDays(BreedingHelper.KiddingWindowStart);
        var w2 = mated.AddDays(BreedingHelper.KiddingWindowEnd);
        var du = BreedingHelper.DaysUntil(kd);
        var dueColor = du < 0 ? "color:#8a261c" : du <= 10 ? "color:var(--amber)" : "color:var(--green-dark)";

        string kidsDisplay;
        bool extraFeed;
        if (!g.KidsCount.HasValue)
        {
            kidsDisplay = "not checked";
            extraFeed = false;
        }
        else
        {
            kidsDisplay = BreedingHelper.KidsLabel(g.KidsCount.Value);
            extraFeed = g.KidsCount >= 2;
        }

        return new BreedingExpectingRowViewModel
        {
            Id = g.Id,
            Tag = g.Tag,
            Name = g.Name,
            MatedDate = mated.ToString("yyyy-MM-dd"),
            BuckTag = g.BuckTag,
            KidsCount = g.KidsCount,
            UltrasoundDate = g.UltrasoundDate?.ToString("yyyy-MM-dd"),
            KidsDisplay = kidsDisplay,
            ExtraFeed = extraFeed,
            ExpectedKidding = kd.ToString("yyyy-MM-dd"),
            KiddingWindow = $"{w1:yyyy-MM-dd} → {w2:yyyy-MM-dd}",
            DueText = BreedingHelper.DueText(du),
            DueColor = dueColor
        };
    }
}
