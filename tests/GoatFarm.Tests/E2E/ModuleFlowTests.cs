using System.Text.Json;
using System.Text.RegularExpressions;
using GoatFarm.Infrastructure.Identity;
using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace GoatFarm.Tests.E2E;

/// <summary>Finance visibility, salary safeguards, date of birth and the Feed, Milk, Vaccines/Health and Reports modules.</summary>
[Collection(E2eCollection.Name)]
[Trait("Category", "E2E")]
public class ModuleFlowTests
{
    private const string StrongPassword = "Str0ng!Passw0rd";
    private const string Hostile = "<img src=x onerror=\"window.__xss=1\"> اردو";

    private readonly E2eApp _app;

    public ModuleFlowTests(E2eApp app) => _app = app;

    private static string Today(int offset = 0) => DateOnly.FromDateTime(DateTime.Today).AddDays(offset).ToString("yyyy-MM-dd");
    private static string Uid() => Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();
    private Task<long> Sql(string sql) => _app.Db.ScalarAsync(sql);

    private async Task<BrowserSession> AdminAsync()
    {
        Skip.If(_app.SkipReason is not null, _app.SkipReason);
        var s = await BrowserSession.LoginAsync(_app, IdentitySeedHelper.DefaultAdminEmail, IdentitySeedHelper.DefaultAdminPassword);
        s.Page.Dialog += (_, d) => _ = d.DismissAsync();
        return s;
    }

    private static async Task<string> CreateUserAsync(BrowserSession admin, string role, object? permissions = null)
    {
        var id = Uid().ToLowerInvariant();
        var email = $"{role.ToLowerInvariant()}{id}@farm.test";
        var user = await admin.ApiOkAsync("POST", "/Settings/CreateUser", new { fullName = $"{role} {id}", email, password = StrongPassword, role });
        if (permissions is not null)
            await admin.ApiOkAsync("PUT", $"/Settings/SaveUserPermissions?id={user.GetProperty("id").GetString()}",
                new { usesRolePermissions = false, permissions });
        return email;
    }

    private static async Task AssertNoScriptRanAsync(IPage page) =>
        Assert.True(await page.EvaluateAsync<bool>("() => window.__xss === undefined"), "Stored text was executed as HTML.");

    private static async Task<string> DownloadCsvAsync(IPage page)
    {
        await Expect(page.Locator("#dashKpi .stat").First).ToBeAttachedAsync();
        var download = await page.RunAndWaitForDownloadAsync(() => page.ClickAsync("#repCsv"));
        return await File.ReadAllTextAsync((await download.PathAsync())!);
    }

