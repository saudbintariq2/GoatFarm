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

    public GoatService(GoatFarmDbContext context) => _context = context;

    public async Task<HerdPageViewModel> GetHerdPageAsync(
        string? filter,
        int page = 1,
        int pageSize = DefaultPageSize,
        CancellationToken cancellationToken = default)
    {
        pageSize = pageSize <= 0 ? DefaultPageSize : pageSize;

        var goats = await _context.Goats
            .Include(g => g.Group)
            .AsNoTracking()
            .OrderByDescending(g => g.Id)
            .ToListAsync(cancellationToken);

        var vmGoats = goats.Select(MapGoat).ToList();
        var filtered = ApplyFilter(vmGoats, filter).ToList();
        var totalItems = filtered.Count;
        var totalPages = Math.Max(1, (int)Math.Ceiling(totalItems / (double)pageSize));
        page = Math.Clamp(page, 1, totalPages);

        var paged = filtered
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        return new HerdPageViewModel
        {
            Stats = BuildStats(vmGoats),
            Goats = paged,
            Groups = await GetGroupsAsync(cancellationToken),
            Filter = filter ?? "all",
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
        var goat = await _context.Goats.Include(g => g.Group).AsNoTracking()
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
        _context.Goats.Include(g => g.Group).AsNoTracking()
            .FirstOrDefaultAsync(g => g.Tag.ToLower() == tag.ToLower(), cancellationToken);

    private static string NormalizeTag(string tag)
    {
        if (string.IsNullOrWhiteSpace(tag))
            return string.Empty;

        return new string(tag.Where(c => !char.IsControl(c)).ToArray()).Trim();
    }

    public async Task<GoatViewModel> CreateAsync(CreateGoatViewModel model, CancellationToken cancellationToken = default)
    {
        var goat = new Goat
        {
            Tag = model.Tag.Trim(),
            Name = string.IsNullOrWhiteSpace(model.Name) ? null : model.Name.Trim(),
            Comment = string.IsNullOrWhiteSpace(model.Comment) ? null : model.Comment.Trim(),
            Breed = model.Breed,
            Gender = model.Gender,
            Status = model.Status,
            Source = model.Source,
            PurchasePrice = model.Source == GoatSource.Born ? 0 : model.PurchasePrice,
            EventDate = model.EventDate
        };
        _context.Goats.Add(goat);
        await _context.SaveChangesAsync(cancellationToken);
        return MapGoat(goat);
    }

    public async Task<GoatViewModel?> UpdateAsync(int id, CreateGoatViewModel model, CancellationToken cancellationToken = default)
    {
        var goat = await _context.Goats.FindAsync([id], cancellationToken);
        if (goat is null) return null;

        goat.Tag = model.Tag.Trim();
        goat.Name = string.IsNullOrWhiteSpace(model.Name) ? null : model.Name.Trim();
        goat.Comment = string.IsNullOrWhiteSpace(model.Comment) ? null : model.Comment.Trim();
        goat.Breed = model.Breed;
        goat.Gender = model.Gender;
        goat.Status = model.Status;
        goat.Source = model.Source;
        goat.PurchasePrice = model.Source == GoatSource.Born ? 0 : model.PurchasePrice;
        goat.EventDate = model.EventDate;
        goat.UpdatedDate = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);
        return MapGoat(goat);
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var goat = await _context.Goats.FindAsync([id], cancellationToken);
        if (goat is null) return false;
        _context.Goats.Remove(goat);
        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task BulkMoveAsync(BulkMoveViewModel model, CancellationToken cancellationToken = default)
    {
        var goats = await _context.Goats.Where(g => model.GoatIds.Contains(g.Id)).ToListAsync(cancellationToken);
        foreach (var goat in goats)
        {
            if (model.MoveTarget.StartsWith("st:", StringComparison.OrdinalIgnoreCase))
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
        return _context.Goats.Count(g => g.Status == status);
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

    private static HerdStatsViewModel BuildStats(IReadOnlyList<GoatViewModel> goats) => new()
    {
        Total = goats.Count,
        Kids = goats.Count(g => g.Status == GoatStatus.Kid),
        Milking = goats.Count(g => g.Status == GoatStatus.Milking),
        Pregnant = goats.Count(g => g.Status == GoatStatus.Pregnant),
        Bucks = goats.Count(g => g.Status == GoatStatus.Buck)
    };

    private GoatViewModel MapGoat(Goat g)
    {
        var ageDays = GetAgeInDays(g.EventDate);
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
            AgeDays = ageDays,
            AgeLabel = GetAgeLabel(ageDays),
            StatusDisplay = text,
            StatusCssClass = css,
            PriceDisplay = g.Source == GoatSource.Born ? "home-bred" : DisplayHelper.FormatRs(g.PurchasePrice)
        };
    }

    private static IEnumerable<GoatViewModel> ApplyFilter(IReadOnlyList<GoatViewModel> goats, string? filter)
    {
        filter ??= "all";
        if (filter == "all") return goats;
        if (filter.StartsWith("age:", StringComparison.Ordinal))
        {
            var parts = filter.Split(':');
            if (parts.Length >= 3 && int.TryParse(parts[1], out var lo) && int.TryParse(parts[2], out var hi))
                return goats.Where(g => g.AgeDays >= lo && g.AgeDays < hi);
        }
        if (filter.StartsWith("st:", StringComparison.Ordinal))
        {
            var status = DisplayHelper.ParseStatusKey(filter[3..]);
            return goats.Where(g => g.Status == status);
        }
        if (filter.StartsWith("grp:", StringComparison.Ordinal))
        {
            var group = filter[4..];
            return goats.Where(g => g.GroupName == group);
        }
        return goats;
    }
}
