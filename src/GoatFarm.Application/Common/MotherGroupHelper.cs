using GoatFarm.Application.ViewModels.Breeding;

namespace GoatFarm.Application.Common;

public static class MotherGroupHelper
{
    public static readonly IReadOnlyList<MotherGroupDto> Defaults =
    [
        new() { Id = "m4", Name = "4+ kids", Color = "#7B5EA7", Order = 1 },
        new() { Id = "m3", Name = "3 kids", Color = "#4F6D8F", Order = 2 },
        new() { Id = "m2", Name = "2 kids (twins)", Color = "#2E6A50", Order = 3 },
        new() { Id = "m1", Name = "1 kid (single)", Color = "#B8862B", Order = 4 }
    ];

    public static string? SuggestGroupId(int? litterBorn)
    {
        if (litterBorn is null or < 1) return null;
        return litterBorn switch
        {
            >= 4 => "m4",
            3 => "m3",
            2 => "m2",
            _ => "m1"
        };
    }
}