    // 13
    [SkippableFact]
    public async Task FinanceVisibility_StaffSeesNoMoneyAnywhere_ManagerKeepsFinance()
    {
        await using var admin = await AdminAsync();
        await admin.Page.GotoAsync("/Finance");
        var incomeType = (await admin.ApiOkAsync("GET", "/Lookup/Get?key=Lookup.IncomeTypes"))[0].GetString();
        await admin.ApiOkAsync("POST", "/Finance/AddIncome", new { type = incomeType, amount = 123456, date = Today() });
        var staffEmail = await CreateUserAsync(admin, "Staff");
        var managerEmail = await CreateUserAsync(admin, "Manager");

        await using (var staff = await BrowserSession.LoginAsync(_app, staffEmail, StrongPassword))
        {
            staff.ExpectStatuses(403);
            var page = staff.Page;
            await page.GotoAsync("/Dashboard");
            await Expect(page.Locator(".dash-panel h2", new() { HasText = "Herd" })).ToBeVisibleAsync();
            await Expect(page.Locator("#dashFinancePanel")).ToHaveCountAsync(0);

            var stats = await staff.ApiOkAsync("GET", "/Dashboard/Stats");
            Assert.True(stats.GetProperty("financeHidden").GetBoolean());
            foreach (var money in new[] { "totalIncome", "totalExpense", "capital", "profit" })
                Assert.Equal(0m, stats.GetProperty(money).GetDecimal());

            var report = await staff.ApiOkAsync("GET", "/Reports/GetData");
            Assert.True(report.GetProperty("financeHidden").GetBoolean());
            Assert.DoesNotContain(report.GetProperty("dashboard").GetProperty("kpis").EnumerateArray(), k => k.GetProperty("isFinancial").GetBoolean());
            Assert.DoesNotContain(report.GetProperty("comparison").GetProperty("rows").EnumerateArray(), r => r.GetProperty("isFinancial").GetBoolean());
            var finance = report.GetProperty("finance");
            Assert.Equal(0m, finance.GetProperty("totalIncome").GetDecimal());
            Assert.Empty(finance.GetProperty("incomeSources").EnumerateArray());
            Assert.Empty(finance.GetProperty("expenseEntries").EnumerateArray());
            Assert.DoesNotContain("123,456", report.GetRawText());

            await page.GotoAsync("/Reports");
            await Expect(page.Locator("[data-reptab=herd]")).ToBeVisibleAsync();
            await Expect(page.Locator("[data-reptab=finance], [data-reptab=profit], #rep-finance")).ToHaveCountAsync(0);
            var csv = await DownloadCsvAsync(page);
            Assert.Contains("KEY FIGURES", csv);
            foreach (var hidden in new[] { "Revenue", "Net profit", "INCOME VS EXPENSES", "EXPENSES BY CATEGORY", "123,456" })
                Assert.DoesNotContain(hidden, csv);

            Assert.Equal(403, (await staff.ApiAsync("GET", "/Finance/GetData")).Status);
            Assert.Equal(403, (await staff.ApiAsync("GET", "/Finance/SalaryReconciliation")).Status);
            await page.GotoAsync("/Finance");
            await page.WaitForURLAsync(new Regex("AccessDenied", RegexOptions.IgnoreCase));
            staff.AssertHealthy();
        }

        await using (var manager = await BrowserSession.LoginAsync(_app, managerEmail, StrongPassword))
        {
            var page = manager.Page;
            await page.GotoAsync("/Dashboard");
            await Expect(page.Locator("#dashFinancePanel")).ToBeVisibleAsync();
            Assert.False((await manager.ApiOkAsync("GET", "/Dashboard/Stats")).GetProperty("financeHidden").GetBoolean());
            var report = await manager.ApiOkAsync("GET", "/Reports/GetData");
            Assert.False(report.GetProperty("financeHidden").GetBoolean());
            Assert.Contains(report.GetProperty("dashboard").GetProperty("kpis").EnumerateArray(), k => k.GetProperty("isFinancial").GetBoolean());
            await page.GotoAsync("/Reports");
            await Expect(page.Locator("[data-reptab=finance]")).ToBeVisibleAsync();
            Assert.Contains("Revenue", await DownloadCsvAsync(page));
            await manager.ApiOkAsync("GET", "/Finance/SalaryReconciliation");
            manager.AssertHealthy();
        }
        admin.AssertHealthy();
    }

