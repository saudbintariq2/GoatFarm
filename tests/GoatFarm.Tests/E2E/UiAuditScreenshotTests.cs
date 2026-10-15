using System.Text.Json;
using GoatFarm.Infrastructure.Identity;
using Microsoft.Playwright;

namespace GoatFarm.Tests.E2E;

[Collection(E2eCollection.Name)]
[Trait("Category", "E2E")]
public class UiAuditScreenshotTests
{
    private readonly E2eApp _app;
    public UiAuditScreenshotTests(E2eApp app) => _app = app;

    [SkippableFact]
    public async Task CaptureDesktopAndMobilePages_AndRecordAssetStatus()
    {
        Skip.If(_app.SkipReason is not null, _app.SkipReason);
        Skip.If(Environment.GetEnvironmentVariable("GOATFARM_UI_AUDIT") != "1",
            "BLOCKED: screenshot audit is opt-in. Set GOATFARM_UI_AUDIT=1.");

        var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            @".cursor\projects\d-Code-My-Products-Goat-Records-2-0\agent-tools\ui-audit");
        Directory.CreateDirectory(dir);

        var findings = new List<string>();

        await using (var login = await BrowserSession.OpenAsync(_app, 1280, 900))
        {
            var page = login.Page;
            await page.GotoAsync("/Account/Login");
            await page.WaitForSelectorAsync(".login-card");
            findings.AddRange(await AssetStatusAsync(page, "login"));
            await Shot(page, Path.Combine(dir, "01-login-desktop.png"));
            login.AssertHealthy();
        }

        await using var s = await BrowserSession.LoginAsync(_app, IdentitySeedHelper.DefaultAdminEmail,
            IdentitySeedHelper.DefaultAdminPassword, 1280, 900);
        var p = s.Page;

        await s.CreateGoatAsync("AUDIT1", status: "Milking", name: "Audit Doe");
        await s.ApiOkAsync("POST", "/Tasks/Create", new
        {
            title = "Check water trough",
            notes = "",
            type = "Task",
            priority = "Normal",
            dueDate = DateOnly.FromDateTime(DateTime.Today).ToString("yyyy-MM-dd"),
            assigneeUserId = (string?)null,
            goatTag = "AUDIT1"
        });

        var pages = new (string File, string Url)[]
        {
            ("02-dashboard-desktop", "/Dashboard"),
            ("03-herd-desktop", "/Goat"),
            ("04-breeding-desktop", "/Breeding"),
            ("05-tasks-desktop", "/Tasks"),
            ("06-feed-desktop", "/Feed"),
            ("07-milk-desktop", "/Milk"),
            ("08-finance-desktop", "/Finance"),
            ("09-vaccines-desktop", "/Vaccine"),
            ("10-reports-desktop", "/Reports"),
            ("11-settings-desktop", "/Settings")
        };

        foreach (var (file, url) in pages)
        {
            await p.GotoAsync(url);
            await p.WaitForSelectorAsync("#sideNav");
            findings.AddRange(await AssetStatusAsync(p, file));
            var groups = string.Join(",", await p.Locator(".nav-group-title").AllInnerTextsAsync());
            findings.Add($"{file}: title={await p.TitleAsync()} url={p.Url} nav={await p.Locator("#sideNav .nav-link").CountAsync()} groups={groups}");
            await Shot(p, Path.Combine(dir, file + ".png"));
        }

        await using var mobile = await BrowserSession.LoginAsync(_app, IdentitySeedHelper.DefaultAdminEmail,
            IdentitySeedHelper.DefaultAdminPassword, 390, 844);
        var m = mobile.Page;
        await m.GotoAsync("/Dashboard");
        await m.WaitForSelectorAsync("#navToggle");
        findings.Add($"mobile-closed: sidenav-box={await m.Locator("#sideNav").BoundingBoxAsync()} overflowX={await m.EvaluateAsync<int>("() => document.documentElement.scrollWidth - document.documentElement.clientWidth")}");
        await Shot(m, Path.Combine(dir, "12-dashboard-mobile-closed.png"));
        await m.ClickAsync("#navToggle");
        await m.WaitForSelectorAsync("body.nav-open");
        await Shot(m, Path.Combine(dir, "13-dashboard-mobile-nav.png"));
        await m.GotoAsync("/Goat");
        await m.WaitForSelectorAsync("#goatFormPanel");
        await Shot(m, Path.Combine(dir, "14-herd-mobile.png"));
        mobile.AssertHealthy();

        await File.WriteAllLinesAsync(Path.Combine(dir, "findings.txt"), findings);
        s.AssertHealthy();
    }

    private static async Task Shot(IPage page, string path)
    {
        await page.ScreenshotAsync(new PageScreenshotOptions { Path = path, FullPage = true });
    }

    private static async Task<IEnumerable<string>> AssetStatusAsync(IPage page, string label)
    {
        var css = await page.EvaluateAsync<JsonElement>(@"() => {
            const css = [...document.querySelectorAll('link[rel=stylesheet]')].map(l => ({href:l.href, loaded:!!l.sheet}));
            const js = [...document.querySelectorAll('script[src]')].map(s => s.src);
            const shell = !!document.querySelector('#sideNav');
            const tabsBar = !!document.querySelector('.tabs-bar');
            const farmPro = document.body.classList.contains('farm-pro');
            return {css, js, shell, tabsBar, farmPro, body: document.body.className};
        }");
        return [$"{label}: {css.GetRawText()}"];
    }
}
