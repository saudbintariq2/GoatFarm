using System.Text.Json;
using System.Text.RegularExpressions;
using GoatFarm.Infrastructure.Identity;
using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace GoatFarm.Tests.E2E;

[Collection(E2eCollection.Name)]
[Trait("Category", "E2E")]
public class BrowserFlowTests
{
    private const string AdminEmail = IdentitySeedHelper.DefaultAdminEmail;
    private const string AdminPassword = IdentitySeedHelper.DefaultAdminPassword;
    private const string StrongPassword = "Str0ng!Passw0rd";

    private readonly E2eApp _app;

    public BrowserFlowTests(E2eApp app) => _app = app;

    private static string Today(int offset = 0) => DateOnly.FromDateTime(DateTime.Today).AddDays(offset).ToString("yyyy-MM-dd");
    private static string Uid() => Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();

    private async Task<BrowserSession> AdminAsync(int width = 1280, int height = 900)
    {
        Skip.If(_app.SkipReason is not null, _app.SkipReason);
        return await BrowserSession.LoginAsync(_app, AdminEmail, AdminPassword, width, height);
    }

    private Task<long> Sql(string sql) => _app.Db.ScalarAsync(sql);

    private Task<long> GoatId(string tag) => Sql($"select \"Id\" from \"Goats\" where \"Tag\" = '{tag}'");

    // 1
    [SkippableFact]
    public async Task LoginLogout_RejectsBadPassword_SignsInAndOut_AndProtectsPages()
    {
        Skip.If(_app.SkipReason is not null, _app.SkipReason);
        await using var s = await BrowserSession.OpenAsync(_app);
        var page = s.Page;

        await page.GotoAsync("/Goat");
        await page.WaitForURLAsync(new Regex("/Account/Login", RegexOptions.IgnoreCase));

        await page.FillAsync("#Email", AdminEmail);
        await page.FillAsync("#Password", "wrong-password");
        await page.ClickAsync("button.login-submit");
        await Expect(page.Locator(".login-error")).Not.ToBeEmptyAsync();
        await Expect(page).ToHaveURLAsync(new Regex("/Account/Login", RegexOptions.IgnoreCase));
        Assert.Equal("password", await page.GetAttributeAsync("#Password", "type"));
        await page.ClickAsync("#pwToggle");
        Assert.Equal("text", await page.GetAttributeAsync("#Password", "type"));

        await s.SignInAsync(AdminEmail, AdminPassword);
        await Expect(page.Locator("#sideNav")).ToBeVisibleAsync();
        await Expect(page.Locator(".top-actions .scopechip")).ToHaveTextAsync("Admin");

        await page.ClickAsync("form[action*='Logout'] button");
        await page.WaitForURLAsync(new Regex("/Account/Login", RegexOptions.IgnoreCase));
        await page.GotoAsync("/Finance");
        await page.WaitForURLAsync(new Regex("/Account/Login", RegexOptions.IgnoreCase));
        await Expect(page.Locator("#sideNav")).ToHaveCountAsync(0);
        s.AssertHealthy();
    }

    // 2
    [SkippableFact]
    public async Task ResponsiveNav_DesktopCollapses_MobileOpensAndClosesWithoutOverflow()
    {
        await using var s = await AdminAsync();
        var page = s.Page;
        await page.GotoAsync("/Dashboard");
        var links = page.Locator("#sideNav .nav-link");
        Assert.True(await links.CountAsync() >= 10, "Admin should see every module in the nav.");
        await Expect(page.Locator("#navToggle")).ToHaveAttributeAsync("aria-expanded", "true");
        await page.ClickAsync("#navToggle");
        await Expect(page.Locator("body")).ToHaveClassAsync(new Regex("nav-collapsed"));
        await page.ClickAsync("#navToggle");
        await Expect(page.Locator("body")).Not.ToHaveClassAsync(new Regex("nav-collapsed"));

        await page.SetViewportSizeAsync(390, 844);
        await page.ReloadAsync();
        await Expect(page.Locator("#navToggle")).ToHaveAttributeAsync("aria-expanded", "false");
        await page.ClickAsync("#navToggle");
        await Expect(page.Locator("#navToggle")).ToHaveAttributeAsync("aria-expanded", "true");
        await Expect(page.Locator("#navScrim")).ToBeVisibleAsync();
        await page.Keyboard.PressAsync("Escape");
        await Expect(page.Locator("#navToggle")).ToHaveAttributeAsync("aria-expanded", "false");

        await page.ClickAsync("#navToggle");
        await page.Locator("#sideNav .nav-link", new() { HasText = "Herd" }).ClickAsync();
        await page.WaitForURLAsync(new Regex("/Goat", RegexOptions.IgnoreCase));
        var overflow = await page.EvaluateAsync<bool>("() => document.documentElement.scrollWidth > window.innerWidth + 1");
        Assert.False(overflow, "Herd page scrolls sideways on a 390px phone screen.");
        s.AssertHealthy();
    }

