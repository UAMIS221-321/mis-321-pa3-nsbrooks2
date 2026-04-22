namespace PaApp.Configuration;

/// <summary>
/// Resolves MySQL connection string from environment: explicit override, then Heroku-style URLs, then configuration.
/// JawsDB sets <c>JAWSDB_URL</c> (not always <c>DATABASE_URL</c>).
/// </summary>
public static class ConnectionStringResolver
{
    public const string MySqlConfigKey = "MySql";

    public static string? Resolve(IConfiguration configuration)
    {
        var explicitConn =
            Environment.GetEnvironmentVariable("MYSQL_CONNECTION_STRING")
            ?? Environment.GetEnvironmentVariable("ConnectionStrings__MySql");

        if (!string.IsNullOrWhiteSpace(explicitConn))
            return explicitConn;

        foreach (var envName in HerokuMysqlUrlEnvNames)
        {
            var fromUrl = HerokuDatabaseUrlParser.TryBuildMySqlConnectionString(
                Environment.GetEnvironmentVariable(envName));
            if (!string.IsNullOrWhiteSpace(fromUrl))
                return fromUrl;
        }

        return configuration.GetConnectionString(MySqlConfigKey);
    }

    /// <summary>First match wins. DATABASE_URL is set by some add-ons; JawsDB uses JAWSDB_URL.</summary>
    private static readonly string[] HerokuMysqlUrlEnvNames =
    [
        "DATABASE_URL",
        "JAWSDB_URL",
        "JAWSDB_MARIA_URL",
        "CLEARDB_DATABASE_URL",
        "JAWSDB_OLIVE_URL",
    ];
}
