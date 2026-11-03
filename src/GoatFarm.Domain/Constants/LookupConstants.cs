namespace GoatFarm.Domain.Constants;

public static class LookupConstants
{
    public static readonly string[] IncomeTypes =
    [
        "Ghee", "Paneer", "Yogurt / Lassi", "Soap", "Live goat sale", "Meat", "Other"
    ];

    public static readonly string[] ExpenseTypes =
    [
        "Seeding", "Fodder cutting", "Irrigation / water",
        "Cultivation (fodder)", "Vaccines", "Doctor / vet visit", "Vet / extra medicine",
        "Utilities", "Transport", "Repairs", "Land rent", "Other"
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

    public static readonly string[] Owners = ["Farm"];

    public static readonly string[] SaleTypes =
    [
        "Milk", "Dairy products", "Meat", "Live animal / farm sale", "Other farm sale"
    ];

    public static readonly string[] FundingShareholders =
    [
        "Shareholder 1", "Shareholder 2"
    ];

    /// <summary>Labels shown on the Add goat form (matches index_new.html HEALTH_STATES).</summary>
    public static readonly string[] HealthStates =
    [
        "Healthy", "Under treatment", "Quarantine", "Injured", "Weak / off feed"
    ];

    public static readonly string[] ProjectCategories =
    [
        "Construction", "Milking Parlor", "Solar / Power", "Slaughter / Processing",
        "Cold Room / Warehouse", "Water / Utilities", "Other Capital Project"
    ];

    public static readonly string[] CapexCostTypes =
    [
        "Civil / Construction", "Solar Panels", "Solar Inverter", "Solar Structure / Mounting",
        "Battery / Storage", "Milking Parlor Equipment", "Milking Machine / Cluster",
        "Milk Tank / Chiller", "Pasteurizer / Dairy Equipment", "Cold Room Equipment",
        "Electrical", "Plumbing", "Installation Labour", "Professional / Approval Fees",
        "Transport / Delivery", "Other Equipment", "Other Capital Cost"
    ];

    public static readonly string[] ProjectAssetCategories =
    [
        "Buildings / Sheds", "Milking / Dairy Equipment", "Solar / Power",
        "Slaughter / Meat Equipment", "Cold Storage", "Machinery", "Vehicle",
        "Water / Utilities", "Other Asset"
    ];

    public static readonly string[] ProjectFundingSources =
    [
        "Farm Cash", "Shareholder Funding", "Bank / Loan", "Mixed"
    ];
}
