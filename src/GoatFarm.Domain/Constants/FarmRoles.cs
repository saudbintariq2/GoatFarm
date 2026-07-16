namespace GoatFarm.Domain.Constants;

public static class FarmRoles
{
    public const string Admin = "Admin";
    public const string Manager = "Manager";
    public const string Staff = "Staff";

    public static readonly string[] All = [Admin, Manager, Staff];
}

public static class FarmTabs
{
    public const string Dashboard = "dashboard";
    public const string Herd = "herd";
    public const string Search = "search";
    public const string Feed = "feed";
    public const string Milk = "milk";
    public const string Finance = "finance";
    public const string Vaccines = "vaccines";
    public const string Settings = "settings";

    public static readonly (string Key, string Label)[] All =
    [
        (Dashboard, "Dashboard"),
        (Herd, "Herd"),
        (Feed, "Feed & Cost"),
        (Milk, "Milk"),
        (Finance, "Finance"),
        (Vaccines, "Vaccines"),
        (Search, "Search"),
        (Settings, "Settings")
    ];
}

public static class FarmActions
{
    public const string View = "view";
    public const string Add = "add";
    public const string Edit = "edit";
    public const string Delete = "delete";

    public static readonly string[] All = [View, Add, Edit, Delete];
}
