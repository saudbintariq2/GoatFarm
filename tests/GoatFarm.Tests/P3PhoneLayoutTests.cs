namespace GoatFarm.Tests;

/// <summary>P3 — M1 phone herd cards + compact header; Products default By breed.</summary>
public class P3PhoneLayoutTests
{
    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "GoatFarm.sln"))
                || File.Exists(Path.Combine(dir.FullName, "src", "GoatFarm.Web", "GoatFarm.Web.csproj")))
                return dir.FullName;
            dir = dir.Parent;
        }
        throw new InvalidOperationException("Could not locate repo root from " + AppContext.BaseDirectory);
    }

    [Fact]
    public void M1_HerdView_HasPhoneColumnMarkers()
    {
        var view = File.ReadAllText(Path.Combine(RepoRoot(), "src", "GoatFarm.Web", "Views", "Goat", "Index.cshtml"));
        Assert.Contains("herd-table", view);
        Assert.Contains("class=\"c-tag\"", view);
        Assert.Contains("data-label=\"Tag\"", view);
        Assert.Contains("class=\"c-status\"", view);
        Assert.Contains("data-label=\"Status\"", view);
        Assert.Contains("c-breeding", view);
        Assert.Contains("data-label=\"Breeding\"", view);
        Assert.Contains("c-nextjob", view);
        Assert.Contains("data-label=\"Next job\"", view);
        Assert.Contains("c-milk", view);
        Assert.Contains("data-label=\"Milk\"", view);
    }

    [Fact]
    public void M1_HerdCss_CardsOnlyUsefulColumnsOnPhone()
    {
        var css = File.ReadAllText(Path.Combine(RepoRoot(), "src", "GoatFarm.Web", "wwwroot", "css", "goat-records.css"));
        Assert.Contains("@media (max-width: 600px)", css);
        Assert.Contains(".herd-table thead", css);
        Assert.Contains("display:none", css);
        Assert.Contains("td[data-label]::before", css);
        Assert.Contains(":not(.c-tag):not(.c-status):not(.c-breeding):not(.c-nextjob):not(.c-milk)", css);
    }

    [Fact]
    public void M1_Header_CompactSearchPlaceholderAndIconOnly()
    {
        var layout = File.ReadAllText(Path.Combine(RepoRoot(), "src", "GoatFarm.Web", "Views", "Shared", "_Layout.cshtml"));
        Assert.Contains("placeholder=\"Tag / RFID\"", layout);
        Assert.Contains("class=\"search-box\"", layout);
        Assert.Contains("farm-header-search-ico", layout);
        Assert.Contains("date-block", layout);

        var css = File.ReadAllText(Path.Combine(RepoRoot(), "src", "GoatFarm.Web", "wwwroot", "css", "farm-pro.css"));
        Assert.Contains("farm-header-search .search-box", css);
        Assert.Contains("farm-header-search-ico", css);
        Assert.Contains("@media (max-width: 600px)", css);
    }

    [Fact]
    public void ProductsP3_DefaultCountingModeIsByBreed()
    {
        var shell = File.ReadAllText(Path.Combine(RepoRoot(), "src", "GoatFarm.Web", "Views", "Products", "_ProductsShell.cshtml"));
        Assert.Contains("data-sl-mode=\"breed\"", shell);
        Assert.Contains("class=\"active\" aria-checked=\"true\" data-sl-mode=\"breed\"", shell);
        Assert.Contains("data-sl-mode-pane=\"breed\"", shell);
        Assert.DoesNotContain("data-sl-mode-pane=\"breed\" hidden", shell);

        var js = File.ReadAllText(Path.Combine(RepoRoot(), "src", "GoatFarm.Web", "wwwroot", "js", "products-module.js"));
        Assert.Contains("lsGet('mgMode', 'breed')", js);
    }
}