    // 3
    [SkippableFact]
    public async Task Dashboard_RendersPanelsAndStatsEndpoint()
    {
        await using var s = await AdminAsync();
        var page = s.Page;
        await page.GotoAsync("/");
        await Expect(page.Locator(".dash-panel h2", new() { HasText = "Herd" })).ToBeVisibleAsync();
        await Expect(page.Locator(".dash-panel h2", new() { HasText = "My open tasks" })).ToBeVisibleAsync();
        await Expect(page.Locator(".dash-sandeel .dash-kpi-row")).ToBeVisibleAsync();
        await Expect(page.Locator("#dashActions h2")).ToHaveTextAsync("Quick actions");
        await Expect(page.Locator("#dashAttentionPanel h2")).ToHaveTextAsync("Needs attention");
        await Expect(page.Locator(".nav-link.active")).ToHaveTextAsync(new Regex("Dashboard"));
        await Expect(page.Locator("body")).ToHaveClassAsync(new Regex("farm-pro"));
        await Expect(page.Locator("link[href*='farm-pro.css']")).ToHaveCountAsync(1);

        var stats = await s.ApiOkAsync("GET", "/Dashboard/Stats");
        Assert.Equal(JsonValueKind.Object, stats.ValueKind);
        s.AssertHealthy();
    }

    // 4
    [SkippableFact]
    public async Task HerdFilterAndPaging_PagesSearchResultsAndCombinesFilters()
    {
        await using var s = await AdminAsync();
        var page = s.Page;
        await page.GotoAsync("/Goat");
        var prefix = "PG" + Uid();
        for (var i = 1; i <= 22; i++)
            await s.CreateGoatAsync($"{prefix}-{i:00}", ageDays: 400 + i);
        for (var i = 1; i <= 3; i++)
            await s.CreateGoatAsync($"{prefix}-B{i}", gender: "Male", status: "Buck");

        await page.FillAsync("#herdSearch", prefix);
        await page.PressAsync("#herdSearch", "Enter");
        await page.WaitForURLAsync(new Regex("q=" + prefix));
        await Expect(page.Locator(".pager-info")).ToContainTextAsync("of 25 goats");
        await Expect(page.Locator("#rows tr.goat-row")).ToHaveCountAsync(10);
        await Expect(page.Locator("#rows tr.goat-row").First).ToContainTextAsync($"{prefix}-B3");

        await page.ClickAsync(".pager-nav a:has-text('Next')");
        await page.WaitForURLAsync(new Regex("page=2"));
        await Expect(page.Locator(".pager-info")).ToContainTextAsync("11");
        await Expect(page.Locator("#rows tr.goat-row")).ToHaveCountAsync(10);
        Assert.Equal(prefix, await page.InputValueAsync("#herdSearch"));

        await page.SelectOptionAsync("#filter", "st:buck");
        await page.WaitForURLAsync(new Regex("filter=st(%3A|:)buck"));
        await Expect(page.Locator("#rows tr.goat-row")).ToHaveCountAsync(3);
        await Expect(page.Locator(".pager-info")).ToContainTextAsync("of 3 goats");
        Assert.Contains("q=" + prefix, page.Url);

        await page.GotoAsync($"/Goat?filter=all&page=999&q={prefix}");
        await Expect(page.Locator("#rows tr.goat-row")).ToHaveCountAsync(5);
        s.AssertHealthy();
    }

