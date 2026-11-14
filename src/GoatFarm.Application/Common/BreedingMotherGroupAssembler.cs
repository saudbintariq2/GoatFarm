using GoatFarm.Application.ViewModels.Breeding;
using GoatFarm.Domain.Entities;
using GoatFarm.Domain.Enums;

namespace GoatFarm.Application.Common;

public static class BreedingMotherGroupAssembler
{
    public static IReadOnlyList<BreedingMotherGroupRowViewModel> BuildRows(
        IReadOnlyList<Goat> does,
        IReadOnlyDictionary<int, int> kiddingCounts,
        IReadOnlyDictionary<int, KiddingRecord> latestKiddingByDoe,
        IReadOnlyDictionary<int, KiddingRecord[]> allKiddingsByDoe)
    {
        return does
            .Where(d => d.Gender == GoatGender.Female && !d.IsArchived && kiddingCounts.GetValueOrDefault(d.Id) > 0)
            .Select(d =>
            {
                latestKiddingByDoe.TryGetValue(d.Id, out var latest);
                var born = latest is null ? (int?)null : latest.LiveKids + latest.Stillborn;
                allKiddingsByDoe.TryGetValue(d.Id, out var all);
                var avg = all is null ? null : MotherScoreHelper.DoeAverage(all);
                return new BreedingMotherGroupRowViewModel
                {
                    DoeId = d.Id,
                    Tag = d.Tag,
                    Breed = d.Breed,
                    KiddingsCount = kiddingCounts.GetValueOrDefault(d.Id),
                    LastLitterBorn = born,
                    LastLitterText = born is null ? "—" : $"{born} kid{(born == 1 ? "" : "s")}",
                    MotherScoreHtml = avg.HasValue ? MotherScoreHelper.StarsHtml(avg) : "<span class=\"muted\">—</span>",
                    MotherGroupId = d.MotherGroupId,
                    SuggestedGroupId = string.IsNullOrWhiteSpace(d.MotherGroupId) ? MotherGroupHelper.SuggestGroupId(born) : null
                };
            })
            .OrderByDescending(r => r.LastLitterBorn ?? 0)
            .ThenBy(r => r.Tag, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}