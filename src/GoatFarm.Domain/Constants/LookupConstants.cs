namespace GoatFarm.Domain.Constants;

public static class LookupConstants
{
    public static readonly string[] IncomeTypes =
    [
        "Ghee", "Paneer", "Yogurt / Lassi", "Soap", "Live goat sale", "Meat", "Other"
    ];

    public static readonly string[] ExpenseTypes =
    [
        "Salaries", "Cultivation (fodder)", "Vet / extra medicine", "Utilities",
        "Transport", "Repairs", "Land rent", "Other"
    ];

    public static readonly string[] AssetTypes =
    [
        "Machinery", "Land", "Buildings/Sheds", "Other"
    ];

    public static readonly string[] Breeds =
    [
        "Beetal", "Makhee Cheeni"
    ];

    public static readonly string[] MilkBreeds =
    [
        "Mixed", "Beetal", "Makhee Cheeni"
    ];

    public static readonly string[] VaccineNames =
    [
        "Enterotoxaemia (ET)", "PPR", "FMD", "CCPP", "ET booster (pre-kidding)", "Deworming"
    ];

    public static readonly string[] VaccineUnits =
    [
        "Box", "Bottle", "Dose", "Vial", "Other"
    ];
}
