using System.Text.Json;
using GoatFarm.Application.Common;
using GoatFarm.Application.Interfaces;
using GoatFarm.Application.ViewModels.Breeding;
using GoatFarm.Application.ViewModels.Goats;
using GoatFarm.Domain.Constants;
using GoatFarm.Domain.Entities;
using GoatFarm.Domain.Enums;
using GoatFarm.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GoatFarm.Infrastructure.Services;

public class GoatService : IGoatService
{
    private const int DefaultPageSize = 10;
    private readonly GoatFarmDbContext _context;
    private Dictionary<GoatStatus, int>? _liveStatusCounts;
    private HerdBreedingContext? _herdBreedingCtx;

    public GoatService(GoatFarmDbContext context)
    {
        _context = context;
        // Any save in this request may change a status or archive a goat.
        _context.SavedChanges += (_, _) =>
        {
            _liveStatusCounts = null;
            _herdBreedingCtx = null;
        };
    }

    private const int MaxPageSize = 200;

    /// <summary>Filtering, counting, ordering and paging all run in the database; only one page of goats is loaded.</summary>
    public async Task<HerdPageViewModel> GetHerdPageAsync(
        string? filter,
        int page = 1,
        int pageSize = DefaultPageSize,
        string? search = null,
        CancellationToken cancellationToken = default)
    {
        pageSize = pageSize <= 0 ? DefaultPageSize : Math.Min(pageSize, MaxPageSize);
        search = string.IsNullOrWhiteSpace(search) ? null : search.Trim();

        var query = ApplySearch(await ApplyFilterAsync(LiveGoatsQuery(), filter, cancellationToken), search);
        var totalItems = await query.CountAsync(cancellationToken);
        var totalPages = Math.Max(1, (int)Math.Ceiling(totalItems / (double)pageSize));
        page = Math.Clamp(page, 1, totalPages);

        var goats = await query
            .OrderByDescending(g => g.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Include(g => g.Group).Include(g => g.Sire).Include(g => g.Dam).Include(g => g.CustomFeedingGroup)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var statusCounts = await LiveGoatsQuery()
            .GroupBy(g => g.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Status, x => x.Count, cancellationToken);
        var confirmedPregnant = await LiveGoatsQuery().CountAsync(PregnancyRules.ConfirmedPregnantExpression, cancellationToken);

        var groups = await GetGroupsAsync(cancellationToken);
        var batches = await GetBirthBatchesAsync(cancellationToken);
        var sireTags = await GetSireFilterTagsAsync(cancellationToken);
        var owners = await GetOwnerFiltersAsync(cancellationToken);
        var feedingGroups = await GetFeedingGroupOptionsAsync(cancellationToken);
        var filterKey = filter ?? "all";
        var breedingCtx = await EnsureHerdBreedingContextAsync(cancellationToken);
        var motherGroups = await LoadMotherGroupsAsync(cancellationToken);
        var weights = await LoadLatestWeightsAsync(goats.Select(g => g.Id).ToList(), cancellationToken);

        return new HerdPageViewModel
        {
            Stats = BuildStats(statusCounts, confirmedPregnant),
            Goats = MapGoatsWithWeights(goats, weights, breedingCtx),
            UnderTreatment = await LoadUnderTreatmentAsync(cancellationToken),
            Groups = groups,
            BirthBatches = batches,
            SireFilterTags = sireTags,
            OwnerFilters = owners,
            FeedingGroups = feedingGroups,
            MotherGroups = motherGroups,
            FilterOptions = await BuildFilterOptionsAsync(filterKey, statusCounts, batches, sireTags, owners, motherGroups, cancellationToken),
            Filter = filterKey,
            Search = search,
            Pagination = new PaginationViewModel
            {
                Page = page,
                PageSize = pageSize,
                TotalItems = totalItems
            },
            NewGoat = new CreateGoatViewModel { EventDate = DateOnly.FromDateTime(DateTime.Today) }
        };
    }

    public async Task<GoatViewModel?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var goat = await _context.Goats.Include(g => g.Group).Include(g => g.Sire).Include(g => g.Dam).AsNoTracking()
            .FirstOrDefaultAsync(g => g.Id == id, cancellationToken);
        return goat is null ? null : MapGoat(goat);
    }

    public async Task<GoatViewModel?> GetViewModelForCardAsync(int id, CancellationToken cancellationToken = default)
    {
        var goat = await _context.Goats.Include(g => g.Group).Include(g => g.Sire).Include(g => g.Dam)
            .Include(g => g.CustomFeedingGroup).AsNoTracking()
            .FirstOrDefaultAsync(g => g.Id == id, cancellationToken);
        if (goat is null)
            return null;
        var weights = await LoadLatestWeightsAsync([goat.Id], cancellationToken);
        weights.TryGetValue(goat.Id, out var kg);
        var vm = MapGoat(goat, weights.ContainsKey(goat.Id) ? kg : null);
        var ctx = await EnsureHerdBreedingContextAsync(cancellationToken);
        HerdBreedingEnricher.Apply(vm, goat, ctx, DateOnly.FromDateTime(DateTime.Today));
        return vm;
    }