    // 5
    [SkippableFact]
    public async Task GoatCreate_ThroughForm_ThenFoundByGlobalTagSearch()
    {
        await using var s = await AdminAsync();
        var page = s.Page;
        var tag = "NEW" + Uid();
        await page.GotoAsync("/Goat");
        await page.FillAsync("#f-tag", tag);
        await page.FillAsync("#f-name", "Rani — رانی");
        await page.SelectOptionAsync("#f-gender", "Female");
        await page.SelectOptionAsync("#f-source", "Born");
        await page.SelectOptionAsync("#f-status", "Dry");
        await page.FillAsync("#f-date", Today(-420));
        await page.FillAsync("#f-owner", "Partner <b>Ali</b>");
        await page.ClickAsync("#addBtn");
        await Expect(page.Locator("#toastHost")).ToContainTextAsync("Goat added");
        Assert.True(await GoatId(tag) > 0);

        await page.FillAsync("#globalTag", tag.ToLowerInvariant());
        await page.PressAsync("#globalTag", "Enter");
        await page.WaitForURLAsync(new Regex("/Search", RegexOptions.IgnoreCase));
        await Expect(page.Locator("#search-results")).ToContainTextAsync(tag);
        await Expect(page.Locator("#search-results")).ToContainTextAsync("رانی");
        await Expect(page.Locator("#search-results b", new() { HasText = "Ali" })).ToHaveCountAsync(0);

        s.ExpectStatuses(400, 409);
        await page.GotoAsync("/Goat");
        await page.FillAsync("#f-tag", tag);
        await page.FillAsync("#f-date", Today(-10));
        await page.ClickAsync("#addBtn");
        await Expect(page.Locator("#appModalMsg")).Not.ToBeEmptyAsync();
        Assert.Equal(1, await Sql($"select count(*) from \"Goats\" where upper(\"Tag\") = '{tag}'"));
        s.AssertHealthy();
    }

    // 6
    [SkippableFact]
    public async Task BreedingEligibility_EnforcesAgeSexAndUnknownAgeRules()
    {
        await using var s = await AdminAsync();
        var page = s.Page;
        var id = Uid();
        await page.GotoAsync("/Breeding");
        await s.CreateGoatAsync($"YOUNG{id}", ageDays: 120, status: "Kid");
        await s.CreateGoatAsync($"ADULT{id}", ageDays: 500);
        await s.CreateGoatAsync($"BOUGHTKID{id}", source: "Bought", ageDays: 5, status: "Kid");
        await s.CreateGoatAsync($"MALE{id}", gender: "Male", status: "Buck");
        await s.CreateGoatAsync($"BUCKF{id}", ageDays: 600);

        async Task Check(string doe, string? buck, string expected, bool ok)
        {
            await page.FillAsync("#bc-tag", doe);
            await page.FillAsync("#bc-buck", buck ?? "");
            await page.ClickAsync("#checkCross");
            var box = page.Locator(ok ? "#bc-check .cross-ok" : "#bc-check .cross-errors");
            await Expect(box).ToContainTextAsync(expected);
        }

        await Check($"YOUNG{id}", null, "300 days", ok: false);
        await Check($"BOUGHTKID{id}", null, "age is unknown", ok: false);
        await Check($"MALE{id}", null, "not a doe", ok: false);
        await Check($"ADULT{id}", $"BUCKF{id}", "not a male", ok: false);
        await Check($"ADULT{id}", $"MALE{id}", "can be crossed", ok: true);

        s.ExpectStatuses(400);
        await page.FillAsync("#bc-tag", $"YOUNG{id}");
        await page.FillAsync("#bc-buck", "");
        await page.FillAsync("#bc-date", Today());
        await page.ClickAsync("#addCross");
        await s.CloseModalAsync("300 days");
        Assert.Equal(0, await Sql($"select count(*) from \"BreedingEvents\" e join \"Goats\" g on g.\"Id\" = e.\"DoeId\" where g.\"Tag\" = 'YOUNG{id}'"));
        s.AssertHealthy();
    }

