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
        string? selectedBatchId = null,
        string? reportBuck = null,
        CancellationToken cancellationToken = default)
    {
        await ApplyAutoWeanDueKidsAsync(cancellationToken);
        var today = Today;
        var goats = await _context.Goats.AsNoTracking()
            .Include(g => g.HeatEvents)
            .Where(g => !g.IsArchived)
            .OrderBy(g => g.Tag)
            .ToListAsync(cancellationToken);
        var prep = goats.Where(g => g.PrepCrossDate.HasValue && !g.MatedDate.HasValue).ToList();
        var awaitingScan = goats.Where(PregnancyRules.IsAwaitingScan).ToList();
        var waiting = awaitingScan;
        var confirmed = goats.Where(PregnancyRules.IsConfirmedPregnant).ToList();

        int? soonest = confirmed.Count > 0
            ? confirmed.Min(g => BreedingHelper.DaysUntil(BreedingHelper.ExpectedKidding(g.MatedDate!.Value)))
            : null;

        var emptyLogs = await _context.BreedingEmptyLogs.AsNoTracking()
            .Include(l => l.Goat)
            .OrderByDescending(l => l.ScanDate)
            .ToListAsync(cancellationToken);

        var emptyByGoat = emptyLogs.GroupBy(l => l.GoatId).ToDictionary(g => g.Key, g => g.Count());
        var totalScans = confirmed.Count + emptyLogs.Count;
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
        var todayBatchId = BreedingSeasonHelper.PickTodaySeasonBatchId(season, today);

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

        var kiddingsAll = await _context.KiddingRecords.AsNoTracking()
            .Include(k => k.BreedingEvent).ThenInclude(e => e!.Buck)
            .ToListAsync(cancellationToken);

        var bornKids = await _context.Goats.AsNoTracking()
            .Where(g => !g.IsArchived && g.Source == GoatSource.Born)
            .Include(g => g.WeightRecords)
            .Include(g => g.KiddingRecord!).ThenInclude(k => k.BreedingEvent).ThenInclude(e => e!.Buck)
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

        var breedingGroups = await LoadBreedingGroupsAsync(cancellationToken);
        var activeGroups = breedingGroups.Count(g =>
            string.Equals(g.Status, "active", StringComparison.OrdinalIgnoreCase));
        var curBatch = season.Batches.FirstOrDefault(b => b.Id == batchId) ?? season.Batches.FirstOrDefault();
        var batchName = curBatch?.Name ?? "Season";
        var inHeat = goats.Count(g => g.Gender == GoatGender.Female && g.Status != GoatStatus.Kid && !g.MatedDate.HasValue &&
            g.HeatEvents.Count > 0 && HerdRules.HeatExpected(g.HeatEvents.Max(h => h.Date), today));
        var gettingReady = goats.Count(g => g.Gender == GoatGender.Female && g.Status != GoatStatus.Kid &&
            !g.MatedDate.HasValue && (g.PrepCrossDate.HasValue || g.NextCrossDate.HasValue)) + inHeat;

        var allKiddings = await _context.KiddingRecords.AsNoTracking()
            .Include(k => k.Kids).ThenInclude(kid => kid.WeightRecords)
            .Include(k => k.BreedingEvent).ThenInclude(e => e!.Buck)
            .ToListAsync(cancellationToken);
        var breedingEvents = await _context.BreedingEvents.AsNoTracking()
            .Include(e => e.Buck)
            .ToListAsync(cancellationToken);
        var kgPerDoe = BreedingKpiHelper.KgPerDoeCrossed(allKiddings, season, today);
        var weanKgText = kgPerDoe is null ? "—" : kgPerDoe.Value.ToString("0.0") + " kg";

        var kiddingCounts = allKiddings.GroupBy(k => k.DoeId).ToDictionary(g => g.Key, g => g.Count());
        var latestWeights = await _context.GoatWeightRecords.AsNoTracking()
            .GroupBy(w => w.GoatId)
            .Select(g => new { g.Key, Kg = g.OrderByDescending(x => x.Date).Select(x => x.Kg).FirstOrDefault() })
            .ToDictionaryAsync(x => x.Key, x => (decimal?)x.Kg, cancellationToken);
        var rescanIds = (await _context.FarmTasks.AsNoTracking()
            .Where(t => t.Status == FarmTaskStatus.ToDo && t.Title.StartsWith("Rescan") && t.GoatId != null)
            .Select(t => t.GoatId!.Value)
            .ToListAsync(cancellationToken)).ToHashSet();
        var kidGoats = goats.Where(g => g.Status == GoatStatus.Kid && !g.WeanDate.HasValue).ToList();

        var seasonPanel = BreedingPageAssembler.BuildSeasonPanel(season, batchId, goats, today);
        var todayBatch = season.Batches.FirstOrDefault(b => b.Id == todayBatchId) ?? season.Batches[0];
        var (tlName, _, _) = BreedingPageAssembler.BuildTodayTimeline(season, todayBatchId, today);
        tlName = todayBatch.Name;
        var (todayPlanPct, todayPlanSegs) = BreedingPageAssembler.BuildPlanTimeline(season, todayBatchId, today);
        var todayLabels = BreedingPageAssembler.BuildTodayTimelineLabels(season, todayBatchId, today);
        var todayNext = BreedingPageAssembler.BuildTodaySeasonNextSteps(season, todayBatchId, today);
        var (planTlPct, planTlSegs) = BreedingPageAssembler.BuildPlanTimeline(season, batchId, today);
        var selectedBatchDoes = goats.Where(g => string.Equals(g.BirthBatchName, batchName, StringComparison.OrdinalIgnoreCase))
            .Select(g => g.Id).ToHashSet();
        var batchBuckGroups = breedingGroups.Count(g =>
            string.Equals(g.Status, "active", StringComparison.OrdinalIgnoreCase) &&
            g.Members.Any(m => selectedBatchDoes.Contains(m.GoatId)));
        var reports = BreedingReportsAssembler.Build(
            season, batchId, reportBuck, goats, breedingEvents, allKiddings, emptyLogs, today, canEditTargets: true);
        var goatsById = goats.ToDictionary(g => g.Id);
        var kiddingsByDoe = allKiddings.ToLookup(k => k.DoeId);
        var dueToKidRows = BreedingKiddingAssembler.BuildDueRows(confirmed, kiddingCounts, kiddingsByDoe, today);
        var motherCheckRows = BreedingKiddingAssembler.BuildMotherCheckRows(allKiddings, goatsById, kiddingCounts, today);
        var kidGroundRows = BreedingKiddingAssembler.BuildKidRows(bornKids, goatsById, season.WeanDays, season.AutoWean, today);
        var onMilkKids = bornKids.Where(g => !g.IsArchived && g.Status == GoatStatus.Kid && !g.WeanDate.HasValue).ToList();
        var kidAutoWeanBars = BreedingKiddingAssembler.BuildAutoWeanBars(season, onMilkKids);
        var kiddingStats = BreedingKiddingAssembler.BuildStats(dueToKidRows, motherCheckRows, kidGroundRows, does, today);
        var motherGroups = await LoadMotherGroupsAsync(cancellationToken);
        var latestKidding = allKiddings
            .GroupBy(k => k.DoeId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(k => k.KiddingDate).First());
        var allKiddingsByDoe = allKiddings.GroupBy(k => k.DoeId).ToDictionary(g => g.Key, g => g.ToArray());
        var motherGroupRows = BreedingMotherGroupAssembler.BuildRows(does, kiddingCounts, latestKidding, allKiddingsByDoe);

        return new BreedingPageViewModel
        {
            PrepCount = prep.Count,
            GettingReadyCount = gettingReady,
            WaitingCount = waiting.Count,
            ExpectingCount = confirmed.Count,
            BuckCount = bucks.Count,
            ActiveBreedingGroupCount = activeGroups,
            WeanedKgPerDoeText = weanKgText,
            TodaySeasonName = tlName,
            TodayTimelinePercent = todayPlanPct,
            TodayTimelineMarkers = [],
            TodayPlanTimelineTodayPercent = todayPlanPct,
            TodayPlanTimelineSegments = todayPlanSegs,
            TodayTimelineLabels = todayLabels,
            TodaySeasonNext = todayNext,
            PlanTimelineTodayPercent = planTlPct,
            PlanTimelineSegments = planTlSegs,
            Reports = reports,
            SelectedBatchBuckGroupCount = batchBuckGroups,
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
            PipelineRows = BreedingPageAssembler.BuildPipelineRows(goats, kiddingCounts, allKiddingsByDoe),
            DoeFlowSteps = BreedingPageAssembler.BuildDoeFlowSteps(goats, today),
            NowJobs = BreedingPageAssembler.BuildNowJobs(goats, season, today, breedingGroups, kiddingCounts,
                latestWeights, rescanIds, kidGoats),
            Season = seasonPanel,
            YearRows = BreedingPageAssembler.BuildYearRows(season, batchId, goats, today, breedingGroups),
            PlanRows = BreedingPageAssembler.BuildPlanRows(does, season, latestBcs, today),
            HeatRows = BreedingPageAssembler.BuildHeatRows(does, heatsByGoat, today),
            BuckRows = BreedingPageAssembler.BuildBuckRows(bucks, goats, kiddingsYear, kiddingsAll, bornKids,
                emptyByBuck,
                await _context.BuckHealthChecks.AsNoTracking().ToListAsync(cancellationToken), today),
            BuckOptions = bucks.Select(b => b.Tag).OrderBy(t => t, StringComparer.OrdinalIgnoreCase).ToList(),
            KiddingStats = kiddingStats,
            DueToKidRows = dueToKidRows,
            MotherCheckRows = motherCheckRows,
            KidGroundRows = kidGroundRows,
            SeasonWeanDays = season.WeanDays,
            SeasonAutoWean = season.AutoWean,
            KidAutoWeanBars = kidAutoWeanBars,
            MotherGroups = motherGroups,
            MotherGroupRows = motherGroupRows,
            GroupCandidates = await BuildGroupPickerCandidatesAsync(
                bucks.FirstOrDefault()?.Tag,
                goats,
                latestWeights,
                motherGroups,
                breedingGroups,
                season,
                today,
                cancellationToken)
        };
    }

    public async Task<IReadOnlyList<BreedingGroupCandidateViewModel>> GetGroupPickerAsync(
        string? buckTag,
        CancellationToken cancellationToken = default)
    {
        var today = Today;
        var goats = await _context.Goats.AsNoTracking()
            .Where(g => !g.IsArchived && g.Gender == GoatGender.Female)
            .OrderBy(g => g.Tag)
            .ToListAsync(cancellationToken);
        var latestWeights = await _context.GoatWeightRecords.AsNoTracking()
            .GroupBy(w => w.GoatId)
            .Select(g => new { g.Key, Kg = g.OrderByDescending(x => x.Date).Select(x => x.Kg).FirstOrDefault() })
            .ToDictionaryAsync(x => x.Key, x => (decimal?)x.Kg, cancellationToken);
        var motherGroups = await LoadMotherGroupsAsync(cancellationToken);
        var breedingGroups = await LoadBreedingGroupsAsync(cancellationToken);
        var season = await LoadSeasonSettingsAsync(cancellationToken);
        return await BuildGroupPickerCandidatesAsync(
            buckTag,
            goats,
            latestWeights,
            motherGroups,
            breedingGroups,
            season,
            today,
            cancellationToken);
    }

    private async Task<IReadOnlyList<BreedingGroupCandidateViewModel>> BuildGroupPickerCandidatesAsync(
        string? buckTag,
        IReadOnlyList<Goat> does,
        IReadOnlyDictionary<int, decimal?> latestWeights,
        IReadOnlyList<MotherGroupDto> motherGroups,
        IReadOnlyList<BreedingGroupDto> breedingGroups,
        BreedingSeasonSettings season,
        DateOnly today,
        CancellationToken cancellationToken)
    {
        var inGroup = new Dictionary<int, string>();
        foreach (var grp in breedingGroups.Where(g => string.Equals(g.Status, "active", StringComparison.OrdinalIgnoreCase)))
        {
            foreach (var m in grp.Members)
                inGroup[m.GoatId] = grp.Buck;
        }

        var buck = await BreedingCycleWriter.FindBuckAsync(_context, buckTag, cancellationToken);
        var pedigree = await BreedingCycleWriter.LoadPedigreeAsync(_context, cancellationToken);
        var minKg = season.MinYoungDoeKg;

        return does
            .OrderByDescending(g => g.PrepCrossDate.HasValue && BreedingGroupPickerHelper.BlockReason(
                g, today, minKg, latestWeights.GetValueOrDefault(g.Id), buck, pedigree, inGroup) is null)
            .ThenByDescending(g => BreedingGroupPickerHelper.BlockReason(
                g, today, minKg, latestWeights.GetValueOrDefault(g.Id), buck, pedigree, inGroup) is null)
            .ThenBy(g => g.Tag)
            .Select(g =>
            {
                var w = latestWeights.GetValueOrDefault(g.Id);
                var block = BreedingGroupPickerHelper.BlockReason(g, today, minKg, w, buck, pedigree, inGroup);
                var mg = string.IsNullOrWhiteSpace(g.MotherGroupId)
                    ? null
                    : motherGroups.FirstOrDefault(m => string.Equals(m.Id, g.MotherGroupId, StringComparison.OrdinalIgnoreCase));
                return new BreedingGroupCandidateViewModel
                {
                    Id = g.Id,
                    Tag = g.Tag,
                    Breed = g.Breed,
                    Ready = g.PrepCrossDate.HasValue,
                    CanPick = block is null,
                    BlockReason = block,
                    LatestWeightKg = w is > 0 ? w : null,
                    MotherGroupId = g.MotherGroupId,
                    MotherGroupName = mg?.Name
                };
            }).ToList();
    }

    private async Task<int> ApplyAutoWeanDueKidsAsync(CancellationToken cancellationToken)
    {
        var season = await LoadSeasonSettingsAsync(cancellationToken);
        if (!season.AutoWean)
            return 0;
        var today = Today;
        var kids = await _context.Goats
            .Where(g => !g.IsArchived && g.Status == GoatStatus.Kid && g.WeanDate == null)
            .ToListAsync(cancellationToken);
        var n = 0;
        foreach (var kid in kids)
        {
            var due = kid.EventDate.AddDays(season.WeanDays);
            if (due > today) continue;
            kid.WeanDate = due;
            kid.WeanMethod = "auto";
            kid.UpdatedDate = DateTime.UtcNow;
            n++;
        }

        if (n > 0)
            await _context.SaveChangesAsync(cancellationToken);
        return n;
    }

    public async Task<BreedingDoeProfileViewModel?> GetDoeProfileAsync(int goatId, CancellationToken cancellationToken = default)
    {
        var goat = await _context.Goats.AsNoTracking()
            .Include(g => g.Sire)
            .Include(g => g.Dam)
            .Include(g => g.HeatEvents)
            .FirstOrDefaultAsync(g => g.Id == goatId && !g.IsArchived, cancellationToken);
        if (goat is null || goat.Gender != GoatGender.Female)
            return null;

        var events = await _context.BreedingEvents.AsNoTracking()
            .Include(e => e.Buck)
            .Where(e => e.DoeId == goatId)
            .OrderByDescending(e => e.MatedDate)
            .ToListAsync(cancellationToken);

        var kiddings = await _context.KiddingRecords.AsNoTracking()
            .Where(k => k.DoeId == goatId)
            .ToListAsync(cancellationToken);

        var kidsAlive = await _context.Goats.AsNoTracking()
            .CountAsync(g => g.DamId == goatId && !g.IsArchived, cancellationToken);

        var today = Today;
        return BreedingDoeProfileBuilder.Build(
            goat,
            events,
            kiddings,
            kidsAlive,
            _goatService.GetAgeLabel,
            today);
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
            var buckBeforeEmpty = goat.BuckTag;
            _context.BreedingEmptyLogs.Add(new BreedingEmptyLog
            {
                GoatId = goat.Id,
                ScanDate = scanDate,
                MatedDate = goat.MatedDate,
                BuckTag = buckBeforeEmpty
            });
            foreach (var e in open)
            {
                e.ScanDate = scanDate;
                e.ScanKids = 0;
            }
            BreedingCycleWriter.CloseCycle(goat, open, BreedingOutcome.Empty, scanDate);
            if (!string.IsNullOrWhiteSpace(model.EmptyNextStep))
            {
                await ApplyEmptyScanNextAsync(goat, scanDate, new BulkScanResultItemViewModel
                {
                    GoatId = goat.Id,
                    KidsCount = 0,
                    EmptyNextStep = model.EmptyNextStep,
                    EmptyBirthBatchId = model.EmptyBirthBatchId
                }, buckBeforeEmpty, cancellationToken);
            }
        }
        else
        {
            if (!goat.MatedDate.HasValue)
                throw new InvalidOperationException("Record her cross first (she must be in the Waiting for scan list).");
            if (model.DaysPregnant is > 0)
            {
                var corrected = scanDate.AddDays(-model.DaysPregnant.Value);
                if (goat.MatedDate.HasValue && corrected > scanDate)
                    throw new InvalidOperationException("Days pregnant is too large for the scan date.");
                goat.MatedDate = corrected;
                foreach (var e in open)
                {
                    e.MatedDate = corrected;
                    e.UpdatedDate = DateTime.UtcNow;
                }
            }

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
        if (model.Kids.Any(k => k.WeightKg is null or <= 0))
            throw new InvalidOperationException("Enter birth weight for every live kid.");
        if (!Enum.IsDefined(model.Ease) || model.Kids.Any(k => !Enum.IsDefined(k.Gender)))
            throw new InvalidOperationException("Invalid birth details.");
        if (model.Mothering is < 1 or > 5 || model.MilkProduction is < 1 or > 5 || model.BodyCondition is < 1 or > 5)
            throw new InvalidOperationException("Mothering, milk, and body condition must be between 1 and 5.");
        if (!Enum.IsDefined(model.DamHealth) || !Enum.IsDefined(model.KeepDecision))
            throw new InvalidOperationException("Invalid mother health or keep decision.");
        if (model.Kids.Any(k => !Enum.IsDefined(k.MilkType) || !Enum.IsDefined(k.KidHealth)))
            throw new InvalidOperationException("Invalid kid milk type or health.");

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

        var issues = model.DamHealth == DamAfterKiddingHealth.Good
            ? null
            : string.Join(", ", model.DamHealthIssues.Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s.Trim()));
        var keepReason = model.KeepDecision == DamKeepDecision.Keep
            ? null
            : string.IsNullOrWhiteSpace(model.KeepReason) ? null : model.KeepReason.Trim();

        var record = new KiddingRecord
        {
            DoeId = doe.Id,
            BreedingEvent = currentEvent,
            KiddingDate = model.KiddingDate,
            LiveKids = model.Kids.Count,
            Stillborn = model.Stillborn,
            Ease = model.Ease,
            Mothering = model.Mothering,
            MilkProduction = model.MilkProduction,
            BodyCondition = model.BodyCondition,
            DamHealth = model.DamHealth,
            DamHealthIssues = string.IsNullOrWhiteSpace(issues) ? null : issues,
            KeepDecision = model.KeepDecision,
            KeepReason = keepReason,
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
                KiddingRecord = record,
                KidMilkType = input.MilkType,
                KidHealthTag = input.KidHealth,
                ColostrumGiven = input.ColostrumGiven
            };
            if (input.WeightKg > 0)
                kid.WeightRecords.Add(new GoatWeightRecord { Date = model.KiddingDate, Kg = input.WeightKg.Value });
            kid.BirthBatchName = doe.BirthBatchName;
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

    private static void ValidateBirthWindow(string from, string to)
    {
        if (!DateOnly.TryParse("2000-" + from, out var f) || !DateOnly.TryParse("2000-" + to, out var t))
            throw new InvalidOperationException("Birth window dates are not valid.");
        if (t < f)
            throw new InvalidOperationException("The birth window must end after it starts.");
    }

    public async Task<string?> UpdateSeasonAsync(UpdateBreedingSeasonViewModel model, CancellationToken cancellationToken = default)
    {
        string? newBatchId = null;
        var season = await LoadSeasonSettingsAsync(cancellationToken);
        if (model.BuildUp is > 0) season.BuildUp = model.BuildUp.Value;
        if (model.BuckCheckDays is >= 0) season.BuckCheckDays = model.BuckCheckDays.Value;
        if (model.WeighYoungDoesDays is >= 0) season.WeighYoungDoesDays = model.WeighYoungDoesDays.Value;
        if (model.MinYoungDoeKg is > 0) season.MinYoungDoeKg = model.MinYoungDoeKg.Value;
        if (model.KiddingPensDays is >= 0) season.KiddingPensDays = model.KiddingPensDays.Value;
        if (model.ScanAfter is > 0) season.ScanAfter = model.ScanAfter.Value;
        if (model.DryOff is > 0) season.DryOff = model.DryOff.Value;
        if (model.WeanDays is > 0) season.WeanDays = model.WeanDays.Value;
        if (model.WeanKg is > 0) season.WeanKg = model.WeanKg.Value;
        if (model.DryBeforeCross is >= 0) season.DryBeforeCross = model.DryBeforeCross.Value;
        if (model.AutoWean.HasValue) season.AutoWean = model.AutoWean.Value;
        if (!string.IsNullOrWhiteSpace(model.ReportTargetKey) && model.ReportTargetValue is > 0)
        {
            season.Targets ??= new BreedingReportTargets();
            season.Targets.Set(model.ReportTargetKey.Trim(), model.ReportTargetValue.Value);
        }

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
            var template = season.Batches.FirstOrDefault(b => b.Id == model.CopyFromBatchId)
                ?? batch
                ?? season.Batches.FirstOrDefault();
            var from = model.BatchFrom?.Trim();
            var to = model.BatchTo?.Trim();
            if (string.IsNullOrWhiteSpace(from)) from = template?.From ?? "06-01";
            if (string.IsNullOrWhiteSpace(to)) to = template?.To ?? "06-15";
            if (from!.Length > 5) from = from[^5..]; // yyyy-MM-dd → MM-dd
            if (to!.Length > 5) to = to[^5..];
            ValidateBirthWindow(from, to);

            newBatchId = "b" + Guid.NewGuid().ToString("N")[..8];
            var icon = string.IsNullOrWhiteSpace(model.NewBatchIcon) ? template?.Icon ?? "⭐" : model.NewBatchIcon.Trim();
            season.Batches.Add(new BreedingSeasonBatch
            {
                Id = newBatchId,
                Name = model.NewBatchName.Trim(),
                Icon = icon,
                From = from,
                To = to
            });
        }

        if (model.DuplicateBatch == true && batch is not null)
        {
            newBatchId = "b" + Guid.NewGuid().ToString("N")[..8];
            season.Batches.Add(new BreedingSeasonBatch
            {
                Id = newBatchId,
                Name = batch.Name.Trim() + " (copy)",
                Icon = batch.Icon,
                From = batch.From,
                To = batch.To
            });
        }

        await SaveSeasonSettingsAsync(season, cancellationToken);
        return newBatchId;
    }

    public async Task<PregnancyReconcileResultViewModel> ReconcilePregnancyAsync(CancellationToken cancellationToken = default)
    {
        var goats = await _context.Goats.Where(g => !g.IsArchived && g.Gender == GoatGender.Female).ToListAsync(cancellationToken);
        var events = await _context.BreedingEvents.AsNoTracking().ToListAsync(cancellationToken);
        var adjusted = 0;

        foreach (var g in goats)
        {
            var changed = false;
            if (!g.MatedDate.HasValue)
            {
                var ev = events.Where(e => e.DoeId == g.Id)
                    .OrderByDescending(e => e.MatedDate)
                    .FirstOrDefault(e =>
                        e.Outcome == BreedingOutcome.ConfirmedPregnant
                        || (e.Outcome == BreedingOutcome.Open && e.ScanKids is > 0));
                if (ev is not null)
                {
                    g.MatedDate = ev.MatedDate;
                    g.BuckTag ??= ev.BuckTagText;
                    if (ev.ScanKids is > 0)
                        g.KidsCount ??= ev.ScanKids;
                    changed = true;
                }
            }

            if (PregnancyRules.IsConfirmedPregnant(g) && g.Status != GoatStatus.Pregnant)
            {
                g.Status = GoatStatus.Pregnant;
                g.BreedingStage = BreedingStage.ConfirmedPregnant;
                changed = true;
            }
            else if (g.Status == GoatStatus.Pregnant && !PregnancyRules.IsConfirmedPregnant(g))
            {
                g.Status = GoatStatus.Dry;
                changed = true;
            }

            if (changed)
            {
                g.UpdatedDate = DateTime.UtcNow;
                adjusted++;
            }
        }

        if (adjusted > 0)
            await _context.SaveChangesAsync(cancellationToken);

        return new PregnancyReconcileResultViewModel
        {
            Adjusted = adjusted,
            Message = adjusted == 0
                ? "Herd pregnancy status already matches breeding records."
                : $"Adjusted pregnancy data on {adjusted} doe(s)."
        };
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
                    Date = model.Date,
                    EmptyNextStep = model.KidsCount.Value == 0 ? model.EmptyNextStep : null,
                    EmptyBirthBatchId = model.KidsCount.Value == 0 ? model.EmptyBirthBatchId : null
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
                if (!goat.MatedDate.HasValue && !goat.UltrasoundDate.HasValue)
                {
                    goat.NextCrossDate = null;
                    goat.PrepCrossDate = null;
                    if (goat.BreedingStage != BreedingStage.None)
                        goat.BreedingStage = BreedingStage.None;
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

    public async Task<UpdateKiddingDamResultViewModel> UpdateKiddingDamAsync(UpdateKiddingDamViewModel model, CancellationToken cancellationToken = default)
    {
        var record = await _context.KiddingRecords.FirstOrDefaultAsync(k => k.Id == model.KiddingRecordId, cancellationToken)
            ?? throw new InvalidOperationException("Kidding record not found.");
        if (model.Mothering is < 1 or > 5 || model.MilkProduction is < 1 or > 5 || model.BodyCondition is < 1 or > 5)
            throw new InvalidOperationException("Mothering, milk, and body condition must be between 1 and 5.");

        if (model.Mothering.HasValue) record.Mothering = model.Mothering;
        if (model.MilkProduction.HasValue) record.MilkProduction = model.MilkProduction;
        if (model.BodyCondition.HasValue) record.BodyCondition = model.BodyCondition;
        if (model.DamHealth.HasValue)
        {
            record.DamHealth = model.DamHealth.Value;
            if (model.DamHealth == DamAfterKiddingHealth.Good)
                record.DamHealthIssues = null;
        }

        if (model.DamHealthIssues is not null)
        {
            var issues = string.Join(", ", model.DamHealthIssues.Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s.Trim()));
            record.DamHealthIssues = string.IsNullOrWhiteSpace(issues) ? null : issues;
        }

        if (model.KeepDecision.HasValue)
        {
            record.KeepDecision = model.KeepDecision.Value;
            if (model.KeepDecision == DamKeepDecision.Keep)
                record.KeepReason = null;
        }

        if (model.KeepReason is not null)
            record.KeepReason = string.IsNullOrWhiteSpace(model.KeepReason) ? null : model.KeepReason.Trim();

        record.UpdatedDate = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);
        return new UpdateKiddingDamResultViewModel
        {
            MotherScoreHtml = MotherScoreHelper.StarsHtml(MotherScoreHelper.ScoreForKidding(record))
        };
    }

    public async Task<MotherGroupDto> CreateMotherGroupAsync(CreateMotherGroupViewModel model, CancellationToken cancellationToken = default)
    {
        var name = model.Name.Trim();
        if (name.Length < 2)
            throw new InvalidOperationException("Enter a group name.");
        var groups = await LoadMotherGroupsAsync(cancellationToken);
        var cols = new[] { "#A3375A", "#C05A21", "#3D7F8C", "#6B7A2E", "#7B5EA7" };
        var g = new MotherGroupDto
        {
            Id = "mx" + Guid.NewGuid().ToString("N")[..8],
            Name = name,
            Color = cols[groups.Count % cols.Length],
            Order = groups.Count + 1
        };
        groups.Add(g);
        await SaveMotherGroupsAsync(groups, cancellationToken);
        return g;
    }

    public async Task<int> MoveMothersToGroupAsync(MoveMotherGroupViewModel model, CancellationToken cancellationToken = default)
    {
        if (model.DoeIds.Count == 0)
            throw new InvalidOperationException("Select at least one mother.");
        var groups = await LoadMotherGroupsAsync(cancellationToken);
        if (!string.IsNullOrWhiteSpace(model.MotherGroupId) &&
            groups.All(g => !string.Equals(g.Id, model.MotherGroupId, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException("Mother group not found.");

        var does = await _context.Goats.Where(g => model.DoeIds.Contains(g.Id)).ToListAsync(cancellationToken);
        foreach (var d in does)
        {
            d.MotherGroupId = string.IsNullOrWhiteSpace(model.MotherGroupId) ? null : model.MotherGroupId.Trim();
            d.UpdatedDate = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync(cancellationToken);
        return does.Count;
    }

    public async Task<int> AutoAssignMotherGroupsByLitterAsync(CancellationToken cancellationToken = default)
    {
        var groups = (await LoadMotherGroupsAsync(cancellationToken)).ToDictionary(g => g.Id, StringComparer.OrdinalIgnoreCase);
        var kiddings = await _context.KiddingRecords.AsNoTracking().ToListAsync(cancellationToken);
        var latest = kiddings
            .GroupBy(k => k.DoeId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(k => k.KiddingDate).First());
        var does = await _context.Goats
            .Where(g => g.Gender == GoatGender.Female && !g.IsArchived && g.MotherGroupId == null)
            .ToListAsync(cancellationToken);
        var n = 0;
        foreach (var d in does)
        {
            if (!latest.TryGetValue(d.Id, out var k)) continue;
            var born = k.LiveKids + k.Stillborn;
            var sug = MotherGroupHelper.SuggestGroupId(born);
            if (sug is null || !groups.ContainsKey(sug)) continue;
            d.MotherGroupId = sug;
            d.UpdatedDate = DateTime.UtcNow;
            n++;
        }

        if (n > 0)
            await _context.SaveChangesAsync(cancellationToken);
        return n;
    }

    public async Task UpdateKidFieldAsync(UpdateKidFieldViewModel model, CancellationToken cancellationToken = default)
    {
        var kid = await _context.Goats.FindAsync([model.GoatId], cancellationToken)
            ?? throw new InvalidOperationException("Kid not found.");
        if (kid.Status != GoatStatus.Kid || kid.IsArchived)
            throw new InvalidOperationException($"{kid.Tag} is not an active kid.");

        switch (model.Field.ToLowerInvariant())
        {
            case "milktype":
            case "milk":
                if (!Enum.TryParse<KidMilkType>(model.Value, true, out var milk))
                    throw new InvalidOperationException("Invalid milk type.");
                kid.KidMilkType = milk;
                break;
            case "health":
            case "kidhealth":
                if (!Enum.TryParse<KidHealthTag>(model.Value, true, out var health))
                    throw new InvalidOperationException("Invalid health.");
                kid.KidHealthTag = health;
                break;
            default:
                throw new InvalidOperationException("Unknown field.");
        }

        kid.UpdatedDate = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task RecordKidWeanWeightAsync(RecordKidWeanWeightViewModel model, CancellationToken cancellationToken = default)
    {
        if (model.Kg is <= 0 or > 80)
            throw new InvalidOperationException("Enter a realistic wean weight in kg.");
        var kid = await _context.Goats
            .Include(g => g.WeightRecords)
            .FirstOrDefaultAsync(g => g.Id == model.GoatId, cancellationToken)
            ?? throw new InvalidOperationException("Kid not found.");
        if (!kid.WeanDate.HasValue)
            throw new InvalidOperationException("Wean this kid before recording wean weight.");
        var when = kid.WeanDate.Value;
        var existing = kid.WeightRecords.FirstOrDefault(w => w.Date == when);
        if (existing is not null)
            existing.Kg = model.Kg;
        else
            kid.WeightRecords.Add(new GoatWeightRecord { Date = when, Kg = model.Kg });
        kid.UpdatedDate = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task BulkWeanKidsAsync(BulkWeanKidsViewModel model, CancellationToken cancellationToken = default)
    {
        if (model.GoatIds.Count == 0)
            throw new InvalidOperationException("Select at least one kid.");
        var when = model.WeanDate ?? Today;
        if (when > Today)
            throw new InvalidOperationException("Wean date cannot be in the future.");
        var kids = await _context.Goats.Where(g => model.GoatIds.Contains(g.Id)).ToListAsync(cancellationToken);
        if (kids.Count != model.GoatIds.Count)
            throw new InvalidOperationException("One or more kids were not found.");
        foreach (var kid in kids)
        {
            if (kid.Status != GoatStatus.Kid)
                throw new InvalidOperationException($"{kid.Tag} is not a kid.");
            if (kid.WeanDate.HasValue) continue;
            kid.WeanDate = when;
            kid.WeanMethod = "early";
            kid.UpdatedDate = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateKidWeanRuleAsync(UpdateKidWeanRuleViewModel model, CancellationToken cancellationToken = default)
    {
        var season = await LoadSeasonSettingsAsync(cancellationToken);
        if (model.WeanDays is > 0 and <= 365)
            season.WeanDays = model.WeanDays.Value;
        if (model.AutoWean.HasValue)
            season.AutoWean = model.AutoWean.Value;
        await SaveSeasonSettingsAsync(season, cancellationToken);
    }

    public async Task RecordBuckCheckAsync(RecordBuckCheckViewModel model, CancellationToken cancellationToken = default)
    {
        if (model.CheckDate > Today)
            throw new InvalidOperationException("Check date cannot be in the future.");
        var buck = await _context.Goats.FindAsync([model.BuckId], cancellationToken)
            ?? throw new InvalidOperationException("Buck not found.");
        if (buck.Gender != GoatGender.Male || buck.IsArchived)
            throw new InvalidOperationException("That animal is not an active buck.");

        var problem = string.Equals(model.Feet, "problem", StringComparison.OrdinalIgnoreCase)
            || string.Equals(model.Testicles, "problem", StringComparison.OrdinalIgnoreCase)
            || model.Bcs is < 2.5m;
        if (problem && !model.ConfirmProblem)
            throw new ConfirmationRequiredException(["Problem noted on feet, testicles, or body condition under 2.5."]);

        _context.BuckHealthChecks.Add(new BuckHealthCheck
        {
            BuckId = buck.Id,
            CheckDate = model.CheckDate,
            WeightKg = model.WeightKg,
            Bcs = model.Bcs,
            Feet = model.Feet,
            Teeth = model.Teeth,
            Testicles = model.Testicles,
            Notes = model.Notes
        });
        if (model.WeightKg is > 0)
        {
            _context.GoatWeightRecords.Add(new GoatWeightRecord
            {
                GoatId = buck.Id,
                Date = model.CheckDate,
                Kg = model.WeightKg.Value
            });
        }
        if (model.Bcs is > 0)
        {
            _context.GoatBcsRecords.Add(new GoatBcsRecord
            {
                GoatId = buck.Id,
                Date = model.CheckDate,
                Score = (int)Math.Round(model.Bcs.Value)
            });
        }

        buck.UpdatedDate = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<string> CreateBreedingGroupAsync(CreateBreedingGroupViewModel model, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(model.BuckTag))
            throw new InvalidOperationException("Enter the buck tag.");
        if (model.StartDate == default)
            throw new InvalidOperationException("Pick a cross date.");
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

        if (!model.DeferCross)
        {
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
        }

        var season = await LoadSeasonSettingsAsync(cancellationToken);
        var birthBatch = season.Batches.FirstOrDefault(b => b.Id == model.BirthBatchId)
            ?? season.Batches.FirstOrDefault();

        var stay = Math.Clamp(model.StayDays, 1, 120);
        var end = model.StartDate.AddDays(stay);
        var n = groups.Count(g => g.Name.StartsWith("Breeding Group", StringComparison.OrdinalIgnoreCase)) + 1;
        var members = new List<BreedingGroupMemberDto>();
        foreach (var id in take)
        {
            var g = await _context.Goats.FindAsync([id], cancellationToken)
                ?? throw new InvalidOperationException("Doe not found.");
            if (birthBatch is not null)
                g.BirthBatchName = birthBatch.Name;
            g.UpdatedDate = DateTime.UtcNow;
            members.Add(new BreedingGroupMemberDto { GoatId = id, Tag = g.Tag });
        }
        if (birthBatch is not null)
            await _context.SaveChangesAsync(cancellationToken);

        var groupId = Guid.NewGuid().ToString("N");
        groups.Add(new BreedingGroupDto
        {
            Id = groupId,
            Name = "Breeding Group " + n.ToString("000"),
            Buck = model.BuckTag,
            Start = model.DeferCross ? "" : model.StartDate.ToString("yyyy-MM-dd"),
            End = model.DeferCross ? "" : end.ToString("yyyy-MM-dd"),
            StayDays = stay,
            CrossDeferred = model.DeferCross,
            Max = model.Max,
            Status = "active",
            BirthBatchId = birthBatch?.Id,
            Members = members
        });
        await SaveBreedingGroupsAsync(groups, cancellationToken);
        return groupId;
    }

    public async Task<IReadOnlyList<int>> PutBuckInGroupAsync(BuckInBreedingGroupViewModel model, CancellationToken cancellationToken = default)
    {
        var groups = await LoadBreedingGroupsAsync(cancellationToken);
        var grp = groups.FirstOrDefault(g => g.Id == model.GroupId)
            ?? throw new InvalidOperationException("Group not found.");
        if (grp.Status != "active")
            throw new InvalidOperationException($"{grp.Name} is not active.");
        var inDate = model.InDate ?? Today;
        if (inDate > Today)
            throw new InvalidOperationException("Buck-in date cannot be in the future.");

        var buck = await BreedingCycleWriter.FindBuckAsync(_context, grp.Buck, cancellationToken)
            ?? throw new InvalidOperationException("Buck not found in herd.");
        var pedigree = await BreedingCycleWriter.LoadPedigreeAsync(_context, cancellationToken);
        var warnings = new List<string>();
        var checks = await _context.BuckHealthChecks.AsNoTracking()
            .Where(c => c.BuckId == buck.Id)
            .ToListAsync(cancellationToken);
        if (!BuckCheckHelper.IsOk(BuckCheckHelper.LatestCheck(checks, buck.Id), inDate))
            warnings.Add("Buck check not done in the last 60 days.");

        foreach (var m in grp.Members)
        {
            var doe = await _context.Goats.FindAsync([m.GoatId], cancellationToken);
            if (doe is null || doe.IsArchived) continue;
            if (doe.MatedDate.HasValue && (!grp.CrossDeferred || !string.IsNullOrWhiteSpace(grp.Start)))
                continue;
            var plan = BreedingCycleWriter.PlanCross(doe, buck, grp.Buck, inDate, Today, pedigree);
            if (plan.Check.Errors.Count > 0)
                throw new InvalidOperationException($"{doe.Tag}: {string.Join(" ", plan.Check.Errors)}");
            warnings.AddRange(plan.Check.Warnings);
        }

        if (warnings.Count > 0 && !model.ConfirmWarnings)
            throw new ConfirmationRequiredException(warnings.Distinct().ToList());

        var crossedIds = new List<int>();
        foreach (var m in grp.Members)
        {
            var doe = await _context.Goats.FindAsync([m.GoatId], cancellationToken);
            if (doe is null || doe.IsArchived) continue;
            if (doe.MatedDate.HasValue && (!grp.CrossDeferred || !string.IsNullOrWhiteSpace(grp.Start)))
                continue;
            var plan = BreedingCycleWriter.PlanCross(doe, buck, grp.Buck, inDate, Today, pedigree);
            var open = await BreedingCycleWriter.OpenEventsAsync(_context, doe.Id, cancellationToken);
            BreedingCycleWriter.ApplyCross(_context, doe, inDate, plan, open);
            crossedIds.Add(doe.Id);
        }

        grp.Start = inDate.ToString("yyyy-MM-dd");
        grp.End = inDate.AddDays(Math.Clamp(grp.StayDays, 1, 120)).ToString("yyyy-MM-dd");
        grp.CrossDeferred = false;
        await _context.SaveChangesAsync(cancellationToken);
        await SaveBreedingGroupsAsync(groups, cancellationToken);
        return crossedIds;
    }

    public async Task ReopenBreedingGroupAsync(string groupId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(groupId))
            throw new InvalidOperationException("Group not found.");
        var groups = await LoadBreedingGroupsAsync(cancellationToken);
        var grp = groups.FirstOrDefault(g => g.Id == groupId)
            ?? throw new InvalidOperationException("Group not found.");
        if (grp.Status != "closed")
            throw new InvalidOperationException($"{grp.Name} is still active.");
        grp.Status = "active";
        await SaveBreedingGroupsAsync(groups, cancellationToken);
    }

    public async Task UndoGroupCrossesAsync(IReadOnlyList<int> doeIds, CancellationToken cancellationToken = default)
    {
        foreach (var id in doeIds.Distinct())
            await RemoveCrossAsync(id, cancellationToken);
    }

    public async Task TakeBuckOutGroupAsync(CloseBreedingGroupViewModel model, CancellationToken cancellationToken = default) =>
        await CloseBreedingGroupAsync(model, cancellationToken);

    public async Task CloseBreedingGroupAsync(CloseBreedingGroupViewModel model, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(model.GroupId))
            throw new InvalidOperationException("Group not found.");
        var groups = await LoadBreedingGroupsAsync(cancellationToken);
        var grp = groups.FirstOrDefault(g => g.Id == model.GroupId)
            ?? throw new InvalidOperationException("Group not found.");
        if (grp.Status != "active")
            throw new InvalidOperationException($"{grp.Name} is already closed.");
        var end = model.EndDate ?? Today;
        if (end > Today)
            throw new InvalidOperationException("Buck-out date cannot be in the future.");
        grp.Status = "closed";
        grp.End = end.ToString("yyyy-MM-dd");
        await SaveBreedingGroupsAsync(groups, cancellationToken);
    }

    public async Task DeleteBreedingGroupAsync(string groupId, CancellationToken cancellationToken = default)
    {
        var groups = await LoadBreedingGroupsAsync(cancellationToken);
        var grp = groups.FirstOrDefault(g => g.Id == groupId)
            ?? throw new InvalidOperationException("Group not found.");

        var memberIds = grp.Members.Select(m => m.GoatId).ToList();
        var goats = await _context.Goats.Where(g => memberIds.Contains(g.Id)).ToListAsync(cancellationToken);
        var emptyLogs = await _context.BreedingEmptyLogs.AsNoTracking()
            .Where(l => memberIds.Contains(l.GoatId)).ToListAsync(cancellationToken);
        var kiddings = await _context.KiddingRecords.AsNoTracking()
            .Where(k => memberIds.Contains(k.DoeId)).ToListAsync(cancellationToken);

        foreach (var m in grp.Members)
        {
            var outcome = BreedingGroupHelper.ResolveOutcome(grp, m, goats.FirstOrDefault(g => g.Id == m.GoatId), emptyLogs, kiddings);
            if (outcome.Key is "preg" or "empty" or "kidded")
                throw new InvalidOperationException(
                    $"{grp.Name} cannot be deleted: one or more does already have scan or birth results. Close the group or remove only does still awaiting scan.");
        }

        foreach (var m in grp.Members)
        {
            var goat = goats.FirstOrDefault(g => g.Id == m.GoatId);
            if (goat is null) continue;
            var outcome = BreedingGroupHelper.ResolveOutcome(grp, m, goat, emptyLogs, kiddings);
            if (outcome.Key != "wait") continue;
            var open = await BreedingCycleWriter.OpenEventsAsync(_context, goat.Id, cancellationToken);
            BreedingCycleWriter.CloseCycle(goat, open, BreedingOutcome.Cancelled, Today, "breeding group deleted");
            if (goat.Status == GoatStatus.Pregnant)
                goat.Status = GoatStatus.Dry;
        }

        groups.RemoveAll(g => g.Id == groupId);
        await _context.SaveChangesAsync(cancellationToken);
        await SaveBreedingGroupsAsync(groups, cancellationToken);
    }

    public async Task RemoveBreedingGroupMemberAsync(BreedingGroupMemberActionViewModel model, CancellationToken cancellationToken = default)
    {
        var groups = await LoadBreedingGroupsAsync(cancellationToken);
        var grp = groups.FirstOrDefault(g => g.Id == model.GroupId)
            ?? throw new InvalidOperationException("Group not found.");
        var member = grp.Members.FirstOrDefault(m => m.GoatId == model.GoatId)
            ?? throw new InvalidOperationException("Doe is not in this group.");

        var goat = await _context.Goats.FindAsync([model.GoatId], cancellationToken);
        var emptyLogs = await _context.BreedingEmptyLogs.AsNoTracking()
            .Where(l => l.GoatId == model.GoatId).ToListAsync(cancellationToken);
        var kiddings = await _context.KiddingRecords.AsNoTracking()
            .Where(k => k.DoeId == model.GoatId).ToListAsync(cancellationToken);
        var outcome = BreedingGroupHelper.ResolveOutcome(grp, member, goat, emptyLogs, kiddings);
        if (outcome.Key != "wait")
            throw new InvalidOperationException("This doe already has a scan or birth result — her record stays in the group.");

        if (goat is not null && PregnancyRules.IsAwaitingScan(goat))
        {
            var open = await BreedingCycleWriter.OpenEventsAsync(_context, goat.Id, cancellationToken);
            BreedingCycleWriter.CloseCycle(goat, open, BreedingOutcome.Cancelled, Today, "removed from breeding group");
            if (goat.Status == GoatStatus.Pregnant)
                goat.Status = GoatStatus.Dry;
            goat.PrepCrossDate = BreedingRules.EligibilityError(goat, Today) is null ? Today : goat.PrepCrossDate;
            goat.BreedingStage = goat.PrepCrossDate.HasValue ? BreedingStage.Ready : BreedingStage.None;
        }

        grp.Members.RemoveAll(m => m.GoatId == model.GoatId);
        await _context.SaveChangesAsync(cancellationToken);
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

    private static string? ResolveGroupBatchId(
        BreedingGroupDto grp,
        IReadOnlyDictionary<int, Goat> goats,
        BreedingSeasonSettings season)
    {
        if (!string.IsNullOrWhiteSpace(grp.BirthBatchId))
            return grp.BirthBatchId;
        var names = grp.Members
            .Select(m => goats.GetValueOrDefault(m.GoatId)?.BirthBatchName)
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .ToList();
        if (names.Count == 0)
            return season.Batches.FirstOrDefault()?.Id;
        var top = names
            .GroupBy(n => n!, StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(g => g.Count())
            .First().Key;
        return season.Batches.FirstOrDefault(b => string.Equals(b.Name, top, StringComparison.OrdinalIgnoreCase))?.Id
            ?? season.Batches.FirstOrDefault()?.Id;
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

    private async Task<List<MotherGroupDto>> LoadMotherGroupsAsync(CancellationToken cancellationToken)
    {
        var row = await _context.AppSettings.AsNoTracking()
            .FirstOrDefaultAsync(s => s.Key == AppSettingKeys.MotherGroups, cancellationToken);
        if (row?.Value is null)
        {
            var defaults = MotherGroupHelper.Defaults.ToList();
            await SaveMotherGroupsAsync(defaults, cancellationToken);
            return defaults;
        }

        return JsonSerializer.Deserialize<List<MotherGroupDto>>(row.Value) ?? MotherGroupHelper.Defaults.ToList();
    }

    private async Task SaveMotherGroupsAsync(List<MotherGroupDto> groups, CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(groups);
        var row = await _context.AppSettings.FirstOrDefaultAsync(s => s.Key == AppSettingKeys.MotherGroups, cancellationToken);
        if (row is null)
            _context.AppSettings.Add(new AppSetting { Key = AppSettingKeys.MotherGroups, Value = json });
        else row.Value = json;
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<BreedingGroupCardViewModel>> GetBreedingGroupCardsAsync(
        CancellationToken cancellationToken = default)
    {
        var groups = await LoadBreedingGroupsAsync(cancellationToken);
        if (groups.Count == 0)
            return [];

        var season = await LoadSeasonSettingsAsync(cancellationToken);
        var memberIds = groups.SelectMany(g => g.Members).Select(m => m.GoatId).Distinct().ToList();
        var goats = await _context.Goats.AsNoTracking()
            .Where(g => memberIds.Contains(g.Id))
            .ToDictionaryAsync(g => g.Id, cancellationToken);
        var emptyLogs = await _context.BreedingEmptyLogs.AsNoTracking()
            .Where(l => memberIds.Contains(l.GoatId))
            .ToListAsync(cancellationToken);
        var kiddings = await _context.KiddingRecords.AsNoTracking()
            .Where(k => memberIds.Contains(k.DoeId))
            .ToListAsync(cancellationToken);

        var cards = new List<BreedingGroupCardViewModel>();
        foreach (var grp in groups.OrderByDescending(g => g.Status == "active").ThenByDescending(g => g.Start))
        {
            var members = new List<BreedingGroupMemberCardViewModel>();
            var counts = new Dictionary<string, int>
            {
                ["wait"] = 0, ["preg"] = 0, ["empty"] = 0, ["kidded"] = 0, ["gone"] = 0, ["other"] = 0
            };
            var kidsAlive = 0;
            foreach (var m in grp.Members)
            {
                goats.TryGetValue(m.GoatId, out var goat);
                var outcome = BreedingGroupHelper.ResolveOutcome(grp, m, goat, emptyLogs, kiddings);
                counts[outcome.Key] = counts.GetValueOrDefault(outcome.Key) + 1;
                kidsAlive += outcome.KidsAlive;
                members.Add(new BreedingGroupMemberCardViewModel
                {
                    GoatId = m.GoatId,
                    Tag = m.Tag,
                    Breed = goat?.Breed ?? "",
                    OutcomeKey = outcome.Key,
                    OutcomeText = outcome.Text,
                    CanRemove = outcome.Key == "wait" && grp.Status == "active"
                });
            }

            var scanned = counts["preg"] + counts["empty"] + counts["kidded"];
            var confirmed = counts["preg"] + counts["kidded"];
            int? rate = scanned > 0 ? (int)Math.Round(confirmed / (double)scanned * 100) : null;
            var finished = counts["preg"] + counts["empty"] + counts["kidded"];
            var canBuckIn = grp.Status == "active" && grp.CrossDeferred && string.IsNullOrWhiteSpace(grp.Start);
            var batchId = ResolveGroupBatchId(grp, goats, season);
            var batch = season.Batches.FirstOrDefault(b => b.Id == batchId);

            cards.Add(new BreedingGroupCardViewModel
            {
                Id = grp.Id,
                Name = grp.Name,
                Buck = grp.Buck,
                BirthBatchId = batchId,
                BirthBatchName = batch?.Name,
                BirthBatchIcon = string.IsNullOrWhiteSpace(batch?.Icon) ? "⭐" : batch!.Icon,
                Start = grp.Start,
                End = string.IsNullOrWhiteSpace(grp.End) ? null : grp.End,
                Max = grp.Max,
                Status = grp.Status,
                MemberCount = grp.Members.Count,
                WaitingScan = counts["wait"],
                Pregnant = counts["preg"],
                Empty = counts["empty"],
                Kidded = counts["kidded"],
                Gone = counts["gone"] + counts["other"],
                KidsAlive = kidsAlive,
                ConceptionPercent = rate,
                StayDays = grp.StayDays > 0 ? grp.StayDays : 42,
                CrossDeferred = grp.CrossDeferred,
                CanClose = grp.Status == "active",
                CanDelete = finished == 0,
                CanBuckIn = canBuckIn,
                Members = members
            });
        }

        return cards;
    }

    public async Task<BreedingScanSheetViewModel> GetScanSheetAsync(CancellationToken cancellationToken = default)
    {
        var today = Today;
        var season = await LoadSeasonSettingsAsync(cancellationToken);
        var birthBatches = season.Batches.Select(b => new BreedingScanSeasonBatchOptionViewModel
        {
            Id = b.Id,
            Name = b.Name,
            Icon = string.IsNullOrWhiteSpace(b.Icon) ? "⭐" : b.Icon
        }).ToList();

        var waiting = await _context.Goats.AsNoTracking()
            .Where(g => !g.IsArchived && g.Gender == GoatGender.Female && g.MatedDate != null && (g.KidsCount == null || g.KidsCount <= 0))
            .OrderBy(g => g.Tag)
            .ToListAsync(cancellationToken);

        var kiddingCounts = await _context.KiddingRecords.AsNoTracking()
            .GroupBy(k => k.DoeId)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count, cancellationToken);

        var rescanDoeIds = (await _context.BreedingEvents.AsNoTracking()
            .Where(e => e.Outcome == BreedingOutcome.Open && e.Notes != null && e.Notes.Contains("inconclusive"))
            .Select(e => e.DoeId)
            .Distinct()
            .ToListAsync(cancellationToken))
            .ToHashSet();

        var groups = await LoadBreedingGroupsAsync(cancellationToken);
        var goatsById = waiting.ToDictionary(g => g.Id);
        var assigned = new HashSet<int>();
        var scanGroups = new List<BreedingScanGroupViewModel>();

        foreach (var grp in groups.Where(g => g.Status == "active").OrderBy(g => g.Name))
        {
            var does = new List<BreedingScanDoeViewModel>();
            foreach (var m in grp.Members)
            {
                if (!goatsById.TryGetValue(m.GoatId, out var g)) continue;
                assigned.Add(g.Id);
                does.Add(MapScanDoe(g, today, kiddingCounts.GetValueOrDefault(g.Id), rescanDoeIds.Contains(g.Id)));
            }

            if (does.Count == 0) continue;

            var batchId = ResolveGroupBatchId(grp, goatsById, season);
            var batch = season.Batches.FirstOrDefault(b => b.Id == batchId);
            var buckStill = BuckStillWithDoes(grp, today);

            scanGroups.Add(new BreedingScanGroupViewModel
            {
                Id = grp.Id,
                Name = grp.Name,
                Buck = grp.Buck,
                BirthBatchIcon = batch?.Icon is { Length: > 0 } ic ? ic : "⭐",
                BuckStillWithDoes = buckStill,
                Does = does
            });
        }

        var ungrouped = waiting
            .Where(g => !assigned.Contains(g.Id))
            .Select(g => MapScanDoe(g, today, kiddingCounts.GetValueOrDefault(g.Id), rescanDoeIds.Contains(g.Id)))
            .ToList();

        return new BreedingScanSheetViewModel
        {
            Groups = scanGroups,
            Ungrouped = ungrouped,
            BirthBatches = birthBatches
        };
    }

    private static bool BuckStillWithDoes(BreedingGroupDto grp, DateOnly today)
    {
        if (grp.CrossDeferred || string.IsNullOrWhiteSpace(grp.Start)) return false;
        if (!BreedingGroupHelper.TryParseStart(grp.Start, out var buckIn) || buckIn > today) return false;
        if (string.IsNullOrWhiteSpace(grp.End)) return true;
        return BreedingGroupHelper.TryParseStart(grp.End, out var buckOut) && buckOut >= today;
    }

    public async Task<BulkScanSaveResultViewModel> SaveScanBatchAsync(
        BulkScanSaveViewModel model, CancellationToken cancellationToken = default)
    {
        if (model.Results.Count == 0)
            throw new InvalidOperationException("Tick at least one scan result.");
        if (model.ScanDate > Today)
            throw new InvalidOperationException("The scan date cannot be in the future.");

        var errors = new List<string>();
        var saved = 0;
        foreach (var item in model.Results)
        {
            var goat = await _context.Goats.FindAsync([item.GoatId], cancellationToken);
            if (goat is null || goat.IsArchived)
            {
                errors.Add($"Goat #{item.GoatId}: not found.");
                continue;
            }

            if (!PregnancyRules.IsAwaitingScan(goat))
            {
                errors.Add($"{goat.Tag}: not waiting for scan (skipped).");
                continue;
            }

            var buckBeforeEmpty = goat.BuckTag;

            if (item.NotSure)
            {
                var days = Math.Clamp(item.RescanInDays, 1, 90);
                var due = model.ScanDate.AddDays(days);
                var open = await BreedingCycleWriter.OpenEventsAsync(_context, goat.Id, cancellationToken);
                foreach (var e in open)
                {
                    e.Notes = BreedingCycleWriter.AppendNote(e.Notes,
                        $"Scan inconclusive {model.ScanDate:yyyy-MM-dd}; rescan ~{due:yyyy-MM-dd}");
                    e.UpdatedDate = DateTime.UtcNow;
                }

                _context.FarmTasks.Add(new FarmTask
                {
                    Title = $"Rescan {goat.Tag}",
                    Notes = "Ultrasound was not sure — scan again.",
                    Type = FarmTaskType.Reminder,
                    Priority = FarmTaskPriority.Normal,
                    Status = FarmTaskStatus.ToDo,
                    DueDate = due,
                    GoatId = goat.Id
                });
                goat.UpdatedDate = DateTime.UtcNow;
                try
                {
                    await _context.SaveChangesAsync(cancellationToken);
                    saved++;
                }
                catch (InvalidOperationException ex)
                {
                    errors.Add($"{goat.Tag}: {ex.Message}");
                }

                continue;
            }

            if (item.KidsCount < 0 || item.KidsCount > BreedingHelper.MaxKidsPerBirth)
            {
                errors.Add($"Goat #{item.GoatId}: kids count must be 0–{BreedingHelper.MaxKidsPerBirth}.");
                continue;
            }

            try
            {
                if (item.KidsCount == 0)
                {
                    if (string.IsNullOrWhiteSpace(item.EmptyNextStep))
                    {
                        errors.Add($"{goat.Tag}: choose what to do after an empty scan.");
                        continue;
                    }

                    await RecordUltrasoundAsync(new RecordUltrasoundViewModel
                    {
                        Tag = goat.Tag,
                        KidsCount = 0,
                        Date = model.ScanDate,
                        EmptyNextStep = item.EmptyNextStep.Trim(),
                        EmptyBirthBatchId = item.EmptyBirthBatchId
                    }, cancellationToken);
                }
                else
                {
                    await RecordUltrasoundAsync(new RecordUltrasoundViewModel
                    {
                        Tag = goat.Tag,
                        KidsCount = item.KidsCount,
                        Date = model.ScanDate,
                        DaysPregnant = item.DaysPregnant
                    }, cancellationToken);
                }

                saved++;
            }
            catch (InvalidOperationException ex)
            {
                errors.Add($"{goat.Tag}: {ex.Message}");
            }
        }

        return new BulkScanSaveResultViewModel { Saved = saved, Errors = errors };
    }

    private async Task ApplyEmptyScanNextAsync(
        Goat goat,
        DateOnly scanDate,
        BulkScanResultItemViewModel item,
        string? buckTag,
        CancellationToken cancellationToken)
    {
        var step = (item.EmptyNextStep ?? "next").ToLowerInvariant();
        var season = await LoadSeasonSettingsAsync(cancellationToken);

        switch (step)
        {
            case "back":
                if (string.IsNullOrWhiteSpace(buckTag))
                    throw new InvalidOperationException("No buck on record — cannot send her back with the buck.");
                await CrossAsync(goat, scanDate, buckTag, false, cancellationToken);
                goat.Comment = AppendLine(goat.Comment, $"Empty scan {scanDate:yyyy-MM-dd} — back with {buckTag}");
                break;
            case "heat":
                goat.Comment = AppendLine(goat.Comment, $"Watch for heat after empty scan {scanDate:yyyy-MM-dd}");
                goat.BreedingStage = BreedingStage.None;
                break;
            case "cull":
                goat.Comment = AppendLine(goat.Comment, $"Cull list — empty scan {scanDate:yyyy-MM-dd}");
                _context.FarmTasks.Add(new FarmTask
                {
                    Title = $"Cull review — {goat.Tag}",
                    Notes = "Empty ultrasound — decide cull or retry.",
                    Type = FarmTaskType.Reminder,
                    Priority = FarmTaskPriority.High,
                    Status = FarmTaskStatus.ToDo,
                    DueDate = scanDate,
                    GoatId = goat.Id
                });
                break;
            case "next":
            default:
                var batchId = item.EmptyBirthBatchId ?? ResolveNextBirthBatchId(goat, season);
                var batch = season.Batches.FirstOrDefault(b => b.Id == batchId) ?? season.Batches.FirstOrDefault();
                if (batch is not null)
                    goat.BirthBatchName = batch.Name;
                goat.PrepCrossDate = scanDate;
                goat.BreedingStage = BreedingStage.Ready;
                goat.Comment = AppendLine(goat.Comment,
                    batch is null
                        ? $"Empty scan {scanDate:yyyy-MM-dd} — ready for next cross"
                        : $"Empty scan {scanDate:yyyy-MM-dd} — next: {batch.Icon} {batch.Name}");
                break;
        }

        goat.UpdatedDate = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);
    }

    private static string? ResolveNextBirthBatchId(Goat doe, BreedingSeasonSettings season)
    {
        if (season.Batches.Count == 0) return null;
        if (string.IsNullOrWhiteSpace(doe.BirthBatchName))
            return season.Batches[0].Id;
        var idx = season.Batches.FindIndex(b =>
            string.Equals(b.Name, doe.BirthBatchName, StringComparison.OrdinalIgnoreCase));
        if (idx < 0) return season.Batches[0].Id;
        return season.Batches[(idx + 1) % season.Batches.Count].Id;
    }

    private static string? AppendLine(string? existing, string line) =>
        string.IsNullOrWhiteSpace(existing) ? line : $"{existing.Trim()}\n{line}";

    private static BreedingScanDoeViewModel MapScanDoe(Goat g, DateOnly today, int kiddings, bool awaitingRescan)
    {
        var k = kiddings;
        return new BreedingScanDoeViewModel
        {
            Id = g.Id,
            Tag = g.Tag,
            Breed = g.Breed ?? string.Empty,
            BuckTag = g.BuckTag,
            MatedDate = g.MatedDate?.ToString("yyyy-MM-dd"),
            DaysSinceCross = g.MatedDate.HasValue ? today.DayNumber - g.MatedDate.Value.DayNumber : 0,
            Kiddings = k,
            KiddingsText = k == 0 ? "first time" : $"{k}× kidded",
            AwaitingRescan = awaitingRescan
        };
    }
}
