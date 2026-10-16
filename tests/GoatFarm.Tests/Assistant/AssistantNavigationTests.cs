using GoatFarm.Application.Assistant;
using GoatFarm.Domain.Constants;

namespace GoatFarm.Tests.Assistant;

public class AssistantNavigationTests
{
    [Fact]
    public void BuildsAllowlistedFinanceAndReportsPaths()
    {
        Assert.True(AssistantNavigationCatalog.TryBuild("finance", "2026-09", null, null, null, null, out var finance, out _));
        Assert.Equal("/Finance?month=2026-09", finance.Path);

        Assert.True(AssistantNavigationCatalog.TryBuild("reports", null, "lastmonth", null, null, null, out var reports, out _));
        Assert.Equal("/Reports?period=lastmonth", reports.Path);

        Assert.True(AssistantNavigationCatalog.TryBuild("dashboard", null, null, null, null, null, out var dash, out _));
        Assert.Equal("/", dash.Path);

        Assert.True(AssistantNavigationCatalog.TryBuild("milk", null, null, null, null, null, out var milk, out _));
        Assert.Equal("/Milk", milk.Path);
        Assert.Equal(FarmTabs.Products, AssistantNavigationCatalog.Destinations["milk"]);

        Assert.True(AssistantNavigationCatalog.TryBuild("products", "2026-09", null, null, null, null, out var products, out _));
        Assert.Equal("/Products?month=2026-09", products.Path);
    }

    [Theory]
    [InlineData("https://evil.example/x")]
    [InlineData("/Finance?month=2026-09")]
    [InlineData("javascript:alert(1)")]
    [InlineData("../Settings")]
    public void RejectsArbitraryUrlsAndPaths(string destination)
    {
        Assert.False(AssistantNavigationCatalog.TryBuild(destination, null, null, null, null, null, out _, out var error));
        Assert.False(string.IsNullOrWhiteSpace(error));
    }

    [Fact]
    public void RejectsUnknownPagesAndBadQueryValues()
    {
        Assert.False(AssistantNavigationCatalog.TryBuild("settings", null, null, null, null, null, out _, out _));
        Assert.False(AssistantNavigationCatalog.TryBuild("finance", "09-2026", null, null, null, null, out _, out _));
        Assert.False(AssistantNavigationCatalog.TryBuild("reports", null, "forever", null, null, null, out _, out _));
    }

    [Theory]
    [InlineData("trading")]
    [InlineData("projects")]
    [InlineData("admin")]
    [InlineData("backup")]
    [InlineData("backupcenter")]
    public void RejectsModulesNotExposedToAssistant(string destination)
    {
        Assert.False(AssistantNavigationCatalog.Destinations.ContainsKey(destination));
        Assert.False(AssistantNavigationCatalog.TryBuild(destination, null, null, null, null, null, out _, out _));
    }
}