    // 7
    [SkippableFact]
    public async Task Scans_PositiveConfirmsPregnancy_NegativeClosesCycleAsEmpty()
    {
        await using var s = await AdminAsync();
        var page = s.Page;
        var id = Uid();
        await page.GotoAsync("/Breeding");
        foreach (var t in new[] { $"POS{id}", $"NEG{id}" })
        {
            await s.CreateGoatAsync(t);
            await s.ApiOkAsync("POST", "/Breeding/RecordCross", new { tag = t, date = Today(-50), confirmWarnings = true });
        }
        await page.ReloadAsync();

        await page.FillAsync("#us-tag", $"POS{id}");
        await page.SelectOptionAsync("#us-result", "2");
        await page.ClickAsync("#addUs");
        await Expect(page.Locator("#toastHost")).ToContainTextAsync("Pregnancy confirmed");
        Assert.Equal(3, await Sql($"select \"BreedingStage\" from \"Goats\" where \"Tag\" = 'POS{id}'"));
        Assert.Equal(1, await Sql($"select \"Outcome\" from \"BreedingEvents\" where \"DoeId\" = {await GoatId($"POS{id}")}"));

        await page.FillAsync("#us-tag", $"NEG{id}");
        await page.SelectOptionAsync("#us-result", "0");
        await page.ClickAsync("#addUs");
        await s.CloseModalAsync("NOT pregnant");
        await Expect(page.Locator("#toastHost")).ToContainTextAsync("Recorded as empty");
        Assert.Equal(2, await Sql($"select \"Outcome\" from \"BreedingEvents\" where \"DoeId\" = {await GoatId($"NEG{id}")}"));
        Assert.Equal(0, await Sql($"select count(*) from \"Goats\" where \"Tag\" = 'NEG{id}' and \"MatedDate\" is not null"));
        s.AssertHealthy();
    }

    // 8
    [SkippableFact]
    public async Task Birth_RecordsKidsOnce_EvenWhenSubmittedRepeatedlyOrConcurrently()
    {
        await using var s = await AdminAsync();
        var page = s.Page;
        var id = Uid();
        await page.GotoAsync("/Breeding");
        foreach (var t in new[] { $"MUM{id}", $"RACE{id}" })
        {
            await s.CreateGoatAsync(t);
            await s.ApiOkAsync("POST", "/Breeding/RecordCross", new { tag = t, date = Today(-148), confirmWarnings = true });
            await s.ApiOkAsync("POST", "/Breeding/RecordUltrasound", new { tag = t, kidsCount = 2, date = Today(-100) });
        }
        await page.ReloadAsync();

        await page.ClickAsync($"[data-birth='MUM{id}']");
        await Expect(page.Locator("#birthPanel")).ToBeVisibleAsync();
        Assert.Equal("2", await page.InputValueAsync("#kb-live"));
        await page.Locator("#kb-kids tr").Nth(0).Locator("[data-k=tag]").FillAsync($"KIDA{id}");
        await page.Locator("#kb-kids tr").Nth(1).Locator("[data-k=gender]").SelectOptionAsync("Male");
        await page.Locator("#kb-save").DblClickAsync();
        await Expect(page.Locator("#toastHost")).ToContainTextAsync("Birth recorded");

        var mum = await GoatId($"MUM{id}");
        Assert.Equal(2, await Sql($"select count(*) from \"Goats\" where \"DamId\" = {mum}"));
        Assert.Equal(1, await Sql($"select count(*) from \"KiddingRecords\" where \"DoeId\" = {mum}"));

        var sent = s.RequestBodies.Where(b => b.Contains("/Breeding/RecordBirth")).ToList();
        Assert.Single(sent);
        var replayBody = JsonSerializer.Deserialize<JsonElement>(sent[0][(sent[0].IndexOf('\n') + 1)..]);
        var replay = await s.ApiOkAsync("POST", "/Breeding/RecordBirth", replayBody);
        Assert.True(replay.GetProperty("alreadyRecorded").GetBoolean());
        Assert.Equal(2, await Sql($"select count(*) from \"Goats\" where \"DamId\" = {mum}"));

        s.ExpectServerLogCategories("Microsoft.EntityFrameworkCore.Update", "Microsoft.EntityFrameworkCore.Database.Command");
        var race = await page.EvaluateAsync<JsonElement>(
            @"async (tag) => {
                const body = JSON.stringify({ clientRequestId: crypto.randomUUID(), doeTag: tag, kiddingDate: new Date().toISOString().slice(0,10),
                                              ease: 'Normal', stillborn: 0, kids: [{ gender: 'Female' }, { gender: 'Male' }] });
                const send = () => fetch('/Breeding/RecordBirth', { method: 'POST', headers: { 'Content-Type': 'application/json', 'Accept': 'application/json' }, body })
                                    .then(async r => ({ status: r.status, body: await r.json().catch(() => null) }));
                return Promise.all([send(), send(), send()]);
            }", $"RACE{id}");
        Assert.All(race.EnumerateArray(), r => Assert.Equal(200, r.GetProperty("status").GetInt32()));
        Assert.Equal(1, race.EnumerateArray().Count(r => !r.GetProperty("body").GetProperty("alreadyRecorded").GetBoolean()));
        var race1 = await GoatId($"RACE{id}");
        Assert.Equal(1, await Sql($"select count(*) from \"KiddingRecords\" where \"DoeId\" = {race1}"));
        Assert.Equal(2, await Sql($"select count(*) from \"Goats\" where \"DamId\" = {race1}"));
        s.AssertHealthy();
    }

