namespace GoatFarm.Domain.Constants;

public static class FeedTypes
{
    public const string Wanda = "wanda";
    public const string Binola = "binola";
    public const string Sarson = "sarson";
    public const string Bran = "bran";
    public const string Maize = "maize";
    public const string Sheera = "sheera";
    public const string Fodder = "fodder";
    public const string FodderDry = "fodderdry";
    public const string MilkPowder = "milkpowder";
    public const string KidStarter = "kidstarter";

    public const string UseRecipe = "recipe";
    public const string UseDirect = "direct";

    public static readonly IReadOnlyList<(string Key, string Name)> All =
    [
        (Wanda, "Wanda (concentrate)"),
        (Binola, "Binola (cottonseed cake)"),
        (Sarson, "Sarson khali (mustard cake)"),
        (Bran, "Wheat bran (choker)"),
        (Maize, "Maize (makai)"),
        (Sheera, "Sheera (molasses)"),
        (Fodder, "Green fodder (chaara)"),
        (FodderDry, "Dry fodder (toori / bhoosa)"),
        (MilkPowder, "Powder milk (kids)")
    ];

    public static bool IsGreenFodder(string feedType) =>
        string.Equals(feedType, Fodder, StringComparison.OrdinalIgnoreCase);

    public static bool IsPurchasable(string feedType) =>
        !IsGreenFodder(feedType);

    public static bool IsMilkPowder(string feedType) =>
        string.Equals(feedType, MilkPowder, StringComparison.OrdinalIgnoreCase);

    public static bool IsMixIngredient(string feedType) =>
        IsPurchasable(feedType) &&
        !string.Equals(feedType, FodderDry, StringComparison.OrdinalIgnoreCase) &&
        !IsMilkPowder(feedType);

    public static bool IsDirectUse(string? useType) =>
        string.Equals(useType, UseDirect, StringComparison.OrdinalIgnoreCase);
}
