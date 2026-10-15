using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Playwright;

namespace GoatFarm.Tests.E2E;

/// <summary>One browser context (cookie jar) plus a monitor for console errors, uncaught page errors,
/// failed same-origin requests, HTTP 5xx responses and server-side log failures.</summary>
public sealed class BrowserSession : IAsyncDisposable
{
    private static readonly Regex FailedResource = new(@"status of (\d{3})", RegexOptions.Compiled);

    private readonly E2eApp _app;
    private readonly int _logStart;
    private readonly List<string> _problems = [];
    private readonly HashSet<int> _expectedStatuses = [];
    private readonly List<string> _expectedPageErrors = [];
    private readonly List<string> _expectedLogCategories = [];

    public IBrowserContext Context { get; }
    public IPage Page { get; }
    public List<string> RequestBodies { get; } = [];

    private BrowserSession(E2eApp app, IBrowserContext context, IPage page)
    {
        _app = app;
        Context = context;
        Page = page;
        _logStart = app.LogLines.Length;

        page.Console += (_, msg) =>
        {
            if (msg.Type != "error") return;
            var m = FailedResource.Match(msg.Text);
            if (m.Success && _expectedStatuses.Contains(int.Parse(m.Groups[1].Value))) return;
            if (!IsSameOrigin(msg.Location)) return;
            _problems.Add($"console error: {msg.Text} @ {msg.Location}");
        };
        page.PageError += (_, error) =>
        {
            if (_expectedPageErrors.Any(error.Contains)) return;
            _problems.Add("uncaught page error: " + error);
        };
        page.RequestFailed += (_, req) =>
        {
            if (!IsSameOrigin(req.Url)) return;
            if (req.Failure?.Contains("ERR_ABORTED") == true) return;
            _problems.Add($"request failed: {req.Method} {req.Url} {req.Failure}");
        };
        page.Response += (_, res) =>
        {
            if (IsSameOrigin(res.Url) && res.Status >= 500)
                _problems.Add($"HTTP {res.Status}: {res.Request.Method} {res.Url}");
        };
        page.Request += (_, req) =>
        {
            if (req.Method == "POST" && req.PostData is { } body) RequestBodies.Add(req.Url + "\n" + body);
        };
    }

    public static async Task<BrowserSession> OpenAsync(E2eApp app, int width = 1280, int height = 900)
    {
        var context = await app.Browser.NewContextAsync(new BrowserNewContextOptions
        {
            BaseURL = app.BaseUrl,
            ViewportSize = new ViewportSize { Width = width, Height = height }
        });
        context.SetDefaultTimeout(15000);
        // Third-party assets (Google Fonts) are not part of the app under test; offline they stall the load event.
        await context.RouteAsync(url => !url.StartsWith(app.BaseUrl, StringComparison.OrdinalIgnoreCase), route => route.AbortAsync());
        var page = await context.NewPageAsync();
        return new BrowserSession(app, context, page);
    }

    public static async Task<BrowserSession> LoginAsync(E2eApp app, string email, string password, int width = 1280, int height = 900)
    {
        var s = await OpenAsync(app, width, height);
        await s.SignInAsync(email, password);
        return s;
    }

    public async Task SignInAsync(string email, string password)
    {
        await Page.GotoAsync("/Account/Login");
        await Page.FillAsync("#Email", email);
        await Page.FillAsync("#Password", password);
        await Page.ClickAsync("button.login-submit", new() { Timeout = 60000 });
        await Page.WaitForURLAsync(u => !u.Contains("/Account/Login", StringComparison.OrdinalIgnoreCase), new() { Timeout = 60000 });
    }

    /// <summary>Allow browser "Failed to load resource" console lines for deliberately rejected requests.</summary>
    public void ExpectStatuses(params int[] statuses) => _expectedStatuses.UnionWith(statuses);

    public void ExpectPageError(string fragment) => _expectedPageErrors.Add(fragment);

    /// <summary>Allow `fail:` log lines from these categories (e.g. EF logging a unique-index violation the app handles).</summary>
    public void ExpectServerLogCategories(params string[] categories) => _expectedLogCategories.AddRange(categories);

    /// <summary>Same-origin fetch from inside the signed-in page (the app's fetch wrapper adds the CSRF header).</summary>
    public async Task<(int Status, JsonElement Body)> ApiAsync(string method, string url, object? body = null)
    {
        var json = body switch
        {
            null => null,
            JsonElement el => el.GetRawText(),
            _ => JsonSerializer.Serialize(body)
        };
        var result = await Page.EvaluateAsync<JsonElement>(
            @"async ([method, url, body]) => {
                const r = await fetch(url, { method, headers: { 'Content-Type': 'application/json', 'Accept': 'application/json' },
                                             body: body ?? undefined });
                const text = await r.text();
                let json = null; try { json = JSON.parse(text); } catch { json = text; }
                return { status: r.status, body: json };
            }",
            new object?[] { method, url, json });
        return (result.GetProperty("status").GetInt32(), result.GetProperty("body"));
    }

    public async Task<JsonElement> ApiOkAsync(string method, string url, object? body = null)
    {
        var (status, json) = await ApiAsync(method, url, body);
        Assert.True(status is >= 200 and < 300, $"{method} {url} -> {status}: {json}");
        return json;
    }

    public async Task CreateGoatAsync(string tag, string gender = "Female", string source = "Born", int ageDays = 500,
        string status = "Dry", string? name = null, string? owner = null) =>
        await ApiOkAsync("POST", "/Goat/Create", new
        {
            tag, name, owner, breed = "Beetal", gender, source, purchasePrice = 0,
            eventDate = DateOnly.FromDateTime(DateTime.Today).AddDays(-ageDays).ToString("yyyy-MM-dd"),
            status, healthState = "Healthy"
        });

    public async Task CloseModalAsync(string expectedText)
    {
        await Assertions.Expect(Page.Locator("#appModalMsg")).ToContainTextAsync(expectedText);
        await Page.ClickAsync("#appModalOk");
    }

    /// <summary>Fails the test if anything went wrong in the browser or on the server during this session.</summary>
    public void AssertHealthy()
    {
        var serverFailures = _app.LogLines.Skip(_logStart)
            .Where(l => l.StartsWith("fail:", StringComparison.Ordinal) || l.StartsWith("crit:", StringComparison.Ordinal) ||
                        l.Contains("Unhandled exception", StringComparison.OrdinalIgnoreCase))
            .Where(l => !_expectedLogCategories.Any(c => l.StartsWith("fail: " + c + "[", StringComparison.Ordinal)))
            .ToList();
        var all = _problems.Concat(serverFailures.Select(l => "server log: " + l)).ToList();
        Assert.True(all.Count == 0, "Problems detected:\n" + string.Join("\n", all));
    }

    private bool IsSameOrigin(string? url) =>
        !string.IsNullOrEmpty(url) && url.StartsWith(_app.BaseUrl, StringComparison.OrdinalIgnoreCase);

    public async ValueTask DisposeAsync() => await Context.CloseAsync();
}