    // 9
    [SkippableFact]
    public async Task BulkBreeding_OneIneligibleDoe_RollsBackTheWholeSelection()
    {
        await using var s = await AdminAsync();
        var page = s.Page;
        var id = "BK" + Uid();
        await page.GotoAsync("/Goat");
        await s.CreateGoatAsync($"{id}-OK1");
        await s.CreateGoatAsync($"{id}-OK2", ageDays: 800);
        await s.CreateGoatAsync($"{id}-YOUNG", ageDays: 90, status: "Kid");
        await page.GotoAsync($"/Goat?filter=all&q={id}");
        await Expect(page.Locator("#rows tr.goat-row")).ToHaveCountAsync(3);

        s.ExpectStatuses(400);
        await page.CheckAsync("#checkAll");
        await Expect(page.Locator("#selCount")).ToHaveTextAsync("3 selected");
        await page.SelectOptionAsync("#moveTo", "br:prep");
        await page.ClickAsync("#moveBtn");
        await page.ClickAsync("#appModalOk");
        await s.CloseModalAsync("Nothing was changed");
        Assert.Equal(0, await Sql($"select count(*) from \"Goats\" where \"Tag\" like '{id}-%' and (\"PrepCrossDate\" is not null or \"BreedingStage\" <> 0)"));

        await page.SelectOptionAsync("#moveTo", "br:cross");
        await page.ClickAsync("#moveBtn");
        await page.ClickAsync("#appModalOk");
        await page.ClickAsync("#appModalCancel");
        await s.CloseModalAsync("Nothing was changed");
        Assert.Equal(0, await Sql($"select count(*) from \"Goats\" where \"Tag\" like '{id}-%' and \"MatedDate\" is not null"));
        Assert.Equal(0, await Sql($"select count(*) from \"BreedingEvents\" e join \"Goats\" g on g.\"Id\" = e.\"DoeId\" where g.\"Tag\" like '{id}-%'"));

        await page.UncheckAsync($"#rows tr.goat-row:has-text('{id}-YOUNG') input[type=checkbox]");
        await Expect(page.Locator("#selCount")).ToHaveTextAsync("2 selected");
        await page.SelectOptionAsync("#moveTo", "br:cross");
        await page.ClickAsync("#moveBtn");
        await page.ClickAsync("#appModalOk");
        await page.RunAndWaitForNavigationAsync(() => page.ClickAsync("#appModalCancel"));
        Assert.Equal(2, await Sql($"select count(*) from \"Goats\" where \"Tag\" like '{id}-OK%' and \"MatedDate\" is not null and \"BreedingStage\" = 2"));
        Assert.Equal(2, await Sql($"select count(*) from \"BreedingEvents\" e join \"Goats\" g on g.\"Id\" = e.\"DoeId\" where g.\"Tag\" like '{id}-%'"));
        s.AssertHealthy();
    }