    public async Task SetKiddingsBeforePurchaseAsync(SetKiddingsBeforePurchaseViewModel model, CancellationToken cancellationToken = default)
    {
        if (model.KiddingsBeforePurchase < 0)
            throw new InvalidOperationException("Kiddings before purchase cannot be negative.");
        var goat = await _context.Goats.FindAsync([model.GoatId], cancellationToken)
            ?? throw new InvalidOperationException("Goat not found.");
        if (goat.Gender != GoatGender.Female || goat.Status == GoatStatus.Kid)
            throw new InvalidOperationException("Kiddings before purchase applies to adult does only.");
        goat.KiddingsBeforePurchase = model.KiddingsBeforePurchase;
        goat.UpdatedDate = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task RecordBcsAsync(RecordBcsViewModel model, CancellationToken cancellationToken = default)
    {
        if (model.Score is < 1 or > 5)
            throw new InvalidOperationException("Body condition score must be between 1 and 5.");
        var goat = await _context.Goats.FindAsync([model.GoatId], cancellationToken)
            ?? throw new InvalidOperationException("Goat not found.");
        _context.GoatBcsRecords.Add(new GoatBcsRecord
        {
            GoatId = goat.Id,
            Date = model.Date,
            Score = model.Score
        });
        goat.UpdatedDate = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task RecordHeatAsync(RecordHeatViewModel model, CancellationToken cancellationToken = default)
    {
        var goat = await _context.Goats.FindAsync([model.GoatId], cancellationToken)
            ?? throw new InvalidOperationException("Goat not found.");
        _context.GoatHeatEvents.Add(new GoatHeatEvent
        {
            GoatId = goat.Id,
            Date = model.Date,
            Note = string.IsNullOrWhiteSpace(model.Note) ? null : model.Note.Trim()
        });
        goat.UpdatedDate = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<GoatViewModel?> GetByTagAsync(string tag, CancellationToken cancellationToken = default)
    {
        var goat = await FindGoatByTagAsync(tag, cancellationToken);
        return goat is null ? null : MapGoat(goat);
    }

    private async Task<Goat?> FindGoatByTagAsync(string tag, CancellationToken cancellationToken)
    {
        var normalized = NormalizeTag(tag);
        if (string.IsNullOrEmpty(normalized))
            return null;

        var goat = await MatchTagAsync(normalized, cancellationToken);
        if (goat is not null)
            return goat;

        if (long.TryParse(normalized, out _))
        {
            var trimmed = normalized.TrimStart('0');
            if (!string.IsNullOrEmpty(trimmed) && !string.Equals(trimmed, normalized, StringComparison.Ordinal))
            {
                goat = await MatchTagAsync(trimmed, cancellationToken);
                if (goat is not null)
                    return goat;
            }

            foreach (var candidate in new[] { normalized.PadLeft(3, '0'), normalized.PadLeft(4, '0') })
            {
                if (string.Equals(candidate, normalized, StringComparison.Ordinal))
                    continue;

                goat = await MatchTagAsync(candidate, cancellationToken);
                if (goat is not null)
                    return goat;
            }
        }

        return null;
    }

    private Task<Goat?> MatchTagAsync(string tag, CancellationToken cancellationToken) =>
        LiveGoatsQuery().Include(g => g.Group).Include(g => g.Sire).Include(g => g.Dam).AsNoTracking()
            .FirstOrDefaultAsync(g => g.Tag.ToLower() == tag.ToLower(), cancellationToken);

    public IQueryable<Goat> LiveGoatsQuery() =>
        _context.Goats.Where(g => !g.IsArchived);

    private static string NormalizeTag(string tag)
    {
        if (string.IsNullOrWhiteSpace(tag))
            return string.Empty;

        return new string(tag.Where(c => !char.IsControl(c)).ToArray()).Trim();
    }

    public async Task<GoatViewModel> CreateAsync(CreateGoatViewModel model, CancellationToken cancellationToken = default)
    {
        var goat = new Goat();
        await ApplyAsync(goat, model, cancellationToken);
        _context.Goats.Add(goat);
        await _context.SaveChangesAsync(cancellationToken);
        return MapGoat(goat);
    }

    public async Task<GoatViewModel?> UpdateAsync(int id, CreateGoatViewModel model, CancellationToken cancellationToken = default)
    {
        var goat = await _context.Goats.FindAsync([id], cancellationToken);
        if (goat is null) return null;

        await ApplyAsync(goat, model, cancellationToken);
        goat.UpdatedDate = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);
        return MapGoat(goat);
    }

    private async Task ApplyAsync(Goat goat, CreateGoatViewModel model, CancellationToken cancellationToken)
    {
        var tag = model.Tag.Trim();
        if (string.IsNullOrEmpty(tag))
            throw new InvalidOperationException("Please enter a Tag / RFID ID");
        var lowerTag = tag.ToLower();
        if (await _context.Goats.AnyAsync(g => g.Id != goat.Id && g.Tag.ToLower() == lowerTag, cancellationToken))
            throw new InvalidOperationException($"Tag {tag} is already used by another goat (including archived goats).");
        if (!Enum.IsDefined(model.Status) || !Enum.IsDefined(model.HealthState) || !Enum.IsDefined(model.Gender))
            throw new InvalidOperationException("Invalid status, health or gender.");

        var (sireId, sireText) = await ResolveParentAsync(goat, model.SireTag, GoatGender.Male, "Sire", cancellationToken);
        var (damId, damText) = await ResolveParentAsync(goat, model.DamTag, GoatGender.Female, "Dam", cancellationToken);

        if (goat.IsTemporaryTag && !string.Equals(goat.Tag, tag, StringComparison.Ordinal))
            goat.IsTemporaryTag = false;
        goat.Tag = tag;
        goat.Name = string.IsNullOrWhiteSpace(model.Name) ? null : model.Name.Trim();
        goat.Comment = string.IsNullOrWhiteSpace(model.Comment) ? null : model.Comment.Trim();
        goat.Breed = model.Breed;
        goat.Gender = model.Gender;
        if (PregnancyRules.IsConfirmedPregnant(goat))
        {
            if (model.Status is not GoatStatus.Pregnant and not GoatStatus.Meat and not GoatStatus.Sale)
                throw new InvalidOperationException("She is confirmed pregnant — use Breeding to record birth, abortion, or clear a mistake before changing herd status.");
            goat.Status = GoatStatus.Pregnant;
        }
        else if (model.Status == GoatStatus.Pregnant)
        {
            if (goat.Gender != GoatGender.Female)
                throw new InvalidOperationException("Only does can be marked pregnant.");
            if (!goat.MatedDate.HasValue)
                throw new InvalidOperationException("Record her cross in Breeding first — pregnant status requires a cross date.");
            throw new InvalidOperationException("Record a positive scan in Breeding first — she is not confirmed pregnant yet.");
        }
        else
            goat.Status = model.Status;
        goat.Source = model.Source;
        goat.PurchasePrice = model.Source == GoatSource.Born ? 0 : model.PurchasePrice;
        goat.EventDate = model.EventDate;
        (goat.DateOfBirth, goat.DateOfBirthIsEstimated) = ValidateDateOfBirth(model);
        goat.Owner = string.IsNullOrWhiteSpace(model.Owner) ? null : model.Owner.Trim();
        goat.HealthState = model.HealthState;
        goat.SireId = sireId;
        goat.SireTagText = sireText;
        goat.DamId = damId;
        goat.DamTagText = damText;
    }

    public const int MaxGoatAgeYears = 25;

    private static (DateOnly? Date, bool Estimated) ValidateDateOfBirth(CreateGoatViewModel model, DateOnly? today = null)
    {
        if (model.Source == GoatSource.Born || model.DateOfBirth is not { } dob) return (null, false);
        var now = today ?? DateOnly.FromDateTime(DateTime.Today);
        if (dob > now)
            throw new InvalidOperationException("Date of birth cannot be in the future.");
        if (dob > model.EventDate)
            throw new InvalidOperationException("Date of birth cannot be after the purchase date.");
        if (dob < now.AddYears(-MaxGoatAgeYears))
            throw new InvalidOperationException($"Date of birth is more than {MaxGoatAgeYears} years ago. Check the year.");
        return (dob, model.DateOfBirthIsEstimated);
    }

    private async Task<(int? Id, string? Text)> ResolveParentAsync(
        Goat child, string? parentTag, GoatGender expected, string role, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(parentTag)) return (null, null);
        var tag = parentTag.Trim();
        var lower = tag.ToLower();
        var parent = await _context.Goats.AsNoTracking()
            .Where(g => g.Tag.ToLower() == lower)
            .Select(g => new { g.Id, g.Tag, g.Gender, g.SireId, g.DamId })
            .FirstOrDefaultAsync(cancellationToken);
        if (parent is null) return (null, tag);

        if (child.Id != 0 && parent.Id == child.Id)
            throw new InvalidOperationException($"A goat cannot be its own {role.ToLower()}.");
        if (child.Id != 0 && (parent.SireId == child.Id || parent.DamId == child.Id))
            throw new InvalidOperationException($"{parent.Tag} is this goat's offspring and cannot be its {role.ToLower()}.");
        if (parent.Gender != expected)
            throw new InvalidOperationException($"{role} {parent.Tag} is not {(expected == GoatGender.Male ? "male" : "female")}.");

        if (child.Id != 0)
        {
            var links = await _context.Goats.AsNoTracking()
                .Select(g => new { g.Id, g.SireId, g.DamId })
                .ToDictionaryAsync(x => x.Id, x => (x.SireId, x.DamId), cancellationToken);
            if (PedigreeHelper.IsAncestorOf(parent.Id, child.Id, links))
                throw new InvalidOperationException(
                    $"{parent.Tag} is an ancestor of this goat and cannot be set as {role.ToLower()}.");
        }

        return (parent.Id, null);
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var goat = await _context.Goats.FindAsync([id], cancellationToken);
        if (goat is null) return false;

        var hasHistory =
            await _context.Goats.AnyAsync(g => g.SireId == id || g.DamId == id, cancellationToken) ||
            await _context.BreedingEvents.AnyAsync(e => e.DoeId == id || e.BuckId == id, cancellationToken) ||
            await _context.KiddingRecords.AnyAsync(k => k.DoeId == id, cancellationToken) ||
            await _context.VaccinationHistories.AnyAsync(v => v.GoatId == id, cancellationToken) ||
            await _context.GoatWeightRecords.AnyAsync(w => w.GoatId == id, cancellationToken) ||
            await _context.GoatTreatments.AnyAsync(t => t.GoatId == id, cancellationToken);
        if (hasHistory)
            throw new InvalidOperationException(
                $"{goat.Tag} has herd history (breeding, health, weights, or offspring) and can't be deleted. Record a death or sale instead so the history is kept.");

        _context.Goats.Remove(goat);
        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<ParentOptionsViewModel> GetParentOptionsAsync(CancellationToken cancellationToken = default)
    {
        var goats = await _context.Goats.AsNoTracking()
            .OrderBy(g => g.Tag)
            .Select(g => new { g.Tag, g.Gender })
            .ToListAsync(cancellationToken);
        return new ParentOptionsViewModel
        {
            Sires = goats.Where(g => g.Gender == GoatGender.Male).Select(g => g.Tag).ToList(),
            Dams = goats.Where(g => g.Gender == GoatGender.Female).Select(g => g.Tag).ToList()
        };
    }

    public async Task BulkMoveAsync(BulkMoveViewModel model, CancellationToken cancellationToken = default)
    {
        var goats = await _context.Goats.Where(g => model.GoatIds.Contains(g.Id)).ToListAsync(cancellationToken);
        var target = model.MoveTarget;
        var today = DateOnly.FromDateTime(DateTime.Today);

        if (target.Equals("br:prep", StringComparison.OrdinalIgnoreCase) ||
            target.Equals("br:cross", StringComparison.OrdinalIgnoreCase))
        {
            await BulkBreedingAsync(goats, model, today, cancellationToken);
            return;
        }

        if (target.Equals("k:wean", StringComparison.OrdinalIgnoreCase))
        {
            var weanDate = model.WeanDate ?? today;
            foreach (var goat in goats)
            {
                if (goat.Status != GoatStatus.Kid)
                    throw new InvalidOperationException($"{goat.Tag} is not a kid.");
                goat.WeanDate = weanDate;
                goat.UpdatedDate = DateTime.UtcNow;
            }
            await _context.SaveChangesAsync(cancellationToken);
            return;
        }

        if (target is "k:class-doeling" or "k:class-buckling" or "k:class-meat")
        {
            var cls = target switch
            {
                "k:class-doeling" => YoungGoatClass.Doeling,
                "k:class-buckling" => YoungGoatClass.Buckling,
                _ => YoungGoatClass.Meat
            };
            foreach (var goat in goats)
            {
                if (goat.Status != GoatStatus.Kid)
                    throw new InvalidOperationException($"{goat.Tag} is not a kid.");
                goat.YoungClass = cls;
                goat.YoungClassDate = today;
                if (cls == YoungGoatClass.Meat)
                    goat.Status = GoatStatus.Meat;
                goat.UpdatedDate = DateTime.UtcNow;
            }
            await _context.SaveChangesAsync(cancellationToken);
            return;
        }

        if (target.Equals("k:graduate", StringComparison.OrdinalIgnoreCase))
        {
            foreach (var goat in goats)
            {
                if (goat.YoungClass == YoungGoatClass.Doeling)
                    goat.Status = GoatStatus.Dry;
                else if (goat.YoungClass == YoungGoatClass.Buckling)
                    goat.Status = GoatStatus.Buck;
                else if (goat.YoungClass == YoungGoatClass.Meat)
                    goat.Status = GoatStatus.Meat;
                else
                    throw new InvalidOperationException($"{goat.Tag} has no young-stock class to graduate.");
                goat.YoungClass = YoungGoatClass.None;
                goat.YoungClassDate = null;
                goat.UpdatedDate = DateTime.UtcNow;
            }
            await _context.SaveChangesAsync(cancellationToken);
            return;
        }

        if (target.Equals("br:dryoff", StringComparison.OrdinalIgnoreCase))
        {
            foreach (var goat in goats)
            {
                goat.Status = GoatStatus.Dry;
                goat.UpdatedDate = DateTime.UtcNow;
            }
            await _context.SaveChangesAsync(cancellationToken);
            return;
        }

        if (target.Equals("br:plan", StringComparison.OrdinalIgnoreCase))
        {
            foreach (var goat in goats)
            {
                goat.NextCrossDate = model.NextCrossDate;
                goat.UpdatedDate = DateTime.UtcNow;
            }
            await _context.SaveChangesAsync(cancellationToken);
            return;
        }

        foreach (var goat in goats)
        {
            if (target.Equals("br:kidded", StringComparison.OrdinalIgnoreCase))
            {
                var open = await BreedingCycleWriter.OpenEventsAsync(_context, goat.Id, cancellationToken);
                BreedingCycleWriter.CloseCycle(goat, open, BreedingOutcome.Kidded, today, "marked kidded from Herd");
                goat.Status = GoatStatus.Milking;
            }
            else if (target.StartsWith("st:", StringComparison.OrdinalIgnoreCase))
            {
                var statusKey = target[3..];
                goat.Status = DisplayHelper.ParseStatusKey(statusKey);
            }
            else if (target.StartsWith("grp:", StringComparison.OrdinalIgnoreCase))
            {
                var groupName = target[4..];
                var group = await _context.GoatGroups.FirstOrDefaultAsync(g => g.Name == groupName, cancellationToken);
                if (group is null)
                {
                    group = new GoatGroup { Name = groupName };
                    _context.GoatGroups.Add(group);
                    await _context.SaveChangesAsync(cancellationToken);
                }
                goat.GroupId = group.Id;
            }
            else if (target.StartsWith("bat:", StringComparison.OrdinalIgnoreCase))
            {
                var batch = target[4..];
                goat.BirthBatchName = batch.Equals("none", StringComparison.OrdinalIgnoreCase) ? null : batch;
            }
            else if (target.StartsWith("hl:", StringComparison.OrdinalIgnoreCase))
            {
                var key = target[3..];
                goat.HealthState = key.Equals("sick", StringComparison.OrdinalIgnoreCase)
                    ? GoatHealthState.Sick
                    : Enum.Parse<GoatHealthState>(key, true);
            }
            else if (target.StartsWith("own:", StringComparison.OrdinalIgnoreCase))
                goat.Owner = target[4..];
            else if (target.StartsWith("fg:", StringComparison.OrdinalIgnoreCase))
            {
                if (!int.TryParse(target[3..], out var fgId))
                    throw new InvalidOperationException("Invalid feeding group.");
                goat.CustomFeedingGroupId = fgId;
            }
            else if (!string.IsNullOrWhiteSpace(model.Owner))
                goat.Owner = model.Owner.Trim();
            else if (model.HealthState.HasValue)
                goat.HealthState = model.HealthState.Value;
            else if (model.BirthBatchName is not null)
                goat.BirthBatchName = string.IsNullOrWhiteSpace(model.BirthBatchName) ? null : model.BirthBatchName.Trim();
            else if (model.CustomFeedingGroupId is > 0)
                goat.CustomFeedingGroupId = model.CustomFeedingGroupId;

            goat.UpdatedDate = DateTime.UtcNow;
        }
        await _context.SaveChangesAsync(cancellationToken);
    }

    /// <summary>All-or-nothing: if any selected doe fails the shared breeding rules, nothing is saved.</summary>
    private async Task BulkBreedingAsync(List<Goat> goats, BulkMoveViewModel model, DateOnly today, CancellationToken cancellationToken)
    {
        var isCross = model.MoveTarget.Equals("br:cross", StringComparison.OrdinalIgnoreCase);
        var date = isCross ? model.MatedDate : model.PrepCrossDate;
        if (!date.HasValue)
        {
            if (isCross)
                throw new InvalidOperationException("Pick the cross date.");
            date = today;
        }

        if (isCross && string.IsNullOrWhiteSpace(model.BuckTag))
            throw new InvalidOperationException("Enter the buck tag.");

        var errors = new List<string>();
        var warnings = new List<string>();

        if (!isCross)
        {
            foreach (var goat in goats)
            {
                var error = BreedingRules.EligibilityError(goat, today);
                if (error is not null) errors.Add(error);
            }
            ThrowIfErrors(errors);
            foreach (var goat in goats)
            {
                goat.PrepCrossDate = date;
                goat.BreedingStage = BreedingStage.Ready;
                goat.UpdatedDate = DateTime.UtcNow;
            }
            await _context.SaveChangesAsync(cancellationToken);
            return;
        }

        var buck = await BreedingCycleWriter.FindBuckAsync(_context, model.BuckTag, cancellationToken);
        var pedigree = await BreedingCycleWriter.LoadPedigreeAsync(_context, cancellationToken);
        var plans = goats.Select(g => (Goat: g, Plan: BreedingCycleWriter.PlanCross(g, buck, model.BuckTag, date.Value, today, pedigree))).ToList();
        errors.AddRange(plans.SelectMany(p => p.Plan.Check.Errors));
        warnings.AddRange(plans.SelectMany(p => p.Plan.Check.Warnings));
        ThrowIfErrors(errors);
        if (warnings.Count > 0 && !model.ConfirmWarnings)
            throw new ConfirmationRequiredException(warnings);

        foreach (var (goat, plan) in plans)
        {
            var open = await BreedingCycleWriter.OpenEventsAsync(_context, goat.Id, cancellationToken);
            BreedingCycleWriter.ApplyCross(_context, goat, date.Value, plan, open);
        }
        await _context.SaveChangesAsync(cancellationToken);
    }

    private static void ThrowIfErrors(List<string> errors)
    {
        if (errors.Count == 0) return;
        var shown = errors.Take(5).ToList();
        if (errors.Count > 5) shown.Add($"…and {errors.Count - 5} more.");
        throw new InvalidOperationException("Nothing was changed. " + string.Join(" ", shown));
    }

    public async Task<IReadOnlyList<string>> GetGroupsAsync(CancellationToken cancellationToken = default) =>
        await _context.GoatGroups.OrderBy(g => g.Name).Select(g => g.Name).ToListAsync(cancellationToken);

    public async Task<string> CreateGroupAsync(string name, CancellationToken cancellationToken = default)
    {
        var trimmed = name.Trim();
        if (!await _context.GoatGroups.AnyAsync(g => g.Name == trimmed, cancellationToken))
        {
            _context.GoatGroups.Add(new GoatGroup { Name = trimmed });
            await _context.SaveChangesAsync(cancellationToken);
        }
        return trimmed;
    }

    public int CountByStatus(GoatStatus status, IReadOnlyList<GoatViewModel>? goats = null)
    {
        if (goats is not null) return goats.Count(g => g.Status == status);
        return GetLiveStatusCounts().GetValueOrDefault(status);
    }

    /// <summary>
    /// Live (not archived) goats per status in one grouped query, reused for the rest of this request.
    /// Feed and report calculations ask for these counts over a hundred times per page.
    /// </summary>
    public IReadOnlyDictionary<GoatStatus, int> GetLiveStatusCounts() =>
        _liveStatusCounts ??= LiveGoatsQuery()
            .GroupBy(g => g.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToDictionary(x => x.Status, x => x.Count);

    public async Task<WeightDeathResultViewModel> RecordWeightAsync(
        RecordWeightViewModel model, CancellationToken cancellationToken = default)
    {
        var goat = await FindGoatByTagAsync(model.Tag, cancellationToken);
        if (goat is null)
            return new WeightDeathResultViewModel { Success = false, Message = "No goat found with that tag." };

        _context.GoatWeightRecords.Add(new GoatWeightRecord
        {
            GoatId = goat.Id,
            Date = model.Date,
            Kg = model.Kg
        });
        await _context.SaveChangesAsync(cancellationToken);
        return new WeightDeathResultViewModel
        {
            Success = true,
            Tag = goat.Tag,
            Message = $"Weight saved for {goat.Tag}."
        };
    }

    public async Task<WeightDeathResultViewModel> RecordDeathAsync(
        RecordDeathViewModel model, CancellationToken cancellationToken = default)
    {
        var goat = await _context.Goats.FirstOrDefaultAsync(g => g.Tag.ToLower() == model.Tag.Trim().ToLower() && !g.IsArchived, cancellationToken);
        if (goat is null)
            return new WeightDeathResultViewModel { Success = false, Message = "No live goat found with that tag." };

        var ageDays = BreedingRules.AgeDays(goat, model.Date) is { } a ? Math.Max(0, a) : (int?)null;
        _context.DeathRecords.Add(new DeathRecord
        {
            GoatId = goat.Id,
            Tag = goat.Tag,
            Breed = goat.Breed,
            Date = model.Date,
            Reason = string.IsNullOrWhiteSpace(model.Reason) ? null : model.Reason.Trim(),
            AgeDays = ageDays,
            ValueLost = goat.PurchasePrice
        });

        goat.IsArchived = true;
        goat.ArchivedReason = "died";
        goat.ArchivedDate = model.Date;
        goat.UpdatedDate = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);

        return new WeightDeathResultViewModel
        {
            Success = true,
            Tag = goat.Tag,
            Message = $"Recorded: {goat.Tag} died on {model.Date:yyyy-MM-dd}."
        };
    }

    public async Task<GoatSaleFormDataViewModel> GetSaleFormDataAsync(CancellationToken cancellationToken = default)
    {
        var accounts = await _context.MoneyAccounts.AsNoTracking()
            .Where(a => a.IsActive)
            .OrderBy(a => a.Name)
            .Select(a => new SaleMoneyAccountOptionViewModel { Id = a.Id, Name = a.Name })
            .ToListAsync(cancellationToken);

        var customers = await _context.BusinessCustomers.AsNoTracking()
            .Where(c => c.IsActive)
            .OrderBy(c => c.Name)
            .Select(c => new SaleCustomerOptionViewModel { Id = c.Id, Name = c.Name })
            .ToListAsync(cancellationToken);

        return new GoatSaleFormDataViewModel { Accounts = accounts, Customers = customers };
    }

    public async Task<WeightDeathResultViewModel> RecordSaleAsync(
        RecordSaleViewModel model, CancellationToken cancellationToken = default)
    {
        if (model.Price <= 0)
            return new WeightDeathResultViewModel { Success = false, Message = "Enter the sale price." };

        var goat = await _context.Goats.FirstOrDefaultAsync(
            g => g.Tag.ToLower() == model.Tag.Trim().ToLower() && !g.IsArchived, cancellationToken);
        if (goat is null)
            return new WeightDeathResultViewModel { Success = false, Message = "No live goat found with that tag." };

        if (model.WeightKg is > 0)
        {
            _context.GoatWeightRecords.Add(new GoatWeightRecord
            {
                GoatId = goat.Id,
                Date = model.Date,
                Kg = model.WeightKg.Value
            });
        }

        var buyer = string.IsNullOrWhiteSpace(model.Buyer) ? null : model.Buyer.Trim();
        var incomeNote = goat.Tag + (buyer is not null ? " — " + buyer : "");

        var income = new Income
        {
            Type = "Live goat sale",
            Amount = model.Price,
            Date = model.Date,
            Comment = incomeNote
        };
        _context.Incomes.Add(income);
        await _context.SaveChangesAsync(cancellationToken);

        int? invoiceId = null;
        int? ledgerAccountId = null;
        if (model.IsCredit)
        {
            var customerId = model.CustomerId;
            if (customerId is null or <= 0)
            {
                if (string.IsNullOrEmpty(buyer))
                    return new WeightDeathResultViewModel { Success = false, Message = "Enter a buyer name or pick a customer for credit sales." };

                var existing = await _context.BusinessCustomers
                    .FirstOrDefaultAsync(c => c.Name.ToLower() == buyer.ToLower(), cancellationToken);
                if (existing is null)
                {
                    existing = new BusinessCustomer
                    {
                        Name = buyer,
                        IsActive = true,
                        CreatedDate = DateTime.UtcNow,
                        UpdatedDate = DateTime.UtcNow
                    };
                    _context.BusinessCustomers.Add(existing);
                    await _context.SaveChangesAsync(cancellationToken);
                }

                customerId = existing.Id;
            }

            var inv = new CustomerInvoice
            {
                CustomerId = customerId.Value,
                Date = model.Date,
                DueDate = model.Date.AddDays(14),
                Description = $"Live goat sale · {goat.Tag}",
                Amount = model.Price,
                PaidAmount = 0,
                Status = InvoiceStatus.Open,
                CreatedDate = DateTime.UtcNow,
                UpdatedDate = DateTime.UtcNow
            };
            _context.CustomerInvoices.Add(inv);
            await _context.SaveChangesAsync(cancellationToken);
            invoiceId = inv.Id;
        }
        else
        {
            var accountId = model.AccountId;
            if (accountId is null or <= 0)
                return new WeightDeathResultViewModel { Success = false, Message = "Choose the Cash/Bank account that received the money." };

            ledgerAccountId = accountId;
            _context.LedgerTransactions.Add(new LedgerTransaction
            {
                Date = model.Date,
                Kind = LedgerTransactionKind.Income,
                AccountId = ledgerAccountId,
                Amount = model.Price,
                Category = income.Type,
                Reference = goat.Tag,
                Notes = "Live goat sale" + (buyer is not null ? " · " + buyer : ""),
                CreatedDate = DateTime.UtcNow,
                UpdatedDate = DateTime.UtcNow
            });
            await _context.SaveChangesAsync(cancellationToken);
        }

        var sale = new GoatSale
        {
            GoatId = goat.Id,
            Tag = goat.Tag,
            Breed = goat.Breed,
            Date = model.Date,
            Price = model.Price,
            Buyer = buyer,
            WeightKg = model.WeightKg,
            IsCredit = model.IsCredit,
            AccountId = model.IsCredit ? null : ledgerAccountId ?? model.AccountId,
            CustomerInvoiceId = invoiceId,
            IncomeId = income.Id
        };
        _context.GoatSales.Add(sale);

        goat.IsArchived = true;
        goat.ArchivedReason = "sold";
        goat.ArchivedDate = model.Date;
        goat.UpdatedDate = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);

        return new WeightDeathResultViewModel
        {
            Success = true,
            Tag = goat.Tag,
            Message = $"Sold {goat.Tag} for Rs {model.Price:N0} on {model.Date:yyyy-MM-dd}."
        };
    }

    private async Task<IReadOnlyList<HerdTreatmentRowViewModel>> LoadUnderTreatmentAsync(CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var rows = await _context.GoatTreatments.AsNoTracking()
            .Include(t => t.Goat)
            .Where(t => (t.EndDate == null || t.EndDate >= today) && !t.Goat.IsArchived)
            .OrderByDescending(t => t.StartDate)
            .ToListAsync(cancellationToken);

        return rows.Select(t =>
        {
            var days = GetAgeInDays(t.StartDate);
            var wd = t.WithdrawalDays;
            int? left = wd is > 0 ? Math.Max(0, wd.Value - days) : null;
            var (statusText, _) = DisplayHelper.GetStatusDisplay(t.Goat.Status);
            return new HerdTreatmentRowViewModel
            {
                TreatmentId = t.Id,
                GoatId = t.GoatId,
                Tag = t.Goat.Tag,
                StatusDisplay = statusText,
                HealthDisplay = DisplayHelper.GetHealthLabel(t.Goat.HealthState),
                Diagnosis = t.Diagnosis,
                Medicine = t.Medicine,
                DaysInTreatment = days,
                WithdrawalDays = wd,
                WithdrawalDaysLeft = left
            };
        }).ToList();
    }

    public int GetAgeInDays(DateOnly eventDate)
    {
        var days = (DateOnly.FromDateTime(DateTime.Today).ToDateTime(TimeOnly.MinValue) -
                    eventDate.ToDateTime(TimeOnly.MinValue)).Days;
        return Math.Max(0, days);
    }

    public string GetAgeLabel(int days)
    {
        if (days < 60) return $"{days} days";
        var months = (int)Math.Floor(days / 30.4);
        if (months < 12) return $"{months} mo";
        var years = days / 365;
        var rem = (int)Math.Floor((days % 365) / 30.4);
        return rem > 0 ? $"{years}y {rem}m" : $"{years}y";
    }

    private static HerdStatsViewModel BuildStats(IReadOnlyDictionary<GoatStatus, int> counts, int confirmedPregnant) => new()
    {
        Total = counts.Values.Sum(),
        Kids = counts.GetValueOrDefault(GoatStatus.Kid),
        Milking = counts.GetValueOrDefault(GoatStatus.Milking),
        Pregnant = confirmedPregnant,
        Dry = counts.GetValueOrDefault(GoatStatus.Dry),
        Bucks = counts.GetValueOrDefault(GoatStatus.Buck),
        Meat = counts.GetValueOrDefault(GoatStatus.Meat)
    };

    private async Task<IReadOnlyDictionary<int, decimal>> LoadLatestWeightsAsync(
        IReadOnlyList<int> goatIds, CancellationToken cancellationToken)
    {
        if (goatIds.Count == 0)
            return new Dictionary<int, decimal>();

        var rows = await _context.GoatWeightRecords.AsNoTracking()
            .Where(w => goatIds.Contains(w.GoatId))
            .Select(w => new { w.GoatId, w.Date, w.Kg })
            .ToListAsync(cancellationToken);

        return rows.GroupBy(w => w.GoatId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.Date).First().Kg);
    }

