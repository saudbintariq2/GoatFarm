namespace GoatFarm.Application.Common;

public static class PedigreeHelper
{
    public const int MaxAncestorGenerations = 12;

    /// <summary>Returns true if <paramref name="candidateId"/> is an ancestor of <paramref name="goatId"/> within max generations.</summary>
    public static bool IsAncestorOf(
        int candidateId,
        int goatId,
        IReadOnlyDictionary<int, (int? SireId, int? DamId)> links,
        int maxGenerations = MaxAncestorGenerations)
    {
        var visited = new HashSet<int>();
        var queue = new Queue<(int Id, int Depth)>();
        queue.Enqueue((goatId, 0));
        while (queue.Count > 0)
        {
            var (current, depth) = queue.Dequeue();
            if (depth >= maxGenerations || !links.TryGetValue(current, out var parents))
                continue;
            foreach (var parentId in new[] { parents.SireId, parents.DamId })
            {
                if (parentId is not { } pid || !visited.Add(pid))
                    continue;
                if (pid == candidateId)
                    return true;
                queue.Enqueue((pid, depth + 1));
            }
        }
        return false;
    }
}
