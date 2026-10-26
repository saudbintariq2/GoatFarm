using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using GoatFarm.Tests.Postgres;
using Microsoft.Playwright;

namespace GoatFarm.Tests.E2E;

/// <summary>
/// Starts the real web app (Development, plain HTTP on loopback) against a freshly created, empty PostgreSQL
/// database on the disposable test server, and one headless Edge/Chrome browser shared by all flows.
/// Enabled only when GOATFARM_E2E=1 and GOATFARM_TEST_PG points at a loopback server.
/// </summary>
public sealed class E2eApp : IAsyncLifetime
{
    public const string EnableVar = "GOATFARM_E2E";

    private readonly ConcurrentQueue<string> _log = new();
    private Process? _process;
    private IPlaywright? _playwright;

    public string? SkipReason { get; private set; }
    public string BaseUrl { get; private set; } = string.Empty;
    public PostgresTestDatabase Db { get; private set; } = null!;
    public IBrowser Browser { get; private set; } = null!;
    public string[] LogLines => _log.ToArray();

    public async Task InitializeAsync()
    {
        if (Environment.GetEnvironmentVariable(EnableVar) != "1")
        {
            SkipReason = $"BLOCKED: browser E2E is opt-in. Set {EnableVar}=1 and {PostgresTestDatabase.EnvVar} to a disposable loopback PostgreSQL server.";
            return;
        }
        if (string.IsNullOrWhiteSpace(PostgresTestDatabase.ServerConnectionString))
        {
            SkipReason = $"BLOCKED: set {PostgresTestDatabase.EnvVar} to a disposable loopback PostgreSQL server.";
            return;
        }

        var root = RepoRoot();
        var webDir = Path.Combine(root, "src", "GoatFarm.Web");
        var dll = new[] { "Debug", "Release" }
            .Select(c => Path.Combine(webDir, "bin", c, "net9.0", "GoatFarm.Web.dll"))
            .Where(File.Exists)
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .FirstOrDefault();
        if (dll is null)
        {
            SkipReason = "BLOCKED: GoatFarm.Web is not built. Run `dotnet build GoatFarm.slnx` first.";
            return;
        }

        Db = await PostgresTestDatabase.CreateAsync(migrate: false);
        var port = FreePort();
        BaseUrl = $"http://127.0.0.1:{port}";

        var psi = new ProcessStartInfo("dotnet", $"\"{dll}\"")
        {
            WorkingDirectory = webDir,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        foreach (var key in new[] { "DATABASE_PUBLIC_URL", "DATABASE_URL", "DATABASE_PRIVATE_URL", "PGHOST", "PGDATABASE", "PGUSER", "PGPASSWORD", "PGPORT",
                     "CUSTOMCONNSTR_DefaultConnection", "POSTGRESQLCONNSTR_DefaultConnection" })
            psi.Environment.Remove(key);
        psi.Environment["ASPNETCORE_ENVIRONMENT"] = "Development";
        psi.Environment["ASPNETCORE_URLS"] = BaseUrl;
        psi.Environment["ConnectionStrings__DefaultConnection"] = Db.PooledConnectionString;
        psi.Environment["Diagnostics__ServerTiming"] = "true";
        psi.Environment["Seed__ClearFarmData"] = "false";
        psi.Environment["Seed__ResetAdminPassword"] = "false";
        psi.Environment["Logging__LogLevel__Microsoft.AspNetCore.Hosting.Diagnostics"] = "Information";
        psi.Environment["OpenAI__ApiKey"] = "";
        psi.Environment["Features__AssistantEnabled"] = "true";

        _process = Process.Start(psi)!;
        _process.OutputDataReceived += (_, e) => { if (e.Data is not null) _log.Enqueue(e.Data); };
        _process.ErrorDataReceived += (_, e) => { if (e.Data is not null) _log.Enqueue("stderr: " + e.Data); };
        _process.BeginOutputReadLine();
        _process.BeginErrorReadLine();

        await WaitForAppAsync();

        // Proves the app is talking to the throw-away database: it was created empty and the app migrated + seeded it.
        if (await Db.ScalarAsync("select count(*) from \"__EFMigrationsHistory\"") == 0 ||
            await Db.ScalarAsync("select count(*) from \"AspNetUsers\"") == 0)
            throw new InvalidOperationException("The app did not migrate the disposable E2E database — refusing to continue.\n" + string.Join("\n", LogLines.TakeLast(40)));

        _playwright = await Playwright.CreateAsync();
        Browser = await LaunchAsync(_playwright);
    }

    private static async Task<IBrowser> LaunchAsync(IPlaywright pw)
    {
        Exception? last = null;
        foreach (var channel in new[] { "msedge", "chrome", null })
        {
            try
            {
                return await pw.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Channel = channel, Headless = true });
            }
            catch (PlaywrightException ex)
            {
                last = ex;
            }
        }
        throw new InvalidOperationException("BLOCKED: no Chromium browser available (tried Edge, Chrome, bundled Chromium).", last);
    }

    private async Task WaitForAppAsync()
    {
        using var http = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false }) { Timeout = TimeSpan.FromSeconds(5) };
        var deadline = DateTime.UtcNow.AddSeconds(120);
        while (DateTime.UtcNow < deadline)
        {
            if (_process!.HasExited)
                throw new InvalidOperationException("Web app exited during startup:\n" + string.Join("\n", LogLines.TakeLast(60)));
            try
            {
                var res = await http.GetAsync(BaseUrl + "/Account/Login");
                if (res.StatusCode == HttpStatusCode.OK) return;
            }
            catch (HttpRequestException) { }
            catch (TaskCanceledException) { }
            await Task.Delay(500);
        }
        throw new TimeoutException("Web app did not start within 120 s:\n" + string.Join("\n", LogLines.TakeLast(60)));
    }

    public async Task DisposeAsync()
    {
        if (Browser is not null) await Browser.CloseAsync();
        _playwright?.Dispose();
        if (_process is { HasExited: false })
        {
            _process.Kill(entireProcessTree: true);
            await _process.WaitForExitAsync();
        }
        _process?.Dispose();
        if (_process is not null)
            await File.WriteAllLinesAsync(Path.Combine(Path.GetTempPath(), "goatfarm-e2e-app.log"), LogLines);
        if (Db is not null) await Db.DisposeAsync();
    }

    private static int FreePort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    public static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "GoatFarm.slnx")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("Repository root (GoatFarm.slnx) not found.");
    }
}

[CollectionDefinition(Name)]
public sealed class E2eCollection : ICollectionFixture<E2eApp>
{
    public const string Name = "E2E";
}
