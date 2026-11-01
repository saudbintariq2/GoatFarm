using GoatFarm.Domain.Entities;
using GoatFarm.Domain.Enums;

namespace GoatFarm.Application.Common;

public sealed class GoatCardPedigreeNode
{
    public int Id { get; init; }
    public string Tag { get; init; } = string.Empty;
    public GoatGender Gender { get; init; }
    public string? Name { get; init; }
    public string Breed { get; init; } = string.Empty;
    public int? SireId { get; init; }
    public int? DamId { get; init; }
    public bool IsArchived { get; init; }
}

public sealed class GoatCardSibling
{
    public int Id { get; init; }
    public string Tag { get; init; } = string.Empty;
    public GoatGender Gender { get; init; }
    public DateOnly BirthDate { get; init; }
    public bool Twin { get; init; }
}

public static class GoatCardBuilder
{
    public const int MaxPedigreeGenerations = 5;

    public static IReadOnlyList<GoatCardPedigreeNode> CollectPedigreeNodes(
        Goat subject,
        IReadOnlyDictionary<int, Goat> byId,
        int maxGenerations = MaxPedigreeGenerations)
    {
        maxGenerations = Math.Clamp(maxGenerations, 2, MaxPedigreeGenerations);
        var seen = new HashSet<int>();
        var list = new List<GoatCardPedigreeNode>();

        void Walk(Goat? g, int depth)
        {
            if (g is null || depth >= maxGenerations || !seen.Add(g.Id))
                return;

            list.Add(new GoatCardPedigreeNode
            {
                Id = g.Id,
                Tag = g.Tag,
                Gender = g.Gender,
                Name = g.Name,
                Breed = g.Breed,
                SireId = g.SireId,
                DamId = g.DamId,
                IsArchived = g.IsArchived
            });

            Goat? sire = g.SireId is int sid && byId.TryGetValue(sid, out var s) ? s : null;
            Goat? dam = g.DamId is int did && byId.TryGetValue(did, out var d) ? d : null;
            Walk(sire, depth + 1);
            Walk(dam, depth + 1);
        }

        Walk(subject, 0);
        return list;
    }

    public static IReadOnlyList<GoatCardSibling> Siblings(
        Goat subject,
        IEnumerable<Goat> allGoats)
    {
        if (subject.DamId is not int damId)
            return [];

        var subjectBirth = BreedingRules.BirthDate(subject);
        return allGoats
            .Where(g => !g.IsArchived && g.Id != subject.Id && g.DamId == damId)
            .Select(g => new GoatCardSibling
            {
                Id = g.Id,
                Tag = g.Tag,
                Gender = g.Gender,
                BirthDate = BreedingRules.BirthDate(g) ?? g.EventDate,
                Twin = subjectBirth.HasValue && BreedingRules.BirthDate(g) == subjectBirth
            })
            .OrderBy(s => s.BirthDate)
            .ThenBy(s => s.Tag, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