    // 10
    [SkippableFact]
    public async Task Tasks_CreateStartCompleteAndValidate()
    {
        await using var s = await AdminAsync();
        var page = s.Page;
        var title = "Trim hooves " + Uid();
        await page.GotoAsync("/Tasks");

        await page.ClickAsync("#saveTask");
        await s.CloseModalAsync("Enter a task title");

        await page.FillAsync("#t-title", title);
        await page.FillAsync("#t-notes", "اردو نوٹ <script>alert(1)</script>");
        await page.FillAsync("#t-due", Today(2));
        await page.SelectOptionAsync("#t-priority", "High");
        await page.ClickAsync("#saveTask");
        await Expect(page.Locator("#toastHost")).ToContainTextAsync("Task added");

        var card = page.Locator(".task-card", new() { HasText = title });
        await Expect(card).ToHaveCountAsync(1);
        await card.Locator("[data-task-status=InProgress]").ClickAsync();
        await Expect(page.Locator("#toastHost")).ToContainTextAsync("Task updated");
        await Expect(card.Locator("[data-task-status=InProgress]")).ToHaveCountAsync(0);
        await card.Locator("[data-task-status=Completed]").ClickAsync();
        await Expect(page.Locator("#toastHost")).ToContainTextAsync("Task completed");
        await Expect(card.Locator("[data-task-status=ToDo]")).ToHaveCountAsync(1);

        Assert.Equal(2, await Sql($"select \"Status\" from \"FarmTasks\" where \"Title\" = '{title}'"));
        Assert.Equal(1, await Sql($"select count(*) from \"FarmTasks\" where \"Title\" = '{title}' and \"StartedAtUtc\" is not null and \"CompletedAtUtc\" is not null and \"Notes\" like 'اردو نوٹ%'"));
        s.AssertHealthy();
    }

