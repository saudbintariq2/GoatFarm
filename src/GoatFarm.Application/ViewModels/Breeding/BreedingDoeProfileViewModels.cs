namespace GoatFarm.Application.ViewModels.Breeding;

public class BreedingDoeProfileViewModel
{
    public int Id { get; set; }
    public string Tag { get; set; } = string.Empty;
    public string? Name { get; set; }
    public string Breed { get; set; } = string.Empty;
    public string AgeText { get; set; } = string.Empty;
    public string? BirthBatchLabel { get; set; }
    public string FlowStageLabel { get; set; } = string.Empty;
    public string FlowStageCssClass { get; set; } = "chip-dry";
    public string AvatarText { get; set; } = string.Empty;
    public int KiddingsHere { get; set; }
    public int KiddingsBeforePurchase { get; set; }
    public int KiddingsTotal { get; set; }
    public int KidsBorn { get; set; }
    public int KidsAlive { get; set; }
    public int TimesPregnant { get; set; }
    public string? DueRangeText { get; set; }
    public string DueStatLabel { get; set; } = "Not crossed now";
    public string? SireTag { get; set; }
    public int? SireId { get; set; }
    public string? SirePedigree { get; set; }
    public string? DamTag { get; set; }
    public int? DamId { get; set; }
    public string? DamPedigree { get; set; }
    public string MainHerdKiddingText { get; set; } = string.Empty;
    public IReadOnlyList<BreedingDoeHistoryItemViewModel> History { get; set; } = [];
}

public class BreedingDoeHistoryItemViewModel
{
    public string Headline { get; set; } = string.Empty;
    public string Detail { get; set; } = string.Empty;
    public string CssClass { get; set; } = "breed-hist-empty";
}
