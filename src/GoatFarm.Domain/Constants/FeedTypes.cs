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

    public static readonly IReadOnlyList<(string Key, string Name)> All =
    [
        (Wanda, "Wanda (concentrate)"),
        (Binola, "Binola (cottonseed cake)"),
        (Sarson, "Sarson khali (mustard cake)"),
        (Bran, "Wheat bran (choker)"),
        (Maize, "Maize (makai)"),
        (Sheera, "Sheera (molasses)"),
        (Fodder, "Green fodder (chaara)")
    ];
}