    // 11
    [SkippableFact]
    public async Task BackupAndSettings_AdminOnly_StaffAndDelegatedManagerCannotEscalate()
    {
        await using var admin = await AdminAsync();
        await admin.Page.GotoAsync("/Dashboard");
        var (exportStatus, export) = await admin.ApiAsync("GET", "/Dashboard/Export");
        Assert.Equal(200, exportStatus);
        Assert.Equal(2, export.GetProperty("formatVersion").GetInt32());
        var raw = export.GetRawText();
        Assert.DoesNotContain("passwordHash", raw, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("securityStamp", raw, StringComparison.OrdinalIgnoreCase);

        var id = Uid().ToLowerInvariant();
        var staffEmail = $"staff{id}@farm.test";
        var managerEmail = $"manager{id}@farm.test";
        await admin.ApiOkAsync("POST", "/Settings/CreateUser", new { fullName = "Staff " + id, email = staffEmail, password = StrongPassword, role = "Staff" });
        var manager = await admin.ApiOkAsync("POST", "/Settings/CreateUser", new { fullName = "Manager " + id, email = managerEmail, password = StrongPassword, role = "Manager" });
        var managerId = manager.GetProperty("id").GetString()!;
        var full = new { view = true, add = true, edit = true, delete = true };
        await admin.ApiOkAsync("PUT", $"/Settings/SaveUserPermissions?id={managerId}", new
        {
            usesRolePermissions = false,
            permissions = new Dictionary<string, object> { ["settings"] = full, ["herd"] = full, ["dashboard"] = new { view = true } }
        });
        var goatsBefore = await Sql("select count(*) from \"Goats\"");

        await using (var staff = await BrowserSession.LoginAsync(_app, staffEmail, StrongPassword))
        {
            staff.ExpectStatuses(403);
            await staff.Page.GotoAsync("/Dashboard");
            await Expect(staff.Page.Locator("#sideNav .nav-link", new() { HasText = "Finance" })).ToHaveCountAsync(0);
            Assert.Equal(403, (await staff.ApiAsync("GET", "/Dashboard/Export")).Status);
            var importStatus = await staff.Page.EvaluateAsync<int>(
                @"async () => { const fd = new FormData(); fd.append('file', new Blob(['{}'], { type: 'application/json' }), 'x.json');
                               return (await fetch('/Dashboard/Import', { method: 'POST', body: fd, headers: { 'Accept': 'application/json' } })).status; }");
            Assert.Equal(403, importStatus);
            Assert.Equal(403, (await staff.ApiAsync("PUT", $"/Settings/SaveUserPermissions?id={managerId}", new { usesRolePermissions = true })).Status);
            Assert.Equal(403, (await staff.ApiAsync("POST", "/Finance/AddExpense", new { type = "Repairs", amount = 10, date = Today() })).Status);
            await staff.Page.GotoAsync("/Settings");
            await staff.Page.WaitForURLAsync(new Regex("AccessDenied", RegexOptions.IgnoreCase));
            staff.AssertHealthy();
        }
        Assert.Equal(goatsBefore, await Sql("select count(*) from \"Goats\""));

        await using (var mgr = await BrowserSession.LoginAsync(_app, managerEmail, StrongPassword))
        {
            mgr.ExpectStatuses(403);
            await mgr.Page.GotoAsync("/Settings");
            var self = await mgr.ApiAsync("PUT", $"/Settings/UpdateUser?id={managerId}", new { fullName = "Me", role = "Admin", isLocked = false });
            Assert.Equal(403, self.Status);
            var newAdmin = await mgr.ApiAsync("POST", "/Settings/CreateUser", new { fullName = "Sock", email = $"sock{id}@farm.test", password = StrongPassword, role = "Admin" });
            Assert.Equal(403, newAdmin.Status);
            var proxy = await mgr.ApiAsync("POST", "/Settings/CreateUser", new { fullName = "Proxy", email = $"proxy{id}@farm.test", password = StrongPassword, role = "Manager" });
            Assert.Equal(403, proxy.Status);
            Assert.Contains("do not have yourself", proxy.Body.GetProperty("error").GetString());
            var roles = await mgr.ApiAsync("PUT", "/Settings/SaveRolePermissions", new { permissions = new { } });
            Assert.Equal(403, roles.Status);
            Assert.Equal(1, await Sql($"select count(*) from \"AspNetUserRoles\" ur join \"AspNetRoles\" r on r.\"Id\" = ur.\"RoleId\" where r.\"Name\" = 'Manager' and ur.\"UserId\" = '{managerId}'"));
            mgr.AssertHealthy();
        }
        Assert.Equal(0, await Sql($"select count(*) from \"AspNetUsers\" where \"NormalizedEmail\" in ('SOCK{id.ToUpperInvariant()}@FARM.TEST', 'PROXY{id.ToUpperInvariant()}@FARM.TEST')"));
        Assert.Equal(1, await Sql("select count(*) from \"AspNetUserRoles\" ur join \"AspNetRoles\" r on r.\"Id\" = ur.\"RoleId\" where r.\"Name\" = 'Admin'"));
        admin.AssertHealthy();
    }

    // 12
    [SkippableFact]
    public async Task ValidationErrorsAndEmptyStates_AreShownWithoutServerErrors()
    {
        await using         var s = await AdminAsync();
        var page = s.Page;
        s.ExpectStatuses(400, 404, 409);

        await page.GotoAsync("/Goat?filter=all&q=NO-SUCH-GOAT-" + Uid());
        await Expect(page.Locator("#empty")).ToBeVisibleAsync();
        await Expect(page.Locator("#empty")).ToContainTextAsync("No goats match this search");
        await Expect(page.Locator(".pager")).ToHaveCountAsync(0);

        await page.ClickAsync("#addBtn");
        await s.CloseModalAsync("Please enter a Tag");
        var (status, body) = await s.ApiAsync("POST", "/Goat/Create", new { tag = new string('X', 60), breed = "Beetal", gender = "Female", source = "Born", eventDate = Today(), status = "Kid" });
        Assert.Equal(400, status);
        Assert.Contains("Tag", body.GetRawText());

        await page.GotoAsync("/Search?tag=NOPE" + Uid());
        await Expect(page.Locator("body")).ToContainTextAsync("No goat found with tag");

        await page.GotoAsync("/Breeding");
        await page.FillAsync("#us-tag", "NOPE" + Uid());
        await page.ClickAsync("#addUs");
        await Expect(page.Locator("#appModalMsg")).Not.ToBeEmptyAsync();
        await page.ClickAsync("#appModalOk");

        await page.GotoAsync("/Tasks?filter=overdue");
        await Expect(page.Locator("#taskFilter")).ToHaveValueAsync("overdue");

        var expense = await s.ApiAsync("POST", "/Finance/AddExpense", new { type = "Salary advance", amount = 5000, date = Today(), confirmPossibleDuplicate = true });
        Assert.Equal(409, expense.Status);
        Assert.Equal("block", expense.Body.GetProperty("severity").GetString());
        Assert.False(expense.Body.GetProperty("requiresConfirmation").GetBoolean());
        Assert.Equal(0, await Sql("select count(*) from \"Expenses\" where \"Type\" = 'Salary advance'"));
        s.AssertHealthy();
    }
}
