using System.Text.Json;
using System.Text.RegularExpressions;
using GoatFarm.Infrastructure.Identity;
using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace GoatFarm.Tests.E2E;

[Collection(E2eCollection.Name)]
[Trait("Category", "E2E")]
public class AssistantFlowTests
{
    private readonly E2eApp _app;

    public AssistantFlowTests(E2eApp app) => _app = app;

    [SkippableFact]
    public async Task AssistantPanel_OpensCloses_PostsChat_RendersBlocksSafely()
    {
        Skip.If(_app.SkipReason is not null, _app.SkipReason);
        await using var s = await BrowserSession.LoginAsync(
            _app, IdentitySeedHelper.DefaultAdminEmail, IdentitySeedHelper.DefaultAdminPassword);
        var page = s.Page;

        await page.GotoAsync("/Dashboard");
        await Expect(page.Locator("#sideAiAssistantBtn")).ToBeVisibleAsync();
        await Expect(page.Locator("#aiAssistantPanel")).ToBeHiddenAsync();

        await page.ClickAsync("#sideAiAssistantBtn");
        await Expect(page.Locator("#aiAssistantPanel")).ToBeVisibleAsync();
        await Expect(page.Locator("body")).ToHaveClassAsync(new Regex("ai-open"));
        await Expect(page.Locator("#aiAssistantInput")).ToBeFocusedAsync();

        await page.Keyboard.PressAsync("Escape");
        await Expect(page.Locator("#aiAssistantPanel")).ToBeHiddenAsync();
        await Expect(page.Locator("#sideAiAssistantBtn")).ToBeFocusedAsync();

        var live = await s.ApiOkAsync("POST", "/Assistant/Chat", new
        {
            message = "Show the latest dashboard summary.",
            timeZone = "Asia/Karachi"
        });
        Assert.False(string.IsNullOrWhiteSpace(live.GetProperty("conversationId").GetString()));
        Assert.True(live.TryGetProperty("blocks", out var liveBlocks) && liveBlocks.GetArrayLength() > 0);

        await page.ClickAsync("#sideAiAssistantBtn");
        await page.RouteAsync("**/Assistant/Chat", RouteAllBlocksAsync);
        await page.FillAsync("#aiAssistantInput", "Show milk production for last month.");
        await page.ClickAsync("#aiAssistantSend");

        await Expect(page.Locator(".ai-bubble-user")).ToContainTextAsync("Show milk production for last month.");
        await Expect(page.Locator(".ai-block-title").First).ToContainTextAsync("Milk last month");
        await Expect(page.Locator(".ai-kpis .k")).ToContainTextAsync("Total litres");
        await Expect(page.Locator(".ai-table-wrap th")).ToContainTextAsync("Date");
        await Expect(page.Locator(".dash-bar-lbl")).ToContainTextAsync("Week 1");
        await Expect(page.Locator("a.ai-nav")).ToHaveAttributeAsync("href", "/Milk");
        await Expect(page.Locator(".ai-clarify-qs button")).ToContainTextAsync("Use last month?");
        await Expect(page.Locator(".ai-retry")).ToBeVisibleAsync();

        var thread = page.Locator("#aiAssistantThread");
        await Expect(thread).ToContainTextAsync("<img src=x onerror=alert(1)>");
        Assert.Equal(0, await thread.Locator("img").CountAsync());
        Assert.Equal(0, await thread.Locator("script").CountAsync());
        Assert.Equal(0, await thread.Locator("a[href*='javascript']").CountAsync());
        Assert.Equal(0, await thread.Locator("a[href*='evil']").CountAsync());

        var storedId = await page.EvaluateAsync<string>("() => sessionStorage.getItem('goatAiConv')");
        Assert.Equal("e2e-conv-1", storedId);

        await page.UnrouteAsync("**/Assistant/Chat");
        var secondBody = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        await page.RouteAsync("**/Assistant/Chat", async route =>
        {
            secondBody.TrySetResult(route.Request.PostData ?? "");
            await route.FulfillAsync(new()
            {
                Status = 200,
                ContentType = "application/json",
                Body = """{"conversationId":"e2e-conv-1","blocks":[{"kind":"Text","text":"Follow-up received."}]}"""
            });
        });
        await page.FillAsync("#aiAssistantInput", "Show my profit and loss for September 2026.");
        await page.ClickAsync("#aiAssistantSend");
        await Expect(thread).ToContainTextAsync("Follow-up received.");
        var posted = await secondBody.Task.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Contains("e2e-conv-1", posted);
        Assert.Contains("conversationId", posted);

        await page.UnrouteAsync("**/Assistant/Chat");
        s.ExpectStatuses(403);
        await page.RouteAsync("**/Assistant/Chat", route => route.FulfillAsync(new()
        {
            Status = 403,
            ContentType = "application/json",
            Body = """{"error":"You do not have permission for this action."}"""
        }));
        await page.FillAsync("#aiAssistantInput", "What were my total expenses last month?");
        await page.ClickAsync("#aiAssistantSend");
        await Expect(page.Locator(".ai-bubble-error").Last).ToContainTextAsync("You do not have permission for this action.");
        await Expect(page.Locator("#appModal")).Not.ToHaveClassAsync(new Regex("open"));
        await Expect(page.Locator("#aiAssistantSend")).ToBeEnabledAsync();

        await page.ClickAsync("#aiAssistantClose");
        await page.GotoAsync("/Goat");
        await Expect(page.Locator("#filter")).ToBeVisibleAsync();
        await Expect(page.Locator("#sideAiAssistantBtn")).ToBeVisibleAsync();
        s.AssertHealthy();
    }

    private static Task RouteAllBlocksAsync(IRoute route) =>
        route.FulfillAsync(new()
        {
            Status = 200,
            ContentType = "application/json",
            Body = JsonSerializer.Serialize(new
            {
                conversationId = "e2e-conv-1",
                blocks = new object[]
                {
                    new { kind = "Text", title = "Milk last month", text = "Herd milk was recorded. <img src=x onerror=alert(1)>" },
                    new
                    {
                        kind = "Summary",
                        summary = new { items = new[] { new { label = "Total litres", value = "120 L", note = "Last month" } } }
                    },
                    new
                    {
                        kind = "Table",
                        table = new { columns = new[] { "Date", "Litres" }, rows = new[] { new[] { "2026-09-01", "12" } } }
                    },
                    new
                    {
                        kind = "Chart",
                        chart = new { chartType = "bar", points = new[] { new { label = "Week 1", value = 40 } } }
                    },
                    new { kind = "Navigate", navigation = new { label = "Open Milk", path = "/Milk" } },
                    new { kind = "Navigate", navigation = new { label = "Evil", path = "javascript:alert(1)" } },
                    new { kind = "Navigate", navigation = new { label = "Offsite", path = "https://evil.example/Milk" } },
                    new { kind = "Clarify", clarification = new { questions = new[] { "Use last month?" } } },
                    new { kind = "Error", title = "Could not answer", text = "Sample error", errorCode = "provider_error" }
                }
            })
        });
}
