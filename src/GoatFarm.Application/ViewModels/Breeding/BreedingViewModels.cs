namespace GoatFarm.Application.ViewModels.Breeding;

public class BreedingPrepRowViewModel
{
    public int Id { get; set; }
    public string Tag { get; set; } = string.Empty;
    public string? Name { get; set; }
    public string StatusDisplay { get; set; } = string.Empty;
    public string StatusCssClass { get; set; } = string.Empty;
    public string PrepCrossDate { get; set; } = string.Empty;
    public string DietStartDate { get; set; } = string.Empty;
    public bool DietStartNow { get; set; }
    public string CrossInText { get; set; } = string.Empty;
}

public class BreedingExpectingRowViewModel
{
    public int Id { get; set; }
    public string Tag { get; set; } = string.Empty;
    public string? Name { get; set; }
    public string MatedDate { get; set; } = string.Empty;
    public string? BuckTag { get; set; }
    public int? KidsCount { get; set; }
    public string? UltrasoundDate { get; set; }
    public string KidsDisplay { get; set; } = string.Empty;
    public bool ExtraFeed { get; set; }
    public string ExpectedKidding { get; set; } = string.Empty;
    public string KiddingWindow { get; set; } = string.Empty;
    public string DueText { get; set; } = string.Empty;
    public string DueColor { get; set; } = string.Empty;
}

public class BreedingEmptyRowViewModel
{
    public string Tag { get; set; } = string.Empty;
    public string? Name { get; set; }
    public string? MatedDate { get; set; }
    public string? BuckTag { get; set; }
    public string ScanDate { get; set; } = string.Empty;
    public int TimesEmpty { get; set; }
    public string StatusDisplay { get; set; } = string.Empty;
    public string StatusCssClass { get; set; } = string.Empty;
}

public class BreedingRationRowViewModel
{
    public string Tag { get; set; } = string.Empty;
    public string? Name { get; set; }
    public string Stage { get; set; } = string.Empty;
    public decimal MixKgPerDay { get; set; }
    public decimal FodderKgPerDay { get; set; }
    public decimal DailyCost { get; set; }
    public string Note { get; set; } = string.Empty;
}

public class BreedingPageViewModel
{
    public int PrepCount { get; set; }
    public int ExpectingCount { get; set; }
    public string NextDueText { get; set; } = "next due —";
    public string EmptyRateText { get; set; } = "—";
    public IReadOnlyList<BreedingPrepRowViewModel> PrepRows { get; set; } = [];
    public IReadOnlyList<BreedingExpectingRowViewModel> ExpectingRows { get; set; } = [];
    public IReadOnlyList<BreedingEmptyRowViewModel> EmptyRows { get; set; } = [];
    public IReadOnlyList<BreedingRationRowViewModel> RationRows { get; set; } = [];
}

public class RecordPrepViewModel
{
    public string Tag { get; set; } = string.Empty;
    public DateOnly Date { get; set; }
}

public class RecordCrossViewModel
{
    public string Tag { get; set; } = string.Empty;
    public DateOnly Date { get; set; }
    public string? BuckTag { get; set; }
}

public class RecordUltrasoundViewModel
{
    public string Tag { get; set; } = string.Empty;
    public int KidsCount { get; set; }
    public DateOnly? Date { get; set; }
}