    // 14
    [SkippableFact]
    public async Task Salary_AdvanceAndSalaryCostsBlockedEverywhere_LegacyRowsShownForReview()
    {
        await using var s = await AdminAsync();
        var page = s.Page;
        var id = Uid();
        var worker = "Worker" + id;
        s.ExpectStatuses(400, 409);

        await page.GotoAsync("/Finance");
        await page.FillAsync("#emp-name", worker);
        await page.FillAsync("#emp-salary", "25000");
        await page.ClickAsync("#addEmp");
        await Expect(page.Locator("#toastHost")).ToContainTextAsync("Employee added");

        await _app.Db.ExecuteAsync($"""
            INSERT INTO "Expenses" ("Type","Amount","Date","Comment","CreatedDate","UpdatedDate")
            VALUES ('Staff advance', 7000, '{Today()}', '{worker} March', now(), now()),
                   ('Fencing', 1500, '{Today()}', 'tankhwah {id}', now(), now())
            """);
        await page.ReloadAsync();

        var options = await page.Locator("#e-type option").AllInnerTextsAsync();
        Assert.DoesNotContain(options, o => o.Contains("salary", StringComparison.OrdinalIgnoreCase) || o.Contains("advance", StringComparison.OrdinalIgnoreCase));

        var recon = page.Locator("#salaryReconciliation");
        await Expect(recon).ToContainTextAsync("nothing is changed automatically");
        await Expect(recon.Locator("tr[data-outcome=ConfirmedDoubleCount]", new() { HasText = worker })).ToContainTextAsync("Counted twice");
        await Expect(recon.Locator("tr[data-outcome=NeedsReview]", new() { HasText = "tankhwah " + id })).ToContainTextAsync("Needs review");
        var legacyRow = page.Locator("#expenseRows tr.fin-expense-row", new() { HasText = worker + " March" });
        await Expect(legacyRow.Locator(".chip-legacy")).ToBeVisibleAsync();

        await legacyRow.ClickAsync();
        Assert.Equal("Staff advance", await page.InputValueAsync("#e-type"));
        await Expect(page.Locator("#e-type option:checked")).ToHaveTextAsync("Staff advance (no longer accepted)");
        await page.FillAsync("#e-amt", "9000");
        await page.ClickAsync("#addExpense");
        await s.CloseModalAsync("counted automatically");
        Assert.Equal(7000, await Sql($"select \"Amount\"::bigint from \"Expenses\" where \"Comment\" = '{worker} March'"));
        await page.ClickAsync("#cancelExpenseBtn");
        await Expect(page.Locator("#e-type option[data-retired]")).ToHaveCountAsync(0);

        await page.FillAsync("#rc-name", "Chowkidar salary " + id);
        await page.FillAsync("#rc-amt", "20000");
        await page.ClickAsync("#addRecur");
        await s.CloseModalAsync("counted twice");
        Assert.Equal(0, await Sql($"select count(*) from \"RecurringCosts\" where \"Name\" like '%{id}'"));

        foreach (var type in new[] { "Salary advance", "Staff wages", "پیشگی تنخواہ" })
            Assert.Equal(409, (await s.ApiAsync("POST", "/Finance/AddExpense", new { type, amount = 100, date = Today(), confirmPossibleDuplicate = true })).Status);
        Assert.Equal(400, (await s.ApiAsync("POST", "/Lookup/AddOption", new { key = "Lookup.ExpenseTypes", value = "Labour advance" })).Status);
        Assert.Equal(2, await Sql($"select count(*) from \"Expenses\" where \"Comment\" like '%{id}%'"));

        var json = await s.ApiOkAsync("GET", "/Finance/SalaryReconciliation");
        Assert.Contains(json.GetProperty("rows").EnumerateArray(), r => r.GetProperty("matchedEmployee").GetString() == worker);
        s.AssertHealthy();
    }

    // 15
    [SkippableFact]
    public async Task FinanceExpense_ValidatesPersistsAfterReloadAndShowsTextSafely()
    {
        await using var s = await AdminAsync();
        var page = s.Page;
        var id = Uid();
        s.ExpectStatuses(400);
        await page.GotoAsync("/Finance");

        await page.ClickAsync("#addExpense");
        await s.CloseModalAsync("Enter date and amount");
        Assert.Equal(400, (await s.ApiAsync("POST", "/Finance/AddExpense", new { type = "Fencing", amount = -5, date = Today() })).Status);

        await page.SelectOptionAsync("#e-type", new SelectOptionValue { Index = 0 });
        await page.FillAsync("#e-amt", "2500");
        await page.FillAsync("#e-comment", Hostile + " " + id);
        await page.ClickAsync("#addExpense");
        await Expect(page.Locator("#toastHost")).ToContainTextAsync("Running cost added");
        await page.ReloadAsync();
        var row = page.Locator("#expenseRows tr.fin-expense-row", new() { HasText = id });
        await Expect(row).ToContainTextAsync("<img src=x");
        await AssertNoScriptRanAsync(page);
        Assert.Equal(1, await Sql($"select count(*) from \"Expenses\" where \"Comment\" like '%{id}' and \"Amount\" = 2500"));
        s.AssertHealthy();
    }

