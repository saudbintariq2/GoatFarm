namespace GoatFarm.Application.ViewModels.Finance;

/// <summary>Controls optional heavy sections when loading the finance page.</summary>
public sealed class FinancePageLoadOptions
{
    public static FinancePageLoadOptions Full { get; } = new();

    public static FinancePageLoadOptions Overview { get; } = new()
    {
        IncludeTabRegisters = false
    };

    public bool IncludeTabRegisters { get; init; } = true;
    public bool IncludeIntegrityAndBalance { get; init; } = true;
}
