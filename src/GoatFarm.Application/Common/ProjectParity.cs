using GoatFarm.Domain.Enums;

namespace GoatFarm.Application.Common;

public static class ProjectParity
{
    public static string StatusLabel(ProjectStatus status) => status switch
    {
        ProjectStatus.Planned => "Planned",
        ProjectStatus.Active => "In Progress",
        ProjectStatus.OnHold => "On Hold",
        ProjectStatus.Completed => "Completed",
        _ => status.ToString()
    };

    public static ProjectStatus ParseStatus(string? value) => (value ?? "").Trim() switch
    {
        "Planned" => ProjectStatus.Planned,
        "In Progress" or "Active" => ProjectStatus.Active,
        "On Hold" or "OnHold" => ProjectStatus.OnHold,
        "Completed" => ProjectStatus.Completed,
        _ => ProjectStatus.Active
    };

    public static FixedAssetCategory ParseAssetCategory(string? value)
    {
        var t = (value ?? "").Trim();
        if (t.Contains("Building", StringComparison.OrdinalIgnoreCase) || t.Contains("Shed", StringComparison.OrdinalIgnoreCase))
            return FixedAssetCategory.Building;
        if (t.Contains("Solar", StringComparison.OrdinalIgnoreCase) || t.Contains("Power", StringComparison.OrdinalIgnoreCase))
            return FixedAssetCategory.Solar;
        if (t.Contains("Land", StringComparison.OrdinalIgnoreCase))
            return FixedAssetCategory.Land;
        if (t.Contains("Machin", StringComparison.OrdinalIgnoreCase) ||
            t.Contains("Vehicle", StringComparison.OrdinalIgnoreCase) ||
            t.Contains("Equipment", StringComparison.OrdinalIgnoreCase) ||
            t.Contains("Cold", StringComparison.OrdinalIgnoreCase) ||
            t.Contains("Parlor", StringComparison.OrdinalIgnoreCase) ||
            t.Contains("Tank", StringComparison.OrdinalIgnoreCase))
            return FixedAssetCategory.Machinery;
        return FixedAssetCategory.Other;
    }

    public static string AssetCategoryLabel(FixedAssetCategory category, string? categoryName)
    {
        if (!string.IsNullOrWhiteSpace(categoryName)) return categoryName.Trim();
        return category switch
        {
            FixedAssetCategory.Building => "Buildings / Sheds",
            FixedAssetCategory.Solar => "Solar / Power",
            FixedAssetCategory.Land => "Land",
            FixedAssetCategory.Machinery => "Machinery",
            _ => "Other Asset"
        };
    }
}
