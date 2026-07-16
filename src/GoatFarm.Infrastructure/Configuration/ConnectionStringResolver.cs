using Microsoft.Extensions.Configuration;

namespace GoatFarm.Infrastructure.Configuration;

internal static class ConnectionStringResolver
{
    /// <summary>
    /// Resolves DefaultConnection from appsettings and Azure App Service env vars.
    /// .NET only auto-maps SQLCONNSTR_, SQLAZURECONNSTR_, MYSQLCONNSTR_, and CUSTOMCONNSTR_
    /// — not POSTGRESQLCONNSTR_. See https://github.com/dotnet/runtime/issues/36123
    /// </summary>
    public static string? Resolve(IConfiguration configuration, string name = "DefaultConnection")
    {
        var fromConfig = configuration.GetConnectionString(name);
        if (!string.IsNullOrWhiteSpace(fromConfig))
            return fromConfig.Trim();

        // Application setting: ConnectionStrings__DefaultConnection
        var appSetting = configuration[$"ConnectionStrings:{name}"];
        if (!string.IsNullOrWhiteSpace(appSetting))
            return appSetting.Trim();

        // Azure Connection strings blade (by type prefix)
        var envKeys = new[]
        {
            $"CUSTOMCONNSTR_{name}",
            $"POSTGRESQLCONNSTR_{name}",
            $"SQLCONNSTR_{name}",
            $"SQLAZURECONNSTR_{name}",
            $"MYSQLCONNSTR_{name}",
        };

        foreach (var key in envKeys)
        {
            var value = Environment.GetEnvironmentVariable(key);
            if (!string.IsNullOrWhiteSpace(value))
                return value.Trim();
        }

        return null;
    }
}
