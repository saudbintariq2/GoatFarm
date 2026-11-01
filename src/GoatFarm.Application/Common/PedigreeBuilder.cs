using GoatFarm.Domain.Entities;

namespace GoatFarm.Application.Common;

public sealed class PedigreeNodeViewModel
{
    public int? Id { get; set; }
    public string Tag { get; set; } = string.Empty;
    public string? SireTag { get; set; }
    public string? DamTag { get; set; }
    public int Generation { get; set; }
}

public static class PedigreeBuilder
{
    public static IReadOnlyList<PedigreeNodeViewModel> BuildAncestors(
        Goat goat,
        IReadOnlyDictionary<int, Goat> byId,
        int maxGenerations = 4)
    {
        var list = new List<PedigreeNodeViewModel>();
        void Walk(Goat? node, int gen)
        {
            if (node is null || gen > maxGenerations)
                return;
            list.Add(new PedigreeNodeViewModel
            {
                Id = node.Id,
                Tag = node.Tag,
                SireTag = node.Sire?.Tag ?? node.SireTagText,
                DamTag = node.Dam?.Tag ?? node.DamTagText,
                Generation = gen
            });
            if (gen >= maxGenerations) return;
            Goat? sire = node.SireId is { } sid && byId.TryGetValue(sid, out var s) ? s : null;
            Goat? dam = node.DamId is { } did && byId.TryGetValue(did, out var d) ? d : null;
            Walk(sire, gen + 1);
            Walk(dam, gen + 1);
        }

        Walk(goat, 0);
        return list;
    }
}
