using System.Text.Json;
using GoatFarm.Application.Common;
using GoatFarm.Application.Interfaces;
using GoatFarm.Application.ViewModels.Breeding;
using GoatFarm.Domain.Constants;
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

    public async Task<BreedingPageViewModel> GetBreedingPageAsync(
        string? selectedBatchId = null, CancellationToken cancellationToken = default)
    {
        var today = Today;
        var goats = await _context.Goats.AsNoTracking()
            .Include(g => g.HeatEvents)
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
                Id = g.Id,
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
        var rationRows = BuildRationRows(prep, waiting, confirmed, plans, _feedService);

        var season = await LoadSeasonSettingsAsync(cancellationToken);
        var batchId = selectedBatchId ?? season.Batches.FirstOrDefault()?.Id ?? "b1";

        var does = goats.Where(g => g.Gender == GoatGender.Female && g.Status != GoatStatus.Kid).ToList();
        var bcsRecords = await _context.GoatBcsRecords.AsNoTracking().ToListAsync(cancellationToken);
        var latestBcs = bcsRecords
            .GroupBy(r => r.GoatId)
            .ToDictionary(g => g.Key, g => (int?)g.OrderByDescending(r => r.Date).First().Score);

        var yrStart = new DateOnly(today.Year, 1, 1);
        var kiddingsYear = await _context.KiddingRecords.AsNoTracking()
            .Include(k => k.BreedingEvent).ThenInclude(e => e!.Buck)
            .Where(k => k.KiddingDate >= yrStart)
            .ToListAsync(cancellationToken);

        var emptyByBuck = emptyLogs
            .Where(l => !string.IsNullOrWhiteSpace(l.BuckTag))
            .GroupBy(l => l.BuckTag!.ToLowerInvariant())
            .ToDictionary(g => g.Key, g => g.Count());

        var bucks = goats.Where(g =>
            g.Gender == GoatGender.Male &&
            (g.Status == GoatStatus.Buck || (g.Status != GoatStatus.Kid && g.Status != GoatStatus.Meat))).ToList();

        var heatsByGoat = goats.ToDictionary(
            g => g.Id,
            g => g.HeatEvents.Select(h => h.Date).OrderBy(d => d).ToList());

        return new BreedingPageViewModel
        {
            PrepCount = prep.Count,
            WaitingCount = waiting.Count,
            ExpectingCount = confirmed.Count,
            BreedWaitText = waiting.Count + " crossed, waiting for scan",
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
            RationRows = rationRows,
            PipelineRows = BreedingPageAssembler.BuildPipelineRows(goats),
            NowJobs = BreedingPageAssembler.BuildNowJobs(goats, season, today),
            Season = BreedingPageAssembler.BuildSeasonPanel(season, batchId, goats, today),
            YearRows = BreedingPageAssembler.BuildYearRows(season, batchId, goats, today),
            PlanRows = BreedingPageAssembler.BuildPlanRows(does, season, latestBcs, today),
            HeatRows = BreedingPageAssembler.BuildHeatRows(does, heatsByGoat, today),
            BuckRows = BreedingPageAssembler.BuildBuckRows(bucks, goats, kiddingsYear, emptyByBuck, today),
            BuckOptions = bucks.Select(b => b.Tag).OrderBy(t => t, StringComparer.OrdinalIgnoreCase).ToList(),
            GroupCandidates = goats
                .Where(g => g.Gender == GoatGender.Female && g.Status != GoatStatus.Kid && !g.MatedDate.HasValue)
                .Where(g => BreedingRules.EligibilityError(g, today) is null)
                .OrderByDescending(g => g.PrepCrossDate.HasValue)
                .ThenBy(g => g.Tag)
                .Select(g => new BreedingGroupCandidateViewModel
                {
                    Id = g.Id,
                    Tag = g.Tag,
                    Breed = g.Breed,
                    Ready = g.PrepCrossDate.HasValue
                }).ToList()
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

    public async Task RecordReturnedToHeatAsync(RecordReturnedToHeatViewModel model, CancellationToken cancellationToken = default)
    {
        var goat = await FindGoatAsync(model.Tag, cancellationToken)
            ?? throw new InvalidOperationException("Scan or type a valid doe tag.");
        if (!goat.MatedDate.HasValue)
            throw new InvalidOperationException("She has no cross recorded.");

        _context.GoatHeatEvents.Add(new GoatHeatEvent
        {
            GoatId = goat.Id,
            Date = model.Date,
            Note = "returned after cross"
        });
        _context.BreedingEmptyLogs.Add(new BreedingEmptyLog
        {
            GoatId = goat.Id,
            ScanDate = model.Date,
            MatedDate = goat.MatedDate,
            BuckTag = goat.BuckTag
        });

        var open = await BreedingCycleWriter.OpenEventsAsync(_context, goat.Id, cancellationToken);
        BreedingCycleWriter.CloseCycle(goat, open, BreedingOutcome.Empty, model.Date, "returned to heat");
        goat.MatedDate = null;
        goat.BuckTag = null;
        goat.KidsCount = null;
        goat.UltrasoundDate = null;
        goat.PrepCrossDate = null;
        goat.Status = GoatStatus.Dry;
        goat.BreedingStage = BreedingStage.None;
        goat.UpdatedDate = DateTime.UtcNow;
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
        IFeedService feedService)
    {
        var dryCost = PlanDailyFeedCostForStatus(plans, feedService, GoatStatus.Dry);
        var pregnantCost = PlanDailyFeedCostForStatus(plans, feedService, GoatStatus.Pregnant);

        var rows = new List<BreedingRationRowViewModel>();
        foreach (var g in prep)
            rows.Add(MapRation(g, "Ready for cross", GoatStatus.Dry, plans, dryCost, g.PrepCrossDate));
        foreach (var g in waiting)
            rows.Add(MapRation(g, "Crossed — awaiting scan", GoatStatus.Dry, plans, dryCost, null));
        foreach (var g in confirmed)
            rows.Add(MapRation(g, "Confirmed pregnant", GoatStatus.Pregnant, plans, pregnantCost, null, g.KidsCount));
        return rows;
    }

    /// <summary>Same as index_new.html planDailyFeed(plans[st], st) — Feed tab daily cost per goat.</summary>
    private static decimal PlanDailyFeedCostForStatus(IReadOnlyList<FeedPlan> plans, IFeedService feedService, GoatStatus status)
    {
        var plan = plans.FirstOrDefault(p => p.StatusKey == status)
            ?? plans.FirstOrDefault(p => p.StatusKey == GoatStatus.Dry);
        if (plan is null) return 0;
        return feedService.CalculateDailyFeedCost(plan.MixKgPerDay, DisplayHelper.StatusKey(status));
    }

    private static BreedingRationRowViewModel MapRation(
        Goat g,
        string stage,
        GoatStatus planStatus,
        IReadOnlyList<FeedPlan> plans,
        decimal dailyCostPerGoat,
        DateOnly? prepCrossDate,
        int? kidsCount = null)
    {
        var plan = plans.FirstOrDefault(p => p.StatusKey == planStatus)
            ?? plans.FirstOrDefault(p => p.StatusKey == GoatStatus.Dry);
        var mix = plan?.MixKgPerDay ?? 0;
        var fodder = plan?.FodderKgPerDay ?? 0;

        var note = "";
        if (prepCrossDate.HasValue && stage == "Ready for cross")
        {
            var since = prepCrossDate.Value > Today ? Today : prepCrossDate.Value;
            var prepDays = Math.Max(0, Today.DayNumber - since.DayNumber);
            note = $"on extra feed · {prepDays} day(s)";
        }
        else if (kidsCount >= 2)
            note = $"↑ {BreedingHelper.KidsLabel(kidsCount.Value)} — extra feed";
        else if (kidsCount == 1)
            note = "single kid";

        return new BreedingRationRowViewModel
        {
            Id = g.Id,
            Tag = g.Tag,
            Name = g.Name,
            Stage = stage,
            MixKgPerDay = mix,
            FodderKgPerDay = fodder,
            DailyCost = dailyCostPerGoat,
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

    public async Task UpdateSeasonAsync(UpdateBreedingSeasonViewModel model, CancellationToken cancellationToken = default)
    {
        var season = await LoadSeasonSettingsAsync(cancellationToken);
        if (model.BuildUp is > 0) season.BuildUp = model.BuildUp.Value;
        if (model.ScanAfter is > 0) season.ScanAfter = model.ScanAfter.Value;
        if (model.DryOff is > 0) season.DryOff = model.DryOff.Value;
        if (model.WeanDays is > 0) season.WeanDays = model.WeanDays.Value;
        if (model.WeanKg is > 0) season.WeanKg = model.WeanKg.Value;
        if (model.DryBeforeCross is >= 0) season.DryBeforeCross = model.DryBeforeCross.Value;

        var batch = season.Batches.FirstOrDefault(b => b.Id == model.SelectedBatchId)
            ?? season.Batches.FirstOrDefault();
        if (batch is not null)
        {
            if (!string.IsNullOrWhiteSpace(model.BatchName))
                batch.Name = model.BatchName.Trim();
            if (!string.IsNullOrWhiteSpace(model.BatchFrom))
                batch.From = model.BatchFrom.Trim();
            if (!string.IsNullOrWhiteSpace(model.BatchTo))
                batch.To = model.BatchTo.Trim();
        }

        if (model.RemoveBatch == true && batch is not null && season.Batches.Count > 1)
        {
            var assigned = await _context.Goats.Where(g => g.BirthBatchName == batch.Name).ToListAsync(cancellationToken);
            foreach (var g in assigned)
                g.BirthBatchName = null;
            season.Batches.Remove(batch);
        }

        if (!string.IsNullOrWhiteSpace(model.NewBatchName))
        {
            var id = "b" + Guid.NewGuid().ToString("N")[..8];
            season.Batches.Add(new BreedingSeasonBatch
            {
                Id = id,
                Name = model.NewBatchName.Trim(),
                From = "06-01",
                To = "06-15"
            });
        }

        await SaveSeasonSettingsAsync(season, cancellationToken);
    }

    public async Task AddDoeToPipelineAsync(BreedingAddDoeViewModel model, CancellationToken cancellationToken = default)
    {
        var goat = await FindGoatAsync(model.Tag, cancellationToken)
            ?? throw new InvalidOperationException("Scan or type a valid doe tag");
        if (string.Equals(model.Stage, "cross", StringComparison.OrdinalIgnoreCase))
        {
            await CrossAsync(goat, model.Date ?? Today, model.BuckTag, false, cancellationToken);
            return;
        }

        var error = BreedingRules.EligibilityError(goat, Today);
        if (error is not null) throw new InvalidOperationException(error);
        goat.PrepCrossDate = Today;
        goat.NextCrossDate = null;
        goat.BreedingStage = BreedingStage.Ready;
        goat.UpdatedDate = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task ApplyStageActionAsync(BreedingStageActionViewModel model, CancellationToken cancellationToken = default)
    {
        var goat = await _context.Goats.FindAsync([model.GoatId], cancellationToken)
            ?? throw new InvalidOperationException("Goat not found");
        var action = model.Action.ToLowerInvariant();
        switch (action)
        {
            case "cross":
                await CrossAsync(goat, model.Date ?? Today, model.BuckTag, model.ConfirmWarnings, cancellationToken);
                break;
            case "replan":
                if (!model.Date.HasValue) throw new InvalidOperationException("Pick a cross date.");
                goat.NextCrossDate = model.Date;
                goat.PrepCrossDate = null;
                goat.UpdatedDate = DateTime.UtcNow;
                await _context.SaveChangesAsync(cancellationToken);
                break;
            case "prep2":
                var err = BreedingRules.EligibilityError(goat, Today);
                if (err is not null) throw new InvalidOperationException(err);
                goat.PrepCrossDate = Today;
                goat.NextCrossDate = null;
                goat.BreedingStage = BreedingStage.Ready;
                goat.UpdatedDate = DateTime.UtcNow;
                await _context.SaveChangesAsync(cancellationToken);
                break;
            case "notcross":
                goat.PrepCrossDate = null;
                if (goat.BreedingStage == BreedingStage.Ready)
                    goat.BreedingStage = BreedingStage.None;
                goat.UpdatedDate = DateTime.UtcNow;
                await _context.SaveChangesAsync(cancellationToken);
                break;
            case "scan":
            case "rescan":
                if (!model.KidsCount.HasValue) throw new InvalidOperationException("Pick a scan result.");
                await RecordUltrasoundAsync(new RecordUltrasoundViewModel
                {
                    Tag = goat.Tag,
                    KidsCount = model.KidsCount.Value,
                    Date = model.Date
                }, cancellationToken);
                break;
            case "abort":
                await RecordAbortionAsync(new RecordAbortionViewModel
                {
                    Tag = goat.Tag,
                    Date = model.Date ?? Today,
                    Notes = model.Notes
                }, cancellationToken);
                break;
            case "mistake":
                if (goat.NextCrossDate.HasValue && !goat.MatedDate.HasValue)
                {
                    goat.NextCrossDate = null;
                    goat.UpdatedDate = DateTime.UtcNow;
                    await _context.SaveChangesAsync(cancellationToken);
                    break;
                }
                await ClearBreedingMistakeAsync(goat.Id, cancellationToken);
                break;
            default:
                throw new InvalidOperationException("Unknown action.");
        }
    }

    public async Task ClearBreedingMistakeAsync(int goatId, CancellationToken cancellationToken = default)
    {
        var goat = await _context.Goats.FindAsync([goatId], cancellationToken)
            ?? throw new InvalidOperationException("Goat not found");
        var open = await BreedingCycleWriter.OpenEventsAsync(_context, goat.Id, cancellationToken);
        foreach (var e in open)
        {
            e.Outcome = BreedingOutcome.Cancelled;
            e.OutcomeDate = Today;
            e.Notes = "entered by mistake";
            e.UpdatedDate = DateTime.UtcNow;
        }
        goat.MatedDate = null;
        goat.BuckTag = null;
        goat.KidsCount = null;
        goat.UltrasoundDate = null;
        goat.PrepCrossDate = null;
        goat.NextCrossDate = null;
        goat.BreedingStage = BreedingStage.None;
        if (goat.Status == GoatStatus.Pregnant)
            goat.Status = GoatStatus.Dry;
        goat.UpdatedDate = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<BreedingGroupDto>> GetBreedingGroupsAsync(CancellationToken cancellationToken = default) =>
        await LoadBreedingGroupsAsync(cancellationToken);

    public async Task CreateBreedingGroupAsync(CreateBreedingGroupViewModel model, CancellationToken cancellationToken = default)
    {
        if (model.DoeIds.Count == 0)
            throw new InvalidOperationException("Tick at least one doe.");
        var take = model.DoeIds.Take(Math.Max(1, model.Max)).ToList();
        var buck = await BreedingCycleWriter.FindBuckAsync(_context, model.BuckTag, cancellationToken)
            ?? throw new InvalidOperationException("Buck not found in herd.");
        var pedigree = await BreedingCycleWriter.LoadPedigreeAsync(_context, cancellationToken);
        var groups = await LoadBreedingGroupsAsync(cancellationToken);
        if (groups.Any(g => g.Status == "active" && string.Equals(g.Buck, model.BuckTag, StringComparison.OrdinalIgnoreCase))
            && !model.ConfirmSecondActive)
            throw new ConfirmationRequiredException(["This buck already has an active group."]);

        foreach (var id in take)
        {
            var doe = await _context.Goats.FindAsync([id], cancellationToken)
                ?? throw new InvalidOperationException("Doe not found.");
            var plan = BreedingCycleWriter.PlanCross(doe, buck, model.BuckTag, model.StartDate, Today, pedigree);
            if (plan.Check.Errors.Count > 0)
                throw new InvalidOperationException($"{doe.Tag}: {string.Join(" ", plan.Check.Errors)}");
            if (plan.Check.Warnings.Count > 0 && !model.ConfirmWarnings)
                throw new ConfirmationRequiredException(plan.Check.Warnings);
            var open = await BreedingCycleWriter.OpenEventsAsync(_context, doe.Id, cancellationToken);
            BreedingCycleWriter.ApplyCross(_context, doe, model.StartDate, plan, open);
        }

        await _context.SaveChangesAsync(cancellationToken);

        var n = groups.Count(g => g.Name.StartsWith("Breeding Group", StringComparison.OrdinalIgnoreCase)) + 1;
        var members = new List<BreedingGroupMemberDto>();
        foreach (var id in take)
        {
            var g = await _context.Goats.AsNoTracking().FirstAsync(x => x.Id == id, cancellationToken);
            members.Add(new BreedingGroupMemberDto { GoatId = id, Tag = g.Tag });
        }
        groups.Add(new BreedingGroupDto
        {
            Id = Guid.NewGuid().ToString("N"),
            Name = "Breeding Group " + n.ToString("000"),
            Buck = model.BuckTag,
            Start = model.StartDate.ToString("yyyy-MM-dd"),
            Max = model.Max,
            Status = "active",
            Members = members
        });
        await SaveBreedingGroupsAsync(groups, cancellationToken);
    }

    private async Task<BreedingSeasonSettings> LoadSeasonSettingsAsync(CancellationToken cancellationToken)
    {
        var row = await _context.AppSettings.AsNoTracking()
            .FirstOrDefaultAsync(s => s.Key == AppSettingKeys.BreedingSeason, cancellationToken);
        if (row?.Value is null)
            return new BreedingSeasonSettings();
        return JsonSerializer.Deserialize<BreedingSeasonSettings>(row.Value) ?? new BreedingSeasonSettings();
    }

    private async Task SaveSeasonSettingsAsync(BreedingSeasonSettings season, CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(season);
        var row = await _context.AppSettings.FirstOrDefaultAsync(s => s.Key == AppSettingKeys.BreedingSeason, cancellationToken);
        if (row is null)
        {
            _context.AppSettings.Add(new AppSetting { Key = AppSettingKeys.BreedingSeason, Value = json });
        }
        else row.Value = json;
        await _context.SaveChangesAsync(cancellationToken);
    }

    private async Task<List<BreedingGroupDto>> LoadBreedingGroupsAsync(CancellationToken cancellationToken)
    {
        var row = await _context.AppSettings.AsNoTracking()
            .FirstOrDefaultAsync(s => s.Key == AppSettingKeys.BreedingGroups, cancellationToken);
        if (row?.Value is null) return [];
        return JsonSerializer.Deserialize<List<BreedingGroupDto>>(row.Value) ?? [];
    }

    private async Task SaveBreedingGroupsAsync(List<BreedingGroupDto> groups, CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(groups);
        var row = await _context.AppSettings.FirstOrDefaultAsync(s => s.Key == AppSettingKeys.BreedingGroups, cancellationToken);
        if (row is null)
            _context.AppSettings.Add(new AppSetting { Key = AppSettingKeys.BreedingGroups, Value = json });
        else row.Value = json;
        await _context.SaveChangesAsync(cancellationToken);
    }
}