    // 16
    [SkippableFact]
    public async Task Milk_CollectionAndSale_ValidatePersistAndRespectViewOnlyAccess()
    {
        await using var s = await AdminAsync();
        var page = s.Page;
        var id = Uid();
        s.ExpectStatuses(400);
        await page.GotoAsync("/Milk");

        await page.ClickAsync("#addProd");
        await s.CloseModalAsync("Enter date and litres");
        Assert.Equal(400, (await s.ApiAsync("POST", "/Milk/AddProduction", new { date = Today(), breed = "Beetal", liters = -3 })).Status);

        await page.FillAsync("#p-liters", "12.5");
        await page.FillAsync("#p-note", Hostile + " " + id);
        await page.ClickAsync("#addProd");
        await Expect(page.Locator("#toastHost")).ToContainTextAsync("Milk collection added");

        await page.FillAsync("#s-liters", "10");
        await page.FillAsync("#s-rate", "180");
        await page.FillAsync("#s-note", "sale " + id);
        await page.ClickAsync("#addSale");
        await Expect(page.Locator("#toastHost")).ToContainTextAsync("Milk sale added");

        await page.ReloadAsync();
        await Expect(page.Locator("#prodRows tr.milk-prod-row", new() { HasText = id })).ToContainTextAsync("<img src=x");
        await AssertNoScriptRanAsync(page);
        Assert.Equal(1, await Sql($"select count(*) from \"MilkProductions\" where \"Comment\" like '%{id}' and \"Liters\" = 12.5"));
        Assert.Equal(1800, await Sql($"select \"Amount\"::bigint from \"MilkSales\" where \"Comment\" = 'sale {id}'"));

        var viewer = await CreateUserAsync(s, "Staff", new Dictionary<string, object>
        {
            ["dashboard"] = new { view = true }, ["milk"] = new { view = true }
        });
        await using (var v = await BrowserSession.LoginAsync(_app, viewer, StrongPassword))
        {
            v.ExpectStatuses(403);
            await v.Page.GotoAsync("/Milk");
            await Expect(v.Page.Locator("#prodRows tr.milk-prod-row", new() { HasText = id })).ToBeVisibleAsync();
            await Expect(v.Page.Locator("#addProd")).ToBeHiddenAsync();
            Assert.Equal(403, (await v.ApiAsync("POST", "/Milk/AddProduction", new { date = Today(), breed = "Beetal", liters = 5 })).Status);
            Assert.Equal(403, (await v.ApiAsync("GET", "/Feed/GetData")).Status);
            v.AssertHealthy();
        }
        Assert.Equal(1, await Sql($"select count(*) from \"MilkProductions\" where \"Comment\" like '%{id}'"));
        s.AssertHealthy();
    }

    // 17
    [SkippableFact]
    public async Task Feed_PurchaseAddsStock_RejectsBadInput_ShowsEmptyMonth()
    {
        await using var s = await AdminAsync();
        var page = s.Page;
        var id = Uid();
        s.ExpectStatuses(400, 403);
        await page.GotoAsync("/Feed");

        var feed = (await page.Locator("#fb-feed option").First.GetAttributeAsync("value"))!;
        var stockBefore = await Sql($"select coalesce(\"StockKg\", 0)::bigint from \"FeedPrices\" where \"FeedType\" = '{feed}'");

        await page.ClickAsync("#addFeedBuy");
        await s.CloseModalAsync("Enter date, feed, qty and rate");
        foreach (var bad in new object[]
                 {
                     new { date = Today(), feedType = feed, kg = -50, ratePerKg = 80 },
                     new { date = Today(), feedType = feed, kg = 50, ratePerKg = 0 },
                     new { date = Today(), feedType = "no-such-feed-" + id, kg = 50, ratePerKg = 80 }
                 })
            Assert.Equal(400, (await s.ApiAsync("POST", "/Feed/AddPurchase", bad)).Status);

        await page.SelectOptionAsync("#fb-feed", feed);
        await page.FillAsync("#fb-kg", "40");
        await page.FillAsync("#fb-rate", "95");
        await page.FillAsync("#fb-note", Hostile + " " + id);
        await page.ClickAsync("#addFeedBuy");
        await Expect(page.Locator("#toastHost")).ToContainTextAsync("Feed purchase added");
        await page.ReloadAsync();
        await Expect(page.Locator("#buyLogRows tr.feed-buy-row", new() { HasText = id })).ToContainTextAsync("<img src=x");
        await AssertNoScriptRanAsync(page);
        Assert.Equal(3800, await Sql($"select \"Amount\"::bigint from \"FeedPurchases\" where \"Comment\" like '%{id}'"));
        Assert.Equal(stockBefore + 40, await Sql($"select \"StockKg\"::bigint from \"FeedPrices\" where \"FeedType\" = '{feed}'"));
        Assert.Equal(1, await Sql($"select count(*) from \"FeedPurchases\" where \"Comment\" like '%{id}'"));
        Assert.Equal(0, await Sql("select count(*) from \"FeedPurchases\" where \"FeedType\" like 'no-such-feed-%' or \"Kg\" <= 0 or \"RatePerKg\" <= 0"));

        await page.GotoAsync("/Feed?month=2001-01");
        await Expect(page.Locator("#buyLogRows")).ToContainTextAsync("No feed purchases logged this month.");
        s.AssertHealthy();
    }

