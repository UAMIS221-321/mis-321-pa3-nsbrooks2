namespace PaApp.Configuration;

/// <summary>
/// Resolves MySQL connection string from environment: explicit override, then Heroku DATABASE_URL, then configuration.
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

        var fromHeroku = HerokuDatabaseUrlParser.TryBuildMySqlConnectionString(
            Environment.GetEnvironmentVariable("DATABASE_URL"));

        if (!string.IsNullOrWhiteSpace(fromHeroku))
            return fromHeroku;

        return configuration.GetConnectionString(MySqlConfigKey);
    }
}
