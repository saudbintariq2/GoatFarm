using GoatFarm.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Npgsql;

namespace GoatFarm.Tests.Postgres;

/// <summary>
/// Creates a throwaway database per test on a loopback PostgreSQL server named by <c>GOATFARM_TEST_PG</c>
/// (for example <c>Host=127.0.0.1;Port=55432;Username=postgres</c>) and drops it afterwards.
/// Tests are skipped — reported as skipped, never as passed — when the variable is not set.
/// </summary>
public sealed class PostgresTestDatabase : IAsyncDisposable
{
    public const string EnvVar = "GOATFARM_TEST_PG";
    private const string NamePrefix = "goatfarm_test_";

    private readonly string _serverConnectionString;
    public string DatabaseName { get; }
    public string ConnectionString { get; }
    /// <summary>Same database with Npgsql's default connection pooling, as the deployed app uses.</summary>
    public string PooledConnectionString { get; }

    private PostgresTestDatabase(string serverConnectionString, string databaseName)
    {
        _serverConnectionString = serverConnectionString;
        DatabaseName = databaseName;
        ConnectionString = new NpgsqlConnectionStringBuilder(serverConnectionString)
        {
            Database = databaseName,
            Pooling = false
        }.ConnectionString;
        PooledConnectionString = new NpgsqlConnectionStringBuilder(serverConnectionString) { Database = databaseName, Pooling = true }.ConnectionString;
    }

    public static string? ServerConnectionString => Environment.GetEnvironmentVariable(EnvVar);

    public static async Task<PostgresTestDatabase> CreateAsync(bool migrate = true)
    {
        var server = ServerConnectionString;
        Skip.If(string.IsNullOrWhiteSpace(server), $"BLOCKED: set {EnvVar} to a disposable loopback PostgreSQL server.");

        var builder = new NpgsqlConnectionStringBuilder(server);
        if (builder.Host is not ("127.0.0.1" or "localhost" or "::1"))
            throw new InvalidOperationException($"{EnvVar} must point at a loopback test server, not '{builder.Host}'.");
        builder.Database = "postgres";
        builder.Pooling = false;

        var db = new PostgresTestDatabase(builder.ConnectionString, NamePrefix + Guid.NewGuid().ToString("N")[..16]);
        await using (var conn = new NpgsqlConnection(db._serverConnectionString))
        {
            await conn.OpenAsync();
            await using var cmd = new NpgsqlCommand($"CREATE DATABASE \"{db.DatabaseName}\"", conn);
            await cmd.ExecuteNonQueryAsync();
        }

        if (migrate)
        {
            await using var ctx = db.CreateContext();
            await ctx.Database.MigrateAsync();
        }
        return db;
    }

    /// <summary>Same provider options as production, including the retrying execution strategy.</summary>
    public GoatFarmDbContext CreateContext(Action<string>? sqlLog = null, params IInterceptor[] interceptors)
    {
        var options = new DbContextOptionsBuilder<GoatFarmDbContext>()
            .UseNpgsql(ConnectionString, npgsql =>
            {
                npgsql.EnableRetryOnFailure(maxRetryCount: 5, maxRetryDelay: TimeSpan.FromSeconds(10), errorCodesToAdd: null);
                npgsql.CommandTimeout(60);
            });
        if (sqlLog is not null)
            options.LogTo(sqlLog, [RelationalEventId.CommandExecuted]);
        if (interceptors.Length > 0)
            options.AddInterceptors(interceptors);
        return new GoatFarmDbContext(options.Options);
    }

    public async Task<long> ScalarAsync(string sql)
    {
        await using var conn = new NpgsqlConnection(ConnectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(sql, conn);
        return Convert.ToInt64(await cmd.ExecuteScalarAsync());
    }

    public async Task ExecuteAsync(string sql)
    {
        await using var conn = new NpgsqlConnection(ConnectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(sql, conn);
        await cmd.ExecuteNonQueryAsync();
    }

    public async ValueTask DisposeAsync()
    {
        if (!DatabaseName.StartsWith(NamePrefix, StringComparison.Ordinal)) return;
        await using var conn = new NpgsqlConnection(_serverConnectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{DatabaseName}\" WITH (FORCE)", conn);
        await cmd.ExecuteNonQueryAsync();
    }
}