    // 18
    [SkippableFact]
    public async Task VaccinesAndHealth_PurchaseScheduleAndSickGoat()
    {
        await using var s = await AdminAsync();
        var page = s.Page;
        var id = Uid();
        s.ExpectStatuses(400);
        await page.GotoAsync("/Vaccine");
        await Expect(page.Locator("#dueList")).ToBeAttachedAsync();

        await page.ClickAsync("#addVaccBuy");
        await s.CloseModalAsync("Enter date, vaccine, qty, unit and amount");
        var name = (await page.Locator("#vb-name option").First.GetAttributeAsync("value")) ?? (await page.Locator("#vb-name option").First.InnerTextAsync());
        Assert.Equal(400, (await s.ApiAsync("POST", "/Vaccine/AddPurchase", new { date = Today(), name, qty = -1, unit = "Box", amount = 100 })).Status);

        await page.SelectOptionAsync("#vb-name", new SelectOptionValue { Index = 0 });
        await page.FillAsync("#vb-qty", "2");
        await page.FillAsync("#vb-amt", "1500");
        await page.FillAsync("#vb-note", Hostile + " " + id);
        await page.ClickAsync("#addVaccBuy");
        await Expect(page.Locator("#toastHost")).ToContainTextAsync("Vaccine purchase added");
        Assert.Equal(1, await Sql($"select count(*) from \"VaccinePurchases\" where \"Comment\" like '%{id}' and \"Amount\" = 1500"));
        await page.ReloadAsync();
        await Expect(page.Locator("#vaccBuyRows tr.vacc-buy-row", new() { HasText = id })).ToContainTextAsync("<img src=x");
        await AssertNoScriptRanAsync(page);

        var tag = "SICK" + id;
        await s.CreateGoatAsync(tag);
        await page.GotoAsync($"/Goat?filter=all&q={tag}");
        await page.Locator("#rows tr.goat-row", new() { HasText = tag }).ClickAsync();
        await page.SelectOptionAsync("#f-health", "Sick");
        await page.ClickAsync("#addBtn");
        await Expect(page.Locator("#toastHost")).ToContainTextAsync("Goat updated");
        await page.GotoAsync($"/Goat?filter=health:sick&q={tag}");
        await Expect(page.Locator("#rows tr.goat-row")).ToHaveCountAsync(1);
        await Expect(page.Locator("#rows tr.goat-row .chip-urgent")).ToHaveTextAsync("Sick");
        s.AssertHealthy();
    }

    // 19
    [SkippableFact]
    public async Task ReportsCsv_EscapesFormulaText_AndLoadsEveryTab()
    {
        await using var s = await AdminAsync();
        var page = s.Page;
        var tag = "@SUM" + Uid();
        await s.CreateGoatAsync(tag, ageDays: 0, status: "Kid");
        await page.GotoAsync("/Reports");
        foreach (var tab in new[] { "herd", "breeding", "growth", "feed", "health", "finance", "profit", "inventory", "alerts", "compare" })
        {
            var button = page.Locator($"[data-reptab={tab}]");
            if (await button.CountAsync() == 0) continue;
            await button.ClickAsync();
            await Expect(page.Locator($"#rep-{tab}")).ToBeVisibleAsync();
        }
        var csv = await DownloadCsvAsync(page);
        Assert.Contains("'" + tag, csv);
        Assert.DoesNotContain("," + tag, csv);
        Assert.Contains("INCOME VS EXPENSES", csv);
        s.AssertHealthy();
    }

