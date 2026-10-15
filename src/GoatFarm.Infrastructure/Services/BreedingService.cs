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

    private static DateOnly Today => DateOnly.FromDateTime(DateTime.Today);

    public async Task<BreedingPageViewModel> GetBreedingPageAsync(CancellationToken cancellationToken = default)
    {
        var goats = await _context.Goats.AsNoTracking()
            .Where(g => !g.IsArchived)
            .OrderBy(g => g.Tag)
            .ToListAsync(cancellationToken);
        var prep = goats.Where(g => g.PrepCrossDate.HasValue && !g.MatedDate.HasValue).ToList();
        var exp = goats.Where(g => g.MatedDate.HasValue).ToList();
        var waiting = exp.Where(g => !g.KidsCount.HasValue).ToList();
        var confirmed = exp.Where(g => g.KidsCount > 0).ToList();

        int? soonest = confirmed.Count > 0
            ? confirmed.Min(g => BreedingHelper.DaysUntil(BreedingHelper.ExpectedKidding(g.MatedDate!.Value)))
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

        var plans = await _context.FeedPlans.AsNoTracking().ToListAsync(cancellationToken);
        var mixCostPerKg = _feedService.GetMixCostPerKg();
        var rationRows = BuildRationRows(prep, waiting, confirmed, plans, mixCostPerKg);

        return new BreedingPageViewModel
        {
            PrepCount = prep.Count,
            WaitingCount = waiting.Count,
            ExpectingCount = confirmed.Count,
            NextDueText = soonest switch
            {
                null => "next due —",
                < 0 => $"one overdue by {-soonest.Value} days",
                _ => $"next due in {soonest.Value} days"
            },
            EmptyRateText = emptyRateText,
            PrepRows = prep.Select(MapPrep).ToList(),
            WaitingRows = waiting
                .OrderBy(g => g.MatedDate)
                .Select(MapWaiting)
                .ToList(),
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
        var error = BreedingRules.EligibilityError(goat, Today);
        if (error is not null) throw new InvalidOperationException(error);

        goat.PrepCrossDate = model.Date;
        goat.BreedingStage = BreedingStage.Ready;
        goat.UpdatedDate = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<CrossCheckViewModel> CheckCrossAsync(
        string doeTag, string? buckTag, DateOnly? date, CancellationToken cancellationToken = default)
    {
        var doe = await FindGoatAsync(doeTag, cancellationToken);
        if (doe is null)
            return new CrossCheckViewModel { Errors = ["No goat with that tag."] };

        var buck = await BreedingCycleWriter.FindBuckAsync(_context, buckTag, cancellationToken);
        var pedigree = await BreedingCycleWriter.LoadPedigreeAsync(_context, cancellationToken);
        var plan = BreedingCycleWriter.PlanCross(doe, buck, buckTag, date ?? Today, Today, pedigree);
        return new CrossCheckViewModel
        {
            CanCross = plan.Check.Errors.Count == 0,
            DoeTag = doe.Tag,
            DoeName = doe.Name,
            BuckTag = plan.BuckTag,
            BuckInHerd = buck is not null,
            Errors = plan.Check.Errors,
            Warnings = plan.Check.Warnings,
            Notes = plan.Check.Notes
        };
    }

    public async Task RecordCrossAsync(RecordCrossViewModel model, CancellationToken cancellationToken = default)
    {
        var goat = await FindGoatAsync(model.Tag, cancellationToken)
            ?? throw new InvalidOperationException("Scan or type a valid doe tag");
        await CrossAsync(goat, model.Date, model.BuckTag, model.ConfirmWarnings, cancellationToken);
    }

    public async Task RecordCrossFromPrepAsync(
        int goatId, DateOnly date, string? buckTag, bool confirmWarnings, CancellationToken cancellationToken = default)
    {
        var goat = await _context.Goats.FindAsync([goatId], cancellationToken)
            ?? throw new InvalidOperationException("Goat not found");
        await CrossAsync(goat, date, buckTag, confirmWarnings, cancellationToken);
    }

    private async Task CrossAsync(Goat doe, DateOnly date, string? buckTag, bool confirmWarnings, CancellationToken cancellationToken)
    {
        var buck = await BreedingCycleWriter.FindBuckAsync(_context, buckTag, cancellationToken);
        var pedigree = await BreedingCycleWriter.LoadPedigreeAsync(_context, cancellationToken);
        var plan = BreedingCycleWriter.PlanCross(doe, buck, buckTag, date, Today, pedigree);

        if (plan.Check.Errors.Count > 0)
            throw new InvalidOperationException(string.Join(" ", plan.Check.Errors));
        if (plan.Check.Warnings.Count > 0 && !confirmWarnings)
            throw new ConfirmationRequiredException(plan.Check.Warnings);

        var open = await BreedingCycleWriter.OpenEventsAsync(_context, doe.Id, cancellationToken);
        BreedingCycleWriter.ApplyCross(_context, doe, date, plan, open);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task RecordUltrasoundAsync(RecordUltrasoundViewModel model, CancellationToken cancellationToken = default)
    {
        var goat = await FindGoatAsync(model.Tag, cancellationToken)
            ?? throw new InvalidOperationException("Scan or type a valid doe tag");
        var scanDate = model.Date ?? Today;

        if (scanDate > Today)
            throw new InvalidOperationException("The scan date cannot be in the future.");
        if (model.KidsCount < 0 || model.KidsCount > BreedingHelper.MaxKidsPerBirth)
            throw new InvalidOperationException($"Kids on scan must be between 0 and {BreedingHelper.MaxKidsPerBirth}.");
        if (goat.MatedDate.HasValue && scanDate < goat.MatedDate.Value)
            throw new InvalidOperationException($"The scan date is before her cross on {goat.MatedDate:yyyy-MM-dd}.");

        var open = await BreedingCycleWriter.OpenEventsAsync(_context, goat.Id, cancellationToken);

        if (model.KidsCount == 0)
        {
            _context.BreedingEmptyLogs.Add(new BreedingEmptyLog
            {
                GoatId = goat.Id,
                ScanDate = scanDate,
                MatedDate = goat.MatedDate,
                BuckTag = goat.BuckTag
            });
            foreach (var e in open)
            {
                e.ScanDate = scanDate;
                e.ScanKids = 0;
            }
            BreedingCycleWriter.CloseCycle(goat, open, BreedingOutcome.Empty, scanDate);
            if (goat.Status == GoatStatus.Pregnant)
                goat.Status = GoatStatus.Dry;
        }
        else
        {
            if (!goat.MatedDate.HasValue)
                throw new InvalidOperationException("Record her cross first (she must be in the Waiting for scan list).");
            goat.KidsCount = model.KidsCount;
            goat.UltrasoundDate = scanDate;
            goat.Status = GoatStatus.Pregnant;
            goat.BreedingStage = BreedingStage.ConfirmedPregnant;
            foreach (var e in open)
            {
                e.ScanDate = scanDate;
                e.ScanKids = model.KidsCount;
                e.Outcome = BreedingOutcome.ConfirmedPregnant;
                e.UpdatedDate = DateTime.UtcNow;
            }
        }

        goat.UpdatedDate = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<RecordBirthResultViewModel> RecordBirthAsync(RecordBirthViewModel model, CancellationToken cancellationToken = default)
    {
        if (model.ClientRequestId == Guid.Empty)
            throw new InvalidOperationException("Missing request id — reload the page and try again.");

        var existing = await _context.KiddingRecords.AsNoTracking()
            .Include(k => k.Kids)
            .FirstOrDefaultAsync(k => k.ClientRequestId == model.ClientRequestId, cancellationToken);
        if (existing is not null)
            return MapBirthResult(existing, alreadyRecorded: true);

        var doe = await FindGoatAsync(model.DoeTag, cancellationToken)
            ?? throw new InvalidOperationException("Scan or type a valid doe tag");
        if (doe.Gender != GoatGender.Female)
            throw new InvalidOperationException($"{doe.Tag} is not a doe.");

        var today = Today;
        if (model.KiddingDate > today)
            throw new InvalidOperationException("The birth date cannot be in the future.");
        if (model.Stillborn < 0 || model.Kids.Count + model.Stillborn == 0)
            throw new InvalidOperationException("Add at least one kid (live or stillborn).");
        if (model.Kids.Count + model.Stillborn > BreedingHelper.MaxKidsPerBirth)
            throw new InvalidOperationException($"A birth can have at most {BreedingHelper.MaxKidsPerBirth} kids.");
        if (model.Kids.Any(k => k.WeightKg is < 0 or > 20))
            throw new InvalidOperationException("Kid birth weight must be between 0 and 20 kg.");
        if (!Enum.IsDefined(model.Ease) || model.Kids.Any(k => !Enum.IsDefined(k.Gender)))
            throw new InvalidOperationException("Invalid birth details.");

        var warnings = new List<string>();
        if (!doe.MatedDate.HasValue)
            warnings.Add($"No cross is recorded for {doe.Tag}; the sire will be unknown.");
        else
        {
            if (model.KiddingDate < doe.MatedDate.Value)
                throw new InvalidOperationException($"The birth date is before her cross on {doe.MatedDate:yyyy-MM-dd}.");
            var gestation = model.KiddingDate.DayNumber - doe.MatedDate.Value.DayNumber;
            if (gestation < BreedingHelper.MinPlausibleGestationDays)
                warnings.Add($"Only {gestation} days since the cross (normal is about {BreedingHelper.GestationDays}).");
        }
        if (warnings.Count > 0 && !model.ConfirmWarnings)
            throw new ConfirmationRequiredException(warnings);

        var open = await BreedingCycleWriter.OpenEventsAsync(_context, doe.Id, cancellationToken);
        var currentEvent = open.FirstOrDefault();
        var tags = await AssignKidTagsAsync(doe.Tag, model.KiddingDate, model.Kids, cancellationToken);

        var record = new KiddingRecord
        {
            DoeId = doe.Id,
            BreedingEvent = currentEvent,
            KiddingDate = model.KiddingDate,
            LiveKids = model.Kids.Count,
            Stillborn = model.Stillborn,
            Ease = model.Ease,
            Notes = string.IsNullOrWhiteSpace(model.Notes) ? null : model.Notes.Trim(),
            ClientRequestId = model.ClientRequestId
        };
        _context.KiddingRecords.Add(record);

        for (var i = 0; i < model.Kids.Count; i++)
        {
            var input = model.Kids[i];
            var kid = new Goat
            {
                Tag = tags[i].Tag,
                IsTemporaryTag = tags[i].Temporary,
                Name = string.IsNullOrWhiteSpace(input.Name) ? null : input.Name.Trim(),
                Breed = doe.Breed,
                Gender = input.Gender,
                Status = GoatStatus.Kid,
                Source = GoatSource.Born,
                PurchasePrice = 0,
                EventDate = model.KiddingDate,
                GroupId = doe.GroupId,
                Owner = doe.Owner,
                DamId = doe.Id,
                SireId = currentEvent?.BuckId,
                SireTagText = currentEvent?.BuckId is null ? currentEvent?.BuckTagText ?? doe.BuckTag : null,
                KiddingRecord = record
            };
            if (input.WeightKg > 0)
                kid.WeightRecords.Add(new GoatWeightRecord { Date = model.KiddingDate, Kg = input.WeightKg.Value });
            record.Kids.Add(kid);
            _context.Goats.Add(kid);
        }

        BreedingCycleWriter.CloseCycle(doe, open, BreedingOutcome.Kidded, model.KiddingDate);
        doe.Status = GoatStatus.Milking;

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // A concurrent submit of the same birth won the unique ClientRequestId race; this save was rolled back whole.
            _context.ChangeTracker.Clear();
            var winner = await _context.KiddingRecords.AsNoTracking()
                .Include(k => k.Kids)
                .FirstOrDefaultAsync(k => k.ClientRequestId == model.ClientRequestId, cancellationToken);
            if (winner is null) throw;
            return MapBirthResult(winner, alreadyRecorded: true);
        }
        return MapBirthResult(record, alreadyRecorded: false);
    }

    public async Task RecordAbortionAsync(RecordAbortionViewModel model, CancellationToken cancellationToken = default)
    {
        var goat = await FindGoatAsync(model.Tag, cancellationToken)
            ?? throw new InvalidOperationException("Scan or type a valid doe tag");
        if (!goat.MatedDate.HasValue)
            throw new InvalidOperationException($"{goat.Tag} has no open cross or pregnancy.");
        if (model.Date > Today || model.Date < goat.MatedDate.Value)
            throw new InvalidOperationException("The date must be between her cross date and today.");

        var open = await BreedingCycleWriter.OpenEventsAsync(_context, goat.Id, cancellationToken);
        BreedingCycleWriter.CloseCycle(goat, open, BreedingOutcome.Aborted, model.Date,
            string.IsNullOrWhiteSpace(model.Notes) ? null : model.Notes.Trim());
        if (goat.Status == GoatStatus.Pregnant)
            goat.Status = GoatStatus.Dry;
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task MarkKiddedAsync(int goatId, CancellationToken cancellationToken = default)
    {
        var goat = await _context.Goats.FindAsync([goatId], cancellationToken)
            ?? throw new InvalidOperationException("Goat not found");
        var open = await BreedingCycleWriter.OpenEventsAsync(_context, goat.Id, cancellationToken);
        BreedingCycleWriter.CloseCycle(goat, open, BreedingOutcome.Kidded, Today, "marked kidded without birth details");
        goat.Status = GoatStatus.Milking;
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task RemovePrepAsync(int goatId, CancellationToken cancellationToken = default)
    {
        var goat = await _context.Goats.FindAsync([goatId], cancellationToken);
        if (goat is null) return;
        goat.PrepCrossDate = null;
        if (goat.BreedingStage == BreedingStage.Ready)
            goat.BreedingStage = BreedingStage.None;
        goat.UpdatedDate = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task RemoveCrossAsync(int goatId, CancellationToken cancellationToken = default)
    {
        var goat = await _context.Goats.FindAsync([goatId], cancellationToken);
        if (goat is null) return;
        var open = await BreedingCycleWriter.OpenEventsAsync(_context, goat.Id, cancellationToken);
        BreedingCycleWriter.CloseCycle(goat, open, BreedingOutcome.Cancelled, Today, "cross removed");
        if (goat.Status == GoatStatus.Pregnant)
            goat.Status = GoatStatus.Dry;
        await _context.SaveChangesAsync(cancellationToken);
    }

    private async Task<List<(string Tag, bool Temporary)>> AssignKidTagsAsync(
        string damTag, DateOnly date, IReadOnlyList<BirthKidViewModel> kids, CancellationToken cancellationToken)
    {
        var requested = kids
            .Select(k => string.IsNullOrWhiteSpace(k.Tag) ? null : k.Tag.Trim())
            .ToList();

        var explicitTags = requested.OfType<string>().ToList();
        if (explicitTags.Any(t => t.Length > 50))
            throw new InvalidOperationException("Kid tags can be at most 50 characters.");
        var dup = explicitTags.GroupBy(t => t, StringComparer.OrdinalIgnoreCase).FirstOrDefault(g => g.Count() > 1);
        if (dup is not null)
            throw new InvalidOperationException($"Tag {dup.Key} is used for more than one kid.");

        var prefix = $"T-{damTag}-{date:yyMMdd}-";
        var lowered = explicitTags.Select(t => t.ToLower()).ToList();
        var lowerPrefix = prefix.ToLower();
        var taken = (await _context.Goats.AsNoTracking()
                .Where(g => lowered.Contains(g.Tag.ToLower()) || g.Tag.ToLower().StartsWith(lowerPrefix))
                .Select(g => g.Tag)
                .ToListAsync(cancellationToken))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var clash = explicitTags.FirstOrDefault(taken.Contains);
        if (clash is not null)
            throw new InvalidOperationException($"Tag {clash} is already used by another goat.");

        var result = new List<(string, bool)>();
        var n = 1;
        foreach (var tag in requested)
        {
            if (tag is not null)
            {
                result.Add((tag, false));
                continue;
            }
            string temp;
            do temp = prefix + n++; while (taken.Contains(temp) || explicitTags.Contains(temp, StringComparer.OrdinalIgnoreCase));
            if (temp.Length > 50)
                throw new InvalidOperationException("The dam tag is too long to build a temporary kid tag — enter kid tags manually.");
            taken.Add(temp);
            result.Add((temp, true));
        }
        return result;
    }

    private static RecordBirthResultViewModel MapBirthResult(KiddingRecord record, bool alreadyRecorded) => new()
    {
        KiddingRecordId = record.Id,
        AlreadyRecorded = alreadyRecorded,
        Kids = record.Kids
            .OrderBy(k => k.Id)
            .Select(k => new RecordedKidViewModel { Id = k.Id, Tag = k.Tag, IsTemporaryTag = k.IsTemporaryTag, Gender = k.Gender })
            .ToList()
    };

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

    private static BreedingWaitingRowViewModel MapWaiting(Goat g)
    {
        var mated = g.MatedDate!.Value;
        var scanDue = mated.AddDays(BreedingHelper.ScanAfterDays);
        var (text, css) = DisplayHelper.GetStatusDisplay(g.Status);
        return new BreedingWaitingRowViewModel
        {
            Id = g.Id,
            Tag = g.Tag,
            Name = g.Name,
            MatedDate = mated.ToString("yyyy-MM-dd"),
            BuckTag = g.BuckTag,
            DaysSinceCross = -BreedingHelper.DaysUntil(mated),
            ScanDueDate = scanDue.ToString("yyyy-MM-dd"),
            ScanDueNow = BreedingHelper.DaysUntil(scanDue) <= 0,
            StatusDisplay = text,
            StatusCssClass = css
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

        return new BreedingExpectingRowViewModel
        {
            Id = g.Id,
            Tag = g.Tag,
            Name = g.Name,
            MatedDate = mated.ToString("yyyy-MM-dd"),
            BuckTag = g.BuckTag,
            KidsCount = g.KidsCount,
            UltrasoundDate = g.UltrasoundDate?.ToString("yyyy-MM-dd"),
            KidsDisplay = g.KidsCount.HasValue ? BreedingHelper.KidsLabel(g.KidsCount.Value) : "not checked",
            ExtraFeed = g.KidsCount >= 2,
            ExpectedKidding = kd.ToString("yyyy-MM-dd"),
            KiddingWindow = $"{w1:yyyy-MM-dd} → {w2:yyyy-MM-dd}",
            DueText = BreedingHelper.DueText(du),
            DueColor = dueColor
        };
    }
}
