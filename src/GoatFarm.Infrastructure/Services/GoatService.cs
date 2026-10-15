using GoatFarm.Application.Common;
using GoatFarm.Application.Interfaces;
using GoatFarm.Application.ViewModels.Goats;
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

    public GoatService(GoatFarmDbContext context)
    {
        _context = context;
        // Any save in this request may change a status or archive a goat.
        _context.SavedChanges += (_, _) => _liveStatusCounts = null;
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

        var query = ApplySearch(ApplyFilter(LiveGoatsQuery(), filter), search);
        var totalItems = await query.CountAsync(cancellationToken);
        var totalPages = Math.Max(1, (int)Math.Ceiling(totalItems / (double)pageSize));
        page = Math.Clamp(page, 1, totalPages);

        var goats = await query
            .OrderByDescending(g => g.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Include(g => g.Group).Include(g => g.Sire).Include(g => g.Dam)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var statusCounts = await LiveGoatsQuery()
            .GroupBy(g => g.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Status, x => x.Count, cancellationToken);

        return new HerdPageViewModel
        {
            Stats = BuildStats(statusCounts),
            Goats = goats.Select(MapGoat).ToList(),
            Groups = await GetGroupsAsync(cancellationToken),
            Filter = filter ?? "all",
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
        return (parent.Id, null);
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var goat = await _context.Goats.FindAsync([id], cancellationToken);
        if (goat is null) return false;

        var hasHistory =
            await _context.Goats.AnyAsync(g => g.SireId == id || g.DamId == id, cancellationToken) ||
            await _context.BreedingEvents.AnyAsync(e => e.DoeId == id || e.BuckId == id, cancellationToken) ||
            await _context.KiddingRecords.AnyAsync(k => k.DoeId == id, cancellationToken);
        if (hasHistory)
            throw new InvalidOperationException(
                $"{goat.Tag} has breeding or offspring history and can't be deleted. Record a death or sale instead so the history is kept.");

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

        foreach (var goat in goats)
        {
            if (target.Equals("br:kidded", StringComparison.OrdinalIgnoreCase))
            {
                var open = await BreedingCycleWriter.OpenEventsAsync(_context, goat.Id, cancellationToken);
                BreedingCycleWriter.CloseCycle(goat, open, BreedingOutcome.Kidded, today, "marked kidded from Herd");
                goat.Status = GoatStatus.Milking;
            }
            else if (model.MoveTarget.StartsWith("st:", StringComparison.OrdinalIgnoreCase))
            {
                var statusKey = model.MoveTarget[3..];
                goat.Status = DisplayHelper.ParseStatusKey(statusKey);
            }
            else if (model.MoveTarget.StartsWith("grp:", StringComparison.OrdinalIgnoreCase))
            {
                var groupName = model.MoveTarget[4..];
                var group = await _context.GoatGroups.FirstOrDefaultAsync(g => g.Name == groupName, cancellationToken);
                if (group is null)
                {
                    group = new GoatGroup { Name = groupName };
                    _context.GoatGroups.Add(group);
                    await _context.SaveChangesAsync(cancellationToken);
                }
                goat.GroupId = group.Id;
            }
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
            throw new InvalidOperationException(isCross ? "Pick the cross date." : "Pick the planned cross date.");

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

    private static HerdStatsViewModel BuildStats(IReadOnlyDictionary<GoatStatus, int> counts) => new()
    {
        Total = counts.Values.Sum(),
        Kids = counts.GetValueOrDefault(GoatStatus.Kid),
        Milking = counts.GetValueOrDefault(GoatStatus.Milking),
        Pregnant = counts.GetValueOrDefault(GoatStatus.Pregnant),
        Dry = counts.GetValueOrDefault(GoatStatus.Dry),
        Bucks = counts.GetValueOrDefault(GoatStatus.Buck),
        Meat = counts.GetValueOrDefault(GoatStatus.Meat)
    };

    private GoatViewModel MapGoat(Goat g)
    {
        var birth = BreedingRules.BirthDate(g);
        int? ageDays = birth is { } b ? GetAgeInDays(b) : null;
        var estimated = BreedingRules.AgeIsEstimated(g);
        var (text, css) = DisplayHelper.GetStatusDisplay(g.Status);
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
            IsTemporaryTag = g.IsTemporaryTag
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

    /// <summary>Same semantics as the former in-memory filter, expressed so the provider can translate it to SQL.</summary>
    private static IQueryable<Goat> ApplyFilter(IQueryable<Goat> goats, string? filter, DateOnly? today = null)
    {
        filter ??= "all";
        var now = today ?? DateOnly.FromDateTime(DateTime.Today);
        if (filter == "all") return goats;
        if (filter.StartsWith("age:", StringComparison.Ordinal))
        {
            var parts = filter.Split(':');
            if (parts.Length >= 3 && int.TryParse(parts[1], out var lo) && int.TryParse(parts[2], out var hi))
            {
                // Age is max(0, today - birth date) days; a future date counts as age 0. The birth date is EventDate
                // for home-born goats and the optional DateOfBirth for bought goats; bought goats without one have no age.
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
            var status = DisplayHelper.ParseStatusKey(filter[3..]);
            return goats.Where(g => g.Status == status);
        }
        if (filter.Equals("br:prep", StringComparison.Ordinal))
            return goats.Where(g => g.PrepCrossDate.HasValue && !g.MatedDate.HasValue);
        if (filter.Equals("br:waiting", StringComparison.Ordinal))
            return goats.Where(g => g.MatedDate.HasValue && !g.KidsCount.HasValue);
        if (filter.Equals("br:expecting", StringComparison.Ordinal))
            return goats.Where(g => g.MatedDate.HasValue && g.KidsCount > 0);
        if (filter.Equals("br:due30", StringComparison.Ordinal))
        {
            var matedOnOrBefore = now.AddDays(30 - BreedingHelper.GestationDays);
            return goats.Where(g => g.MatedDate.HasValue && g.KidsCount > 0 && g.MatedDate <= matedOnOrBefore);
        }
        if (filter.StartsWith("health:", StringComparison.Ordinal) &&
            Enum.TryParse<GoatHealthState>(filter[7..], true, out var health))
            return goats.Where(g => g.HealthState == health);
        if (filter.StartsWith("owner:", StringComparison.Ordinal))
        {
            var owner = filter[6..].Trim().ToLower();
            return goats.Where(g => g.Owner != null && g.Owner.ToLower() == owner);
        }
        if (filter.StartsWith("sire:", StringComparison.Ordinal))
        {
            var sire = filter[5..].Trim().ToLower();
            return goats.Where(g => (g.Sire != null && g.Sire.Tag.ToLower() == sire) ||
                                    (g.SireTagText != null && g.SireTagText.ToLower() == sire));
        }
        if (filter.StartsWith("dam:", StringComparison.Ordinal))
        {
            var dam = filter[4..].Trim().ToLower();
            return goats.Where(g => (g.Dam != null && g.Dam.Tag.ToLower() == dam) ||
                                    (g.DamTagText != null && g.DamTagText.ToLower() == dam));
        }
        if (filter.StartsWith("grp:", StringComparison.Ordinal))
        {
            var group = filter[4..];
            return goats.Where(g => g.Group != null && g.Group.Name == group);
        }
        return goats;
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
