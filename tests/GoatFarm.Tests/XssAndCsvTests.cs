using System.Text.RegularExpressions;
using GoatFarm.Application.Interfaces;
using GoatFarm.Application.ViewModels.Feed;
using GoatFarm.Domain.Enums;
using GoatFarm.Infrastructure;
using GoatFarm.Infrastructure.Persistence;
using GoatFarm.Tests.Postgres;
using Jint;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace GoatFarm.Tests;

public class XssAndCsvTests
{
    private const string Payload = "<img src=x onerror=alert(1)>";
    private const string Encoded = "&lt;img src=x onerror=alert(1)&gt;";

    [SkippableFact]
    [Trait("Category", "Postgres")]
    public async Task ReportAlerts_EncodeUserEnteredTagsAndFeedNames()
    {
        await using var pg = await PostgresTestDatabase.CreateAsync();
        await using (var db = pg.CreateContext())
        {
            await FarmScenario.SeedDefaultsAsync(db);
            var doe = TestDb.Doe(Payload);
            doe.MatedDate = TestDb.Today.AddDays(-60);
            doe.BreedingStage = BreedingStage.ScanPending;
            db.Goats.Add(doe);
            var feed = await db.FeedPrices.FirstAsync(f => f.FeedType != "fodder");
            feed.DisplayName = Payload;
            feed.StockKg = 0;
            await db.SaveChangesAsync();
        }

        await using var provider = BuildServices(pg.ConnectionString);
        using var scope = provider.CreateScope();
        var reports = scope.ServiceProvider.GetRequiredService<IReportsService>();

        var page = await reports.GetReportsPageAsync("month", null, null);

        var html = string.Join("\n", page.Alerts.Items.Select(a => a.Html));
        Assert.Contains($"Scan due: <b>{Encoded}</b>", html);
        Assert.Contains($"Out of stock: <b>{Encoded}</b>", html);
        Assert.DoesNotContain("<img", html);
    }

    [SkippableFact]
    [Trait("Category", "Postgres")]
    public async Task StockCheckDetail_EncodesFeedDisplayName()
    {
        await using var pg = await PostgresTestDatabase.CreateAsync();
        string feedType;
        await using (var db = pg.CreateContext())
        {
            await FarmScenario.SeedDefaultsAsync(db);
            var feed = await db.FeedPrices.FirstAsync(f => f.FeedType != "fodder");
            feed.DisplayName = Payload;
            feed.StockKg = 100;
            feedType = feed.FeedType;
            await db.SaveChangesAsync();
        }

        await using var provider = BuildServices(pg.ConnectionString);
        using var scope = provider.CreateScope();
        var feedService = scope.ServiceProvider.GetRequiredService<IFeedService>();

        var result = await feedService.SaveStockCheckAsync(new SaveStockCheckViewModel { Counts = { [feedType] = 90 } });

        Assert.True(result.Success);
        Assert.Contains(Encoded, result.DetailHtml);
        Assert.DoesNotContain("<img", result.DetailHtml);
    }

    [Theory]
    [InlineData("<script>alert(1)</script>", "&lt;script&gt;alert(1)&lt;/script&gt;")]
    [InlineData("\" onmouseover=\"x", "&quot; onmouseover=&quot;x")]
    [InlineData("O'Brien & Sons", "O&#39;Brien &amp; Sons")]
    [InlineData("اردو نوٹ", "اردو نوٹ")]
    public void JsEsc_EncodesHtmlAndKeepsUnicode(string input, string expected) =>
        Assert.Equal(expected, ShippedHelpers().Invoke("esc", input).AsString());

    [Fact]
    public void JsEsc_TreatsNullAndUndefinedAsEmpty()
    {
        var js = ShippedHelpers();
        Assert.Equal("", js.Evaluate("esc(null)").AsString());
        Assert.Equal("", js.Evaluate("esc(undefined)").AsString());
        Assert.Equal("0", js.Evaluate("esc(0)").AsString());
    }

    [Theory]
    [InlineData("=HYPERLINK(\"http://evil\",\"x\")", "\"'=HYPERLINK(\"\"http://evil\"\",\"\"x\"\")\"")]
    [InlineData("+cmd|' /C calc'!A0", "'+cmd|' /C calc'!A0")]
    [InlineData("-2+3", "'-2+3")]
    [InlineData("@SUM(A1:A2)", "'@SUM(A1:A2)")]
    [InlineData("\tTAB", "'\tTAB")]
    [InlineData("\r=1", "\"'\r=1\"")]
    [InlineData("- Rs 500", "'- Rs 500")]
    [InlineData("-1,250.5", "\"-1,250.5\"")]
    [InlineData("-12", "-12")]
    [InlineData("+5", "+5")]
    [InlineData("Rs 1,200", "\"Rs 1,200\"")]
    [InlineData("a \"quoted\" note", "\"a \"\"quoted\"\" note\"")]
    [InlineData("line1\nline2", "\"line1\nline2\"")]
    [InlineData("بیتل بکری", "بیتل بکری")]
    [InlineData("", "")]
    public void JsCsvCell_NeutralisesFormulasQuotesAndKeepsUnicode(string input, string expected) =>
        Assert.Equal(expected, ShippedHelpers().Invoke("csvCell", input).AsString());

    [Fact]
    public void ReportCsv_UsesSharedCellEscaperAndUtf8Bom()
    {
        var source = File.ReadAllText(GoatRecordsJsPath());
        Assert.DoesNotContain("csvEsc", source);
        Assert.Contains("r.map(csvCell)", source);
        Assert.Contains("new Blob(['\\ufeff' + csv]", source);
    }

    private static ServiceProvider BuildServices(string connectionString)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["ConnectionStrings:DefaultConnection"] = connectionString })
            .Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddInfrastructure(config);
        return services.BuildServiceProvider();
    }

    private static Engine ShippedHelpers()
    {
        var source = File.ReadAllText(GoatRecordsJsPath());
        var engine = new Engine();
        foreach (var name in new[] { "esc", "csvCell" })
        {
            var match = Regex.Match(source, $@"^\s*const {name} = .*;\s*$", RegexOptions.Multiline);
            Assert.True(match.Success, $"goat-records.js no longer defines '{name}' on a single line.");
            engine.Execute(match.Value.Trim().Replace($"const {name} =", $"var {name} ="));
        }
        return engine;
    }

    private static string GoatRecordsJsPath()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "GoatFarm.slnx")))
            dir = dir.Parent;
        Assert.NotNull(dir);
        return Path.Combine(dir!.FullName, "src", "GoatFarm.Web", "wwwroot", "js", "goat-records.js");
    }
}
