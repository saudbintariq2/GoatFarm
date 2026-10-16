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
    public const string Breeding = "breeding";
    public const string Search = "search";
    public const string Feed = "feed";
    public const string Milk = "milk";
    public const string Finance = "finance";
    public const string Reports = "reports";
    public const string Vaccines = "vaccines";
    public const string Settings = "settings";
    public const string Tasks = "tasks";
    public const string Assistant = "assistant";
    public const string Trading = "trading";
    public const string Products = "products";
    public const string Projects = "projects";
    public const string Admin = "admin";
    public const string Backup = "backup";

    public static readonly (string Key, string Label)[] All =
    [
        (Dashboard, "Dashboard"),
        (Tasks, "Tasks"),
        (Herd, "Herd"),
        (Breeding, "Breeding"),
        (Products, "Products"),
        (Feed, "Feed & Cost"),
        (Trading, "Trading"),
        (Vaccines, "Vaccines"),
        (Finance, "Finance"),
        (Projects, "Projects & Assets"),
        (Reports, "Reports"),
        (Admin, "Users & Audit"),
        (Search, "Search"),
        (Settings, "Settings"),
        (Backup, "Backup Center"),
        (Milk, "Milk (legacy tab)"),
        (Assistant, "AI Assistant")
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
