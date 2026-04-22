namespace PaApp.Configuration;

/// <summary>
/// Parses Heroku-style <c>DATABASE_URL</c> (e.g. <c>mysql://user:pass@host:3306/dbname</c>)
/// into a MySqlConnector connection string. Prefer environment variables in production.
/// </summary>
public static class HerokuDatabaseUrlParser
{
    public static string? TryBuildMySqlConnectionString(string? databaseUrl)
    {
        if (string.IsNullOrWhiteSpace(databaseUrl))
            return null;

        if (!Uri.TryCreate(databaseUrl, UriKind.Absolute, out var uri) ||
            (uri.Scheme != "mysql" && uri.Scheme != "mysql2"))
            return null;

        var userInfo = uri.UserInfo.Split(':', 2);
        var user = Uri.UnescapeDataString(userInfo[0]);
        var password = userInfo.Length > 1 ? Uri.UnescapeDataString(userInfo[1]) : string.Empty;

        var database = uri.AbsolutePath.TrimStart('/');
        if (string.IsNullOrEmpty(database))
            return null;

        var port = uri.IsDefaultPort ? 3306 : uri.Port;

        // SslMode preferred for managed cloud; adjust via query string if needed.
        return $"Server={uri.Host};Port={port};Database={database};User ID={user};Password={password};TreatTinyAsBoolean=false;SslMode=Preferred;";
    }
}
