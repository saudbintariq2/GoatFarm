namespace GoatFarm.Application.Common;

public class BreedingSeasonSettings
{
    public int BuildUp { get; set; } = 60;
    public int ScanAfter { get; set; } = 45;
    public int DryOff { get; set; } = 60;
    public int WeanDays { get; set; } = 90;
    public decimal WeanKg { get; set; } = 13;
    public int DryBeforeCross { get; set; } = 14;
    public List<BreedingSeasonBatch> Batches { get; set; } =
    [
        new() { Id = "b1", Name = "Main group", From = "09-01", To = "09-15" }
    ];
}

public class BreedingSeasonBatch
{
    public string Id { get; set; } = "b1";
    public string Name { get; set; } = "Main group";
    /// <summary>MM-DD birth window start.</summary>
    public string From { get; set; } = "09-01";
    /// <summary>MM-DD birth window end.</summary>
    public string To { get; set; } = "09-15";
}

public sealed class BreedingSeasonPlan
{
    public required BreedingSeasonBatch Batch { get; init; }
    public required DateOnly KidFrom { get; init; }
    public required DateOnly KidTo { get; init; }
    public required DateOnly CrossFrom { get; init; }
    public required DateOnly CrossTo { get; init; }
    public required DateOnly BuildFrom { get; init; }
    public required DateOnly ScanFrom { get; init; }
    public required DateOnly ScanTo { get; init; }
    public required DateOnly DryFrom { get; init; }
    public required DateOnly DryTo { get; init; }
    public required DateOnly WeanFrom { get; init; }
    public required DateOnly WeanTo { get; init; }
}

public class BreedingGroupMemberDto
{
    public int GoatId { get; set; }
    public string Tag { get; set; } = string.Empty;
}

public class BreedingGroupDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Buck { get; set; } = string.Empty;
    public string Start { get; set; } = string.Empty;
    public string End { get; set; } = string.Empty;
    public int Max { get; set; } = 15;
    public string Status { get; set; } = "active";
    public List<BreedingGroupMemberDto> Members { get; set; } = [];
}
