using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using GoatFarm.Application.ViewModels.Assistant;
using Jint;

namespace GoatFarm.Tests.Assistant;

public class AssistantUiTests
{
    [Theory]
    [InlineData("/", true)]
    [InlineData("/Milk", true)]
    [InlineData("/Products", true)]
    [InlineData("/Products?month=2026-09", true)]
    [InlineData("/Finance?month=2026-09", true)]
    [InlineData("/Reports?period=lastmonth", true)]
    [InlineData("/Reports?period=custom&from=2026-09-01&to=2026-09-30&groupBy=day", true)]
    [InlineData("/Goat", true)]
    [InlineData("/Search?tag=G1", true)]
    [InlineData("https://evil.example/Milk", false)]
    [InlineData("//evil.example", false)]
    [InlineData("/Settings", false)]
    [InlineData("/Trading", false)]
    [InlineData("/Projects", false)]
    [InlineData("/Admin", false)]
    [InlineData("/BackupCenter", false)]
    [InlineData("/Account/Login", false)]
    [InlineData("javascript:alert(1)", false)]
    [InlineData("/Finance?url=https://evil.example", false)]
    [InlineData("/Milk/../Settings", false)]
    public void AssistantSafePath_AllowsOnlyCatalogRoutes(string path, bool expected)
    {
        var js = ShippedHelpers();
        Assert.Equal(expected, js.Invoke("assistantSafePath", path).AsBoolean());
    }

    [Fact]
    public void ShippedScript_RendersAssistantBlocksWithTextContent_NotRawInnerHtml()
    {
        var source = File.ReadAllText(GoatRecordsJsPath());
        var start = source.IndexOf("function initAssistant()", StringComparison.Ordinal);
        Assert.True(start >= 0, "initAssistant is missing from goat-records.js.");
        var end = source.IndexOf("\n  return { initHerd", start, StringComparison.Ordinal);
        Assert.True(end > start, "Could not isolate initAssistant.");
        var fn = source[start..end];

        Assert.Contains("assistantBound", fn);
        Assert.Contains("/Assistant/Chat", fn);
        Assert.Contains("conversationId", fn);
        Assert.Contains("quiet: true", fn);
        Assert.Contains("AbortController", fn);
        Assert.Contains("el.textContent = text", fn);
        Assert.Contains("res.status === 429", source);
        Assert.Contains("kind === 'summary'", fn);
        Assert.Contains("kind === 'table'", fn);
        Assert.Contains("kind === 'chart'", fn);
        Assert.Contains("kind === 'navigate'", fn);
        Assert.Contains("kind === 'clarify'", fn);
        Assert.Contains("kind === 'error'", fn);
        Assert.Contains("assistantSafePath", fn);
        Assert.DoesNotContain("innerHTML", fn);
        Assert.DoesNotContain("AI Assistant is not available yet", source);
        Assert.DoesNotContain("unpaid invoice", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Show unpaid invoices", source, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Layout_WiresAssistantPanelAndKeepsCsrfMeta()
    {
        var root = FindRepoRoot();
        var layout = File.ReadAllText(Path.Combine(root, "src", "GoatFarm.Web", "Views", "Shared", "_Layout.cshtml"));
        var panel = File.ReadAllText(Path.Combine(root, "src", "GoatFarm.Web", "Views", "Shared", "_AssistantPanel.cshtml"));
        var css = File.ReadAllText(Path.Combine(root, "src", "GoatFarm.Web", "wwwroot", "css", "farm-pro.css"));
        Assert.Contains("_AssistantPanel", layout);
        Assert.Contains("csrf-token", layout);
        Assert.Contains("X-CSRF-TOKEN", File.ReadAllText(Path.Combine(root, "src", "GoatFarm.Web", "wwwroot", "js", "goat-records.js")));
        Assert.Contains("Show milk production for last month.", panel);
        Assert.Contains("What were my total expenses last month?", panel);
        Assert.Contains("Show my profit and loss for September 2026.", panel);
        Assert.Contains("Show the latest dashboard summary.", panel);
        Assert.Contains("Trading, projects, and product stock are not in this assistant yet.", panel);
        Assert.DoesNotContain("invoice", panel, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(".ai-panel[hidden]", css);
        Assert.Contains("initAssistant();", File.ReadAllText(Path.Combine(root, "src", "GoatFarm.Web", "wwwroot", "js", "goat-records.js")));
    }

    [Fact]
    public void AssistantTurnResponse_SerializesCamelCaseStringEnums()
    {
        var json = JsonSerializer.Serialize(new AssistantTurnResponse
        {
            ConversationId = "conv-1",
            Blocks =
            [
                new AssistantBlock { Kind = AssistantBlockKind.Text, Title = "Note", Text = "Hello" },
                new AssistantBlock { Kind = AssistantBlockKind.Error, Text = "Nope", ErrorCode = AssistantErrorCodes.NotConfigured }
            ]
        }, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Converters = { new JsonStringEnumConverter() }
        });

        Assert.Contains("\"conversationId\":\"conv-1\"", json);
        Assert.Contains("\"kind\":\"Text\"", json);
        Assert.Contains("\"kind\":\"Error\"", json);
        Assert.Contains("\"errorCode\":\"not_configured\"", json);
        Assert.Contains("\"title\":\"Note\"", json);
    }

    [Theory]
    [InlineData("<img src=x onerror=alert(1)>", "&lt;img src=x onerror=alert(1)&gt;")]
    [InlineData("<script>alert(1)</script>", "&lt;script&gt;alert(1)&lt;/script&gt;")]
    public void Esc_StillNeutralisesHostileAssistantText(string input, string expected) =>
        Assert.Equal(expected, ShippedHelpers().Invoke("esc", input).AsString());

    private static Engine ShippedHelpers()
    {
        var source = File.ReadAllText(GoatRecordsJsPath());
        var engine = new Engine();
        foreach (var name in new[] { "esc", "assistantSafePath" })
        {
            var match = Regex.Match(source, $@"^\s*const {name} = .*;\s*$", RegexOptions.Multiline);
            Assert.True(match.Success, $"goat-records.js no longer defines '{name}' on a single line.");
            engine.Execute(match.Value.Trim().Replace($"const {name} =", $"var {name} ="));
        }
        return engine;
    }

    private static string GoatRecordsJsPath() =>
        Path.Combine(FindRepoRoot(), "src", "GoatFarm.Web", "wwwroot", "js", "goat-records.js");

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "GoatFarm.slnx")))
            dir = dir.Parent;
        Assert.NotNull(dir);
        return dir!.FullName;
    }
}