    private IReadOnlyList<GoatViewModel> MapGoatsWithWeights(
        IReadOnlyList<Goat> goats,
        IReadOnlyDictionary<int, decimal> latestWeights,
        HerdBreedingContext? breedingCtx = null)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        return goats.Select(g =>
        {
            latestWeights.TryGetValue(g.Id, out var kg);
            var vm = MapGoat(g, latestWeights.ContainsKey(g.Id) ? kg : null, today);
            if (breedingCtx is not null)
                HerdBreedingEnricher.Apply(vm, g, breedingCtx, today);
            return vm;
        }).ToList();
    }

    private GoatViewModel MapGoat(Goat g, decimal? latestWeightKg = null, DateOnly? today = null)
    {
        var now = today ?? DateOnly.FromDateTime(DateTime.Today);
        var birth = BreedingRules.BirthDate(g);
        int? ageDays = birth is { } b ? GetAgeInDays(b) : null;
        var estimated = BreedingRules.AgeIsEstimated(g);
        var (text, css) = DisplayHelper.GetStatusDisplay(g.Status);
        var next = HerdNextStep.Compute(g, latestWeightKg, now);
        var stage = HerdNextStep.KidStageLine(g, latestWeightKg, now);
        return new GoatViewModel
        {
            Id = g.Id,
            Tag = g.Tag,
            Name = g.Name,
            Comment = g.Comment,
            Breed = g.Breed,
            Gender = g.Gender,
            Status = g.Status,
            Source = g.Source,
            PurchasePrice = g.PurchasePrice,
            EventDate = g.EventDate,
            GroupId = g.GroupId,
            GroupName = g.Group?.Name,
            DateOfBirth = g.Source == GoatSource.Born ? null : g.DateOfBirth,
            DateOfBirthIsEstimated = g.Source != GoatSource.Born && g.DateOfBirthIsEstimated,
            AgeDays = ageDays,
            AgeIsEstimated = estimated,
            AgeLabel = ageDays is { } days ? (estimated ? "about " : "") + GetAgeLabel(days) : "age unknown",
            StatusDisplay = text,
            StatusCssClass = css,
            PriceDisplay = g.Source == GoatSource.Born ? "home-bred" : DisplayHelper.FormatRs(g.PurchasePrice),
            PrepCrossDate = g.PrepCrossDate,
            MatedDate = g.MatedDate,
            BuckTag = g.BuckTag,
            KidsCount = g.KidsCount,
            UltrasoundDate = g.UltrasoundDate,
            BreedingHint = BreedingHint(g),
            BreedingHintColor = g.MatedDate.HasValue
                ? g.KidsCount > 0 ? "var(--pink)" : "var(--blue)"
                : g.PrepCrossDate.HasValue ? "var(--amber)" : null,
            Owner = g.Owner,
            HealthState = g.HealthState,
            HealthDisplay = DisplayHelper.GetHealthLabel(g.HealthState),
            BreedingStage = g.BreedingStage,
            BreedingStageDisplay = DisplayHelper.GetBreedingStageLabel(g.BreedingStage),
            SireTag = g.Sire?.Tag ?? g.SireTagText,
            DamTag = g.Dam?.Tag ?? g.DamTagText,
            IsTemporaryTag = g.IsTemporaryTag,
            WeanDate = g.WeanDate,
            YoungClass = g.YoungClass,
            YoungClassDisplay = DisplayHelper.GetYoungClassLabel(g.YoungClass),
            BirthBatchName = g.BirthBatchName,
            NextCrossDate = g.NextCrossDate,
            FeedingGroupName = g.CustomFeedingGroup?.Name,
            KidStageLine = stage?.Text,
            KidStageColor = stage?.Color,
            KidStageWeanLink = stage?.WeanLink ?? false,
            NextStepAction = next?.Action.ToString(),
            NextStepLabel = next?.ButtonLabel,
            NextStepPermTab = next?.PermTab,
            NextStepPermAction = next?.PermAction,
            NextStepKidsCount = next?.ScanKidsCount,
            IsArchived = g.IsArchived,
            ArchivedReason = g.ArchivedReason,
            ArchivedDateDisplay = g.ArchivedDate?.ToString("yyyy-MM-dd"),
            KiddingsBeforePurchase = g.KiddingsBeforePurchase
        };
    }

    private static string? BreedingHint(Goat g)
    {
        if (g.MatedDate.HasValue)
            return g.KidsCount > 0
                ? $"due ~{BreedingHelper.ExpectedKidding(g.MatedDate.Value):yyyy-MM-dd}"
                : $"crossed {g.MatedDate:yyyy-MM-dd} — scan pending";
        return g.PrepCrossDate.HasValue ? "ready for cross" : null;
    }

    private async Task<IQueryable<Goat>> ApplyFilterAsync(IQueryable<Goat> goats, string? filter, CancellationToken cancellationToken)
    {
        filter ??= "all";
        var now = DateOnly.FromDateTime(DateTime.Today);
        if (filter == "all") return goats;

        if (filter.StartsWith("age:", StringComparison.Ordinal))
        {
            var parts = filter.Split(':');
            if (parts.Length >= 3 && int.TryParse(parts[1], out var lo) && int.TryParse(parts[2], out var hi))
            {
                var bornOnOrBefore = now.AddDays(-Math.Clamp(lo, -36500, 36500));
                var bornAfter = now.AddDays(-Math.Clamp(hi, -36500, 36500));
                var zeroInRange = lo <= 0 && hi > 0;
                return goats.Where(g =>
                    (g.Source == GoatSource.Born &&
                     ((g.EventDate <= now && g.EventDate <= bornOnOrBefore && g.EventDate > bornAfter) || (zeroInRange && g.EventDate > now))) ||
                    (g.Source != GoatSource.Born && g.DateOfBirth != null &&
                     ((g.DateOfBirth <= now && g.DateOfBirth <= bornOnOrBefore && g.DateOfBirth > bornAfter) || (zeroInRange && g.DateOfBirth > now))));
            }
        }

        if (filter.StartsWith("st:", StringComparison.Ordinal))
        {
            var key = filter[3..];
            if (string.Equals(key, "pregnant", StringComparison.OrdinalIgnoreCase))
                return goats.Where(PregnancyRules.ConfirmedPregnantExpression);
            var status = DisplayHelper.ParseStatusKey(key);
            return goats.Where(g => g.Status == status);
        }

        if (filter.Equals("untag", StringComparison.Ordinal))
            return goats.Where(g => g.IsTemporaryTag);

        if (filter.Equals("wean", StringComparison.Ordinal))
        {
            var weanByDate = now.AddDays(-HerdRules.WeanDaysDefault);
            var minKg = HerdRules.WeanKgDefault;
            return goats.Where(g => g.Status == GoatStatus.Kid && g.WeanDate == null &&
                (
                    (g.Source == GoatSource.Born && g.EventDate <= weanByDate) ||
                    (g.Source != GoatSource.Born && g.DateOfBirth != null && g.DateOfBirth <= weanByDate) ||
                    _context.GoatWeightRecords.Where(w => w.GoatId == g.Id).OrderByDescending(w => w.Date).Select(w => w.Kg).FirstOrDefault() >= minKg
                ));
        }

        if (filter.Equals("ageup", StringComparison.Ordinal))
        {
            var cutoff = now.AddDays(-HerdRules.AgeUpDays);
            return goats.Where(g => g.Status == GoatStatus.Kid &&
                ((g.Source == GoatSource.Born && g.EventDate <= cutoff) ||
                 (g.Source != GoatSource.Born && g.DateOfBirth != null && g.DateOfBirth <= cutoff)));
        }

        if (filter.Equals("br:prep", StringComparison.Ordinal))
            return goats.Where(g => g.PrepCrossDate.HasValue && !g.MatedDate.HasValue);
        if (filter.Equals("br:waiting", StringComparison.Ordinal))
            return goats.Where(g => g.MatedDate.HasValue && !g.KidsCount.HasValue);
        if (filter.Equals("br:scan", StringComparison.Ordinal))
        {
            var scanFrom = now.AddDays(-BreedingHelper.ScanAfterDays);
            return goats.Where(g => g.MatedDate.HasValue && !g.KidsCount.HasValue && g.MatedDate <= scanFrom);
        }
        if (filter.Equals("br:expecting", StringComparison.Ordinal))
            return goats.Where(g => g.MatedDate.HasValue && g.KidsCount > 0);
        if (filter.Equals("br:due30", StringComparison.Ordinal))
        {
            var matedOnOrBefore = now.AddDays(30 - BreedingHelper.GestationDays);
            return goats.Where(g => g.MatedDate.HasValue && g.KidsCount > 0 && g.MatedDate <= matedOnOrBefore);
        }

        if (filter.Equals("dryoff", StringComparison.Ordinal))
        {
            return goats.Where(g => g.Status == GoatStatus.Milking && g.MatedDate.HasValue && g.KidsCount > 0 &&
                g.MatedDate.Value.AddDays(BreedingHelper.GestationDays - HerdRules.DryOffBeforeKiddingDays) <= now &&
                g.MatedDate.Value.AddDays(BreedingHelper.GestationDays) > now);
        }

        if (filter.Equals("crossdue", StringComparison.Ordinal))
        {
            var weekOut = now.AddDays(7);
            return goats.Where(g => g.NextCrossDate.HasValue && !g.MatedDate.HasValue &&
                g.NextCrossDate.Value <= weekOut);
        }

        if (filter.Equals("buildup", StringComparison.Ordinal))
        {
            var buildStart = now.AddDays(HerdRules.BuildUpDays);
            return goats.Where(g => g.NextCrossDate.HasValue && !g.MatedDate.HasValue &&
                g.NextCrossDate.Value.AddDays(-HerdRules.BuildUpDays) <= now &&
                g.NextCrossDate.Value > now);
        }

        if (filter.Equals("stopmilk", StringComparison.Ordinal))
        {
            var stopFrom = now.AddDays(HerdRules.DryBeforeCrossDays);
            return goats.Where(g => g.Status == GoatStatus.Milking && g.NextCrossDate.HasValue && !g.MatedDate.HasValue &&
                g.NextCrossDate.Value.AddDays(-HerdRules.DryBeforeCrossDays) <= now);
        }

        if (filter.Equals("heatdue", StringComparison.Ordinal))
        {
            var lastHeatFrom = now.AddDays(-(HerdRules.HeatCycleDays * 2));
            var lastHeatTo = now.AddDays(-(HerdRules.HeatCycleDays - 2));
            var latestHeats = _context.GoatHeatEvents
                .GroupBy(h => h.GoatId)
                .Select(h => new { GoatId = h.Key, Date = h.Max(x => x.Date) });
            return from g in goats
                   join h in latestHeats on g.Id equals h.GoatId
                   where g.Gender == GoatGender.Female
                         && g.Status != GoatStatus.Kid
                         && g.MatedDate == null
                         && h.Date >= lastHeatFrom
                         && h.Date <= lastHeatTo
                   select g;
        }

        if (filter.Equals("hl:sick", StringComparison.Ordinal))
            return goats.Where(g => g.HealthState != GoatHealthState.Healthy);

        if (filter.StartsWith("hl:", StringComparison.Ordinal) &&
            Enum.TryParse<GoatHealthState>(filter[3..], true, out var hl))
            return goats.Where(g => g.HealthState == hl);

        if (filter.StartsWith("health:", StringComparison.Ordinal) &&
            Enum.TryParse<GoatHealthState>(filter[7..], true, out var health))
            return goats.Where(g => g.HealthState == health);

        if (filter.StartsWith("bat:", StringComparison.Ordinal))
        {
            var batch = filter[4..];
            return batch.Equals("none", StringComparison.OrdinalIgnoreCase)
                ? goats.Where(g => g.BirthBatchName == null || g.BirthBatchName == "")
                : goats.Where(g => g.BirthBatchName == batch);
        }

        ResolveOwnerSireDamGrp(filter, out var ownerKey, out var sireKey, out var damKey, out var groupKey, out var handled);
        if (handled)
        {
            if (ownerKey is not null)
            {
                var o = ownerKey.ToLower();
                return goats.Where(g => g.Owner != null && g.Owner.ToLower() == o);
            }
            if (sireKey is not null)
            {
                var s = sireKey.ToLower();
                return goats.Where(g => (g.Sire != null && g.Sire.Tag.ToLower() == s) ||
                                        (g.SireTagText != null && g.SireTagText.ToLower() == s));
            }
            if (damKey is not null)
            {
                var d = damKey.ToLower();
                return goats.Where(g => (g.Dam != null && g.Dam.Tag.ToLower() == d) ||
                                        (g.DamTagText != null && g.DamTagText.ToLower() == d));
            }
            if (groupKey is not null)
                return goats.Where(g => g.Group != null && g.Group.Name == groupKey);
        }

        if (filter.StartsWith("kd:", StringComparison.Ordinal))
        {
            var suffix = filter[3..];
            var fivePlus = suffix is "5" or "5p";
            int? exactKiddings = null;
            if (!fivePlus)
            {
                if (!int.TryParse(suffix, out var n))
                    return goats.Where(_ => false);
                exactKiddings = n;
            }

            if (fivePlus)
            {
                return goats.Where(g => g.Gender == GoatGender.Female && g.Status != GoatStatus.Kid &&
                    g.KiddingsBeforePurchase + _context.KiddingRecords.Count(k => k.DoeId == g.Id) >= 5);
            }

            var target = exactKiddings!.Value;
            return goats.Where(g => g.Gender == GoatGender.Female && g.Status != GoatStatus.Kid &&
                g.KiddingsBeforePurchase + _context.KiddingRecords.Count(k => k.DoeId == g.Id) == target);
        }

        if (filter.Equals("ms:low", StringComparison.Ordinal) || filter.Equals("ms:4", StringComparison.Ordinal))
        {
            var ctx = await EnsureHerdBreedingContextAsync(cancellationToken);
            var ids = filter.Equals("ms:4", StringComparison.Ordinal)
                ? ctx.HighMotherScoreDoeIds
                : ctx.LowMotherScoreDoeIds;
            return goats.Where(g => ids.Contains(g.Id));
        }

        if (filter.Equals("dec:cull", StringComparison.Ordinal))
        {
            var ctx = await EnsureHerdBreedingContextAsync(cancellationToken);
            return goats.Where(g => ctx.CullDecisionDoeIds.Contains(g.Id));
        }

        if (filter.StartsWith("mg:", StringComparison.Ordinal))
        {
            var mgId = filter[3..];
            return goats.Where(g => g.MotherGroupId == mgId);
        }

        if (filter.Equals("kid:milk", StringComparison.Ordinal))
            return goats.Where(g => g.Status == GoatStatus.Kid && g.WeanDate == null);

        if (filter.Equals("kid:weaned", StringComparison.Ordinal))
            return goats.Where(g => g.Status == GoatStatus.Kid && g.WeanDate != null);

        if (filter.StartsWith("milk:", StringComparison.Ordinal) &&
            Enum.TryParse<KidMilkType>(filter[5..], true, out var milkType))
        {
            return goats.Where(g => g.Status == GoatStatus.Kid && g.WeanDate == null && g.KidMilkType == milkType);
        }

        await Task.CompletedTask;
        return goats;
    }

    private async Task<HerdBreedingContext> EnsureHerdBreedingContextAsync(CancellationToken cancellationToken)
    {
        if (_herdBreedingCtx is not null)
            return _herdBreedingCtx;

        var kiddings = await _context.KiddingRecords.AsNoTracking().ToListAsync(cancellationToken);
        var events = await _context.BreedingEvents.AsNoTracking().ToListAsync(cancellationToken);
        var liveKids = await LiveGoatsQuery()
            .Where(g => g.Status == GoatStatus.Kid && g.DamId != null)
            .Include(g => g.Dam)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
        var motherGroups = await LoadMotherGroupsAsync(cancellationToken);
        _herdBreedingCtx = HerdBreedingEnricher.Build(kiddings, events, liveKids, motherGroups);
        return _herdBreedingCtx;
    }

    private async Task<IReadOnlyList<MotherGroupDto>> LoadMotherGroupsAsync(CancellationToken cancellationToken)
    {
        var row = await _context.AppSettings.AsNoTracking()
            .FirstOrDefaultAsync(s => s.Key == AppSettingKeys.MotherGroups, cancellationToken);
        if (row?.Value is null)
            return MotherGroupHelper.Defaults.ToList();

        return JsonSerializer.Deserialize<List<MotherGroupDto>>(row.Value) ?? MotherGroupHelper.Defaults.ToList();
    }

    private static void ResolveOwnerSireDamGrp(
        string filter,
        out string? ownerKey,
        out string? sireKey,
        out string? damKey,
        out string? groupKey,
        out bool handled)
    {
        ownerKey = sireKey = damKey = groupKey = null;
        handled = false;
        if (filter.StartsWith("owner:", StringComparison.Ordinal) || filter.StartsWith("own:", StringComparison.Ordinal))
        {
            handled = true;
            ownerKey = filter.StartsWith("owner:") ? filter[6..] : filter[4..];
            ownerKey = ownerKey.Trim();
            return;
        }
        if (filter.StartsWith("sire:", StringComparison.Ordinal))
        {
            handled = true;
            sireKey = filter[5..].Trim();
            return;
        }
        if (filter.StartsWith("dam:", StringComparison.Ordinal))
        {
            handled = true;
            damKey = filter[4..].Trim();
            return;
        }
        if (filter.StartsWith("grp:", StringComparison.Ordinal))
        {
            handled = true;
            groupKey = filter[4..];
        }
    }

    private async Task<IReadOnlyList<string>> GetBirthBatchesAsync(CancellationToken cancellationToken)
    {
        var fromGoats = await _context.Goats.Where(g => !g.IsArchived && g.BirthBatchName != null && g.BirthBatchName != "")
            .Select(g => g.BirthBatchName!)
            .ToListAsync(cancellationToken);
        var fromSeason = new List<string>();
        var row = await _context.AppSettings.AsNoTracking()
            .FirstOrDefaultAsync(s => s.Key == AppSettingKeys.BreedingSeason, cancellationToken);
        if (row is not null)
        {
            var season = JsonSerializer.Deserialize<BreedingSeasonSettings>(row.Value);
            if (season?.Batches is { Count: > 0 })
                fromSeason.AddRange(season.Batches.Select(b => b.Name));
        }

        return fromGoats.Concat(fromSeason)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private async Task<IReadOnlyList<string>> GetSireFilterTagsAsync(CancellationToken cancellationToken)
    {
        var fromGoats = await _context.Goats.Where(g => !g.IsArchived && g.Status == GoatStatus.Buck)
            .Select(g => g.Tag).ToListAsync(cancellationToken);
        var fromKids = await _context.Goats.Where(g => !g.IsArchived && g.SireTagText != null)
            .Select(g => g.SireTagText!).ToListAsync(cancellationToken);
        var fromFk = await _context.Goats.Where(g => !g.IsArchived && g.Sire != null)
            .Select(g => g.Sire!.Tag).ToListAsync(cancellationToken);
        return fromGoats.Concat(fromKids).Concat(fromFk).Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(t => t, StringComparer.OrdinalIgnoreCase).ToList();
    }

    private async Task<IReadOnlyList<string>> GetOwnerFiltersAsync(CancellationToken cancellationToken) =>
        await _context.Goats.Where(g => !g.IsArchived && g.Owner != null && g.Owner != "")
            .Select(g => g.Owner!)
            .Distinct()
            .OrderBy(o => o)
            .ToListAsync(cancellationToken);

    private async Task<IReadOnlyList<(int Id, string Name)>> GetFeedingGroupOptionsAsync(CancellationToken cancellationToken) =>
        await _context.CustomFeedingGroups.AsNoTracking()
            .OrderBy(f => f.Name)
            .Select(f => new ValueTuple<int, string>(f.Id, f.Name))
            .ToListAsync(cancellationToken);

    private async Task<HerdFilterOptionsViewModel> BuildFilterOptionsAsync(
        string currentFilter,
        IReadOnlyDictionary<GoatStatus, int> statusCounts,
        IReadOnlyList<string> batches,
        IReadOnlyList<string> sireTags,
        IReadOnlyList<string> owners,
        IReadOnlyList<MotherGroupDto> motherGroups,
        CancellationToken cancellationToken)
    {
        var total = statusCounts.Values.Sum();
        var sireLabels = await LoadSireLabelsAsync(sireTags, cancellationToken);
        var chipOwners = owners.Where(o => !o.Equals("Farm", StringComparison.OrdinalIgnoreCase)).ToList();
        var showNoGroup = await LiveGoatsQuery().AnyAsync(g =>
            g.Gender == GoatGender.Female &&
            g.Status != GoatStatus.Kid &&
            (g.BirthBatchName == null || g.BirthBatchName == ""), cancellationToken);

        async Task<HerdFilterChipViewModel> Chip(string value, string label, string color)
        {
            int count;
            if (value == "all")
                count = total;
            else if (value.StartsWith("st:", StringComparison.Ordinal))
                count = statusCounts.GetValueOrDefault(DisplayHelper.ParseStatusKey(value[3..]));
            else
                count = await (await ApplyFilterAsync(LiveGoatsQuery(), value, cancellationToken)).CountAsync(cancellationToken);

            return new HerdFilterChipViewModel
            {
                Value = value,
                Label = label,
                Color = color,
                Count = count,
                Active = string.Equals(value, currentFilter, StringComparison.OrdinalIgnoreCase)
            };
        }

        var rows = new List<HerdFilterRowViewModel>();

        rows.Add(new HerdFilterRowViewModel
        {
            Label = "Status",
            Chips =
            [
                await Chip("all", "All", "var(--green)"),
                await Chip("st:kid", "Kids", "var(--amber)"),
                await Chip("st:milking", "Milking", "var(--green)"),
                await Chip("st:pregnant", "Pregnant", "var(--pink)"),
                await Chip("st:dry", "Dry", "var(--ink-soft)"),
                await Chip("st:buck", "Bucks", "var(--blue)"),
                await Chip("st:sale", "For sale", "#9a3a1e"),
            ]
        });

        rows.Add(new HerdFilterRowViewModel
        {
            Label = "Breeding",
            Chips =
            [
                await Chip("br:prep", "Ready for cross", "var(--amber)"),
                await Chip("br:waiting", "Crossed - awaiting scan", "var(--blue)"),
                await Chip("br:scan", "Scan due now", "var(--amber)"),
                await Chip("br:expecting", "Confirmed pregnant", "var(--pink)"),
                await Chip("br:due30", "Due in 30 days", "var(--red)"),
            ]
        });

        var groupChips = new List<HerdFilterChipViewModel>();
        foreach (var batch in batches)
            groupChips.Add(await Chip("bat:" + batch, batch, "var(--pink)"));
        if (showNoGroup)
            groupChips.Add(await Chip("bat:none", "No group set", "var(--amber)"));
        if (groupChips.Count > 0)
            rows.Add(new HerdFilterRowViewModel { Label = "Group", Chips = groupChips });

        var sireChips = new List<HerdFilterChipViewModel>();
        foreach (var tag in sireTags)
            sireChips.Add(await Chip("sire:" + tag, sireLabels.GetValueOrDefault(tag, tag), "var(--blue)"));
        if (sireChips.Count > 0)
            rows.Add(new HerdFilterRowViewModel { Label = "Sire", Chips = sireChips });

        var checkChips = new List<HerdFilterChipViewModel>
        {
            await Chip("untag", "Kids to tag", "var(--amber)"),
            await Chip("wean", "Ready to wean", "var(--amber)"),
            await Chip("ageup", "Kids over 1 year", "var(--red)"),
            await Chip("heatdue", "Heat expected", "var(--blue)"),
            await Chip("dryoff", "To dry off", "var(--red)"),
            await Chip("buildup", "On extra feed", "var(--amber)"),
            await Chip("stopmilk", "Stop milking", "var(--red)"),
            await Chip("crossdue", "Cross due", "var(--pink)"),
            await Chip("hl:sick", "Sick / treatment", "var(--red)"),
        };
        foreach (var owner in chipOwners)
            checkChips.Add(await Chip("own:" + owner, owner, "var(--blue)"));
        rows.Add(new HerdFilterRowViewModel { Label = "Check", Chips = checkChips });

        var kdChips = new List<HerdFilterChipViewModel>
        {
            await Chip("kd:0", "First-timers (0)", "var(--green)"),
            await Chip("kd:1", "1 kidding", "var(--ink-soft)"),
            await Chip("kd:2", "2 kiddings", "var(--ink-soft)"),
            await Chip("kd:3", "3 kiddings", "var(--ink-soft)"),
            await Chip("kd:4", "4 kiddings", "var(--ink-soft)"),
            await Chip("kd:5", "5+ kiddings", "var(--ink-soft)"),
            await Chip("ms:4", "Mother score ≥ 4", "var(--amber)"),
            await Chip("ms:low", "Mother score < 3", "var(--red)"),
            await Chip("dec:cull", "Decision = Cull", "var(--red)"),
        };
        rows.Add(new HerdFilterRowViewModel { Label = "Kiddings", Chips = kdChips });

        var mgChips = new List<HerdFilterChipViewModel>();
        foreach (var mg in motherGroups)
            mgChips.Add(await Chip("mg:" + mg.Id, mg.Name, mg.Color));
        if (mgChips.Count > 0)
            rows.Add(new HerdFilterRowViewModel { Label = "Mother group", Chips = mgChips });

        var kidMilkChips = new List<HerdFilterChipViewModel>
        {
            await Chip("kid:milk", "Kids on milk", "var(--amber)"),
            await Chip("kid:weaned", "Kids weaned", "var(--green)"),
            await Chip("milk:Mother", "On mother milk", "var(--pink)"),
            await Chip("milk:Powder", "On powder milk", "var(--blue)"),
            await Chip("milk:Mixed", "On mixed milk", "var(--ink-soft)"),
            await Chip("milk:Foster", "On foster milk", "var(--ink-soft)"),
        };
        rows.Add(new HerdFilterRowViewModel { Label = "Kid milk", Chips = kidMilkChips });

        return new HerdFilterOptionsViewModel
        {
            BirthBatches = batches,
            SireTags = sireTags,
            Owners = owners,
            Rows = rows,
            Chips = rows.SelectMany(r => r.Chips).ToList()
        };
    }

    private async Task<Dictionary<string, string>> LoadSireLabelsAsync(
        IReadOnlyList<string> sireTags, CancellationToken cancellationToken)
    {
        var labels = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (sireTags.Count == 0) return labels;

        var goats = await _context.Goats.AsNoTracking()
            .Where(g => sireTags.Contains(g.Tag))
            .Select(g => new { g.Tag, g.Name })
            .ToListAsync(cancellationToken);

        foreach (var tag in sireTags)
            labels[tag] = tag;
        foreach (var g in goats)
            labels[g.Tag] = string.IsNullOrWhiteSpace(g.Name) ? g.Tag : $"{g.Tag} · {g.Name}";
        return labels;
    }

    public async Task<HerdCsvImportResultViewModel> ImportHerdCsvAsync(
        string csvText, string defaultBreed, CancellationToken cancellationToken = default)
    {
        var preview = HerdCsvImporter.Parse(csvText, defaultBreed);
        if (preview.Rows.Count == 0)
            return new HerdCsvImportResultViewModel { Errors = preview.Errors };

        var existingTags = await _context.Goats.Select(g => g.Tag.ToLower()).ToListAsync(cancellationToken);
        var existing = existingTags.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var imported = 0;
        var skipped = new List<string>();
        var errors = preview.Errors.ToList();

        foreach (var row in preview.Rows)
        {
            if (existing.Contains(row.Tag))
            {
                skipped.Add(row.Tag);
                errors.Add($"Tag \"{row.Tag}\" already exists in the herd.");
                continue;
            }

            var model = new CreateGoatViewModel
            {
                Tag = row.Tag,
                Name = row.Name,
                Comment = row.Comment,
                Breed = row.Breed,
                Gender = row.Gender,
                Status = row.Status,
                Source = row.Source,
                PurchasePrice = row.PurchasePrice,
                EventDate = row.EventDate,
                Owner = row.Owner,
                SireTag = row.SireTag,
                DamTag = row.DamTag
            };

            try
            {
                await CreateAsync(model, cancellationToken);
                existing.Add(row.Tag);
                imported++;
            }
            catch (InvalidOperationException ex)
            {
                errors.Add($"{row.Tag}: {ex.Message}");
            }
        }

        // Link dams created in the same import pass
        foreach (var row in preview.Rows.Where(r => !string.IsNullOrWhiteSpace(r.DamTag)))
        {
            var kid = await _context.Goats.FirstOrDefaultAsync(g => g.Tag == row.Tag, cancellationToken);
            if (kid is null || kid.DamId is not null) continue;
            var dam = await FindGoatByTagAsync(row.DamTag!, cancellationToken);
            if (dam is not null && dam.Id != kid.Id)
            {
                kid.DamId = dam.Id;
                kid.DamTagText = null;
                await _context.SaveChangesAsync(cancellationToken);
            }
        }

        return new HerdCsvImportResultViewModel
        {
            Imported = imported,
            Errors = errors,
            SkippedTags = skipped
        };
    }

    /// <summary>Case-insensitive contains over tag, name, owner, breed and parent tags.</summary>
    private static IQueryable<Goat> ApplySearch(IQueryable<Goat> goats, string? search)
    {
        if (string.IsNullOrWhiteSpace(search)) return goats;
        var term = search.Trim().ToLower();
        return goats.Where(g =>
            g.Tag.ToLower().Contains(term) ||
            (g.Name != null && g.Name.ToLower().Contains(term)) ||
            (g.Owner != null && g.Owner.ToLower().Contains(term)) ||
            g.Breed.ToLower().Contains(term) ||
            (g.Sire != null && g.Sire.Tag.ToLower().Contains(term)) ||
            (g.Dam != null && g.Dam.Tag.ToLower().Contains(term)) ||
            (g.SireTagText != null && g.SireTagText.ToLower().Contains(term)) ||
            (g.DamTagText != null && g.DamTagText.ToLower().Contains(term)));
    }
}
