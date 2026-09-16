using Microsoft.Extensions.Configuration;
using Npgsql;

namespace GoatFarm.Infrastructure.Configuration;

internal static class ConnectionStringResolver
{
    /// <summary>
    /// Resolves DefaultConnection from appsettings, Railway, and Azure App Service env vars.
    /// Converts postgres:// URIs (Railway DATABASE_URL) to Npgsql key=value format.
    /// Prefers public database endpoints over railway.internal (private DNS is not always reachable).
    /// </summary>
    public static string? Resolve(IConfiguration configuration, string name = "DefaultConnection")
    {
        var candidates = new List<string?>();

        // Prefer Railway public URL — internal *.railway.internal often fails to resolve
        candidates.Add(Environment.GetEnvironmentVariable("DATABASE_PUBLIC_URL"));
        candidates.Add(configuration.GetConnectionString(name));
        candidates.Add(configuration[$"ConnectionStrings:{name}"]);
        candidates.Add(Environment.GetEnvironmentVariable($"ConnectionStrings__{name}"));
        candidates.Add(BuildFromPostgresEnvVars());
        candidates.Add(Environment.GetEnvironmentVariable("DATABASE_URL"));
        candidates.Add(Environment.GetEnvironmentVariable("DATABASE_PRIVATE_URL"));

        foreach (var key in new[]
        {
            $"CUSTOMCONNSTR_{name}",
            $"POSTGRESQLCONNSTR_{name}",
            $"SQLCONNSTR_{name}",
            $"SQLAZURECONNSTR_{name}",
            $"MYSQLCONNSTR_{name}",
        })
        {
            candidates.Add(Environment.GetEnvironmentVariable(key));
        }

        string? fallback = null;

        foreach (var candidate in candidates)
        {
            if (string.IsNullOrWhiteSpace(candidate))
                continue;

            var normalized = NormalizeConnectionString(candidate);
            if (string.IsNullOrWhiteSpace(normalized))
                continue;

            if (!UsesRailwayInternalHost(normalized))
                return normalized;

            fallback ??= normalized;
        }

        return fallback;
    }

    internal static string NormalizeConnectionString(string connectionString)
    {
        var trimmed = connectionString.Trim().Trim('"', '\'');

        if (!trimmed.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase) &&
            !trimmed.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase))
        {
            return trimmed;
        }

        var uri = new Uri(trimmed);
        var userInfo = uri.UserInfo.Split(':', 2);
        var username = Uri.UnescapeDataString(userInfo[0]);
        var password = userInfo.Length > 1 ? Uri.UnescapeDataString(userInfo[1]) : string.Empty;
        var database = uri.AbsolutePath.TrimStart('/');
        var port = uri.Port > 0 ? uri.Port : 5432;

        var builder = new NpgsqlConnectionStringBuilder
        {
            Host = uri.Host,
            Port = port,
            Database = database,
            Username = username,
            Password = password,
        };

        if (uri.Host.Contains("railway.internal", StringComparison.OrdinalIgnoreCase))
        {
            builder.SslMode = SslMode.Disable;
        }
        else
        {
            builder.SslMode = SslMode.Require;
            builder.TrustServerCertificate = true;
        }

        return builder.ConnectionString;
    }

    private static bool UsesRailwayInternalHost(string connectionString) =>
        connectionString.Contains("railway.internal", StringComparison.OrdinalIgnoreCase);

    private static string? BuildFromPostgresEnvVars()
    {
        var host = Environment.GetEnvironmentVariable("PGHOST");
        var database = Environment.GetEnvironmentVariable("PGDATABASE");
        var user = Environment.GetEnvironmentVariable("PGUSER");
        var password = Environment.GetEnvironmentVariable("PGPASSWORD");

        if (string.IsNullOrWhiteSpace(host) ||
            string.IsNullOrWhiteSpace(database) ||
            string.IsNullOrWhiteSpace(user) ||
            string.IsNullOrWhiteSpace(password))
        {
            return null;
        }

        var port = Environment.GetEnvironmentVariable("PGPORT");
        if (string.IsNullOrWhiteSpace(port))
            port = "5432";

        var ssl = host.Contains("railway.internal", StringComparison.OrdinalIgnoreCase)
            ? "SSL Mode=Disable"
            : "SSL Mode=Require;Trust Server Certificate=true";

        return $"Host={host};Port={port};Database={database};Username={user};Password={password};{ssl}";
    }
}