    // 20
    [SkippableFact]
    public async Task DateOfBirth_OptionalForBoughtGoats_ValidatedAndUsedForBreedingChecks()
    {
        await using var s = await AdminAsync();
        var page = s.Page;
        var id = Uid();
        s.ExpectStatuses(400, 409);
        await page.GotoAsync("/Goat");

        await page.SelectOptionAsync("#f-source", "Born");
        await Expect(page.Locator("#f-dob")).ToBeDisabledAsync();
        await page.SelectOptionAsync("#f-source", "Bought");
        await Expect(page.Locator("#f-dob")).ToBeEnabledAsync();

        await page.FillAsync("#f-tag", "EST" + id);
        await page.FillAsync("#f-price", "30000");
        await page.SelectOptionAsync("#f-status", "Dry");
        await page.FillAsync("#f-date", Today(-10));
        await page.FillAsync("#f-dob", Today(-400));
        await page.CheckAsync("#f-dob-est");
        await page.ClickAsync("#addBtn");
        await Expect(page.Locator("#toastHost")).ToContainTextAsync("Goat added");
        await page.GotoAsync($"/Goat?filter=all&q=EST{id}");
        var row = page.Locator("#rows tr.goat-row", new() { HasText = "EST" + id });
        await Expect(row).ToContainTextAsync("~400");
        await Expect(row).ToContainTextAsync("estimated");
        Assert.Equal(1, await Sql($"select count(*) from \"Goats\" where \"Tag\" = 'EST{id}' and \"DateOfBirth\" = '{Today(-400)}' and \"DateOfBirthIsEstimated\""));

        await row.ClickAsync();
        await Expect(page.Locator("#f-dob")).ToHaveValueAsync(Today(-400));
        await Expect(page.Locator("#f-dob-est")).ToBeCheckedAsync();
        await page.FillAsync("#f-dob", Today(5));
        await page.ClickAsync("#addBtn");
        await s.CloseModalAsync("cannot be in the future");
        Assert.Equal(1, await Sql($"select count(*) from \"Goats\" where \"Tag\" = 'EST{id}' and \"DateOfBirth\" = '{Today(-400)}'"));

        await s.CreateGoatAsync("UNK" + id, source: "Bought", ageDays: 30);
        await s.CreateGoatAsync("BUCK" + id, gender: "Male", status: "Buck");
        await page.GotoAsync($"/Goat?filter=all&q=UNK{id}");
        await Expect(page.Locator("#rows tr.goat-row", new() { HasText = "UNK" + id })).ToContainTextAsync("age unknown");
        await page.GotoAsync($"/Goat?filter=age:0:70&q=UNK{id}");
        await Expect(page.Locator("#rows tr.goat-row")).ToHaveCountAsync(0);

        await page.GotoAsync("/Breeding");
        await page.FillAsync("#bc-tag", "UNK" + id);
        await page.FillAsync("#bc-buck", "BUCK" + id);
        await page.ClickAsync("#checkCross");
        await Expect(page.Locator("#bc-check .cross-warnings")).ToContainTextAsync("age is unknown");
        await page.FillAsync("#bc-date", Today());
        await page.ClickAsync("#addCross");
        await Expect(page.Locator("#appModalMsg")).ToContainTextAsync("age is unknown");
        await page.ClickAsync("#appModalCancel");
        var crossCount = $"select count(*) from \"BreedingEvents\" e join \"Goats\" g on g.\"Id\" = e.\"DoeId\" where g.\"Tag\" = 'UNK{id}'";
        Assert.Equal(0, await Sql(crossCount));

        await page.ClickAsync("#addCross");
        await Expect(page.Locator("#appModalMsg")).ToContainTextAsync("Save anyway?");
        await page.ClickAsync("#appModalOk");
        await EventuallyAsync(async () => await Sql(crossCount) == 1, "the confirmed cross was not saved");
        s.AssertHealthy();
    }

    private static async Task EventuallyAsync(Func<Task<bool>> condition, string failure)
    {
        for (var i = 0; i < 50; i++)
        {
            if (await condition()) return;
            await Task.Delay(200);
        }
        Assert.Fail(failure);
    }
}
