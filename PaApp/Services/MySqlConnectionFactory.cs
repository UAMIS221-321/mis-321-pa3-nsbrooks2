using MySqlConnector;
using PaApp.Configuration;

namespace PaApp.Services;

public sealed class MySqlConnectionFactory : IMySqlConnectionFactory
{
    private readonly string? _connectionString;

    public MySqlConnectionFactory(IConfiguration configuration)
    {
        _connectionString = ConnectionStringResolver.Resolve(configuration);
    }

    public async ValueTask<MySqlConnection> OpenConnectionAsync(CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_connectionString))
            throw new InvalidOperationException(
                "MySQL is not configured. Set MYSQL_CONNECTION_STRING, ConnectionStrings__MySql, DATABASE_URL, or JawsDB JAWSDB_URL (see ConnectionStringResolver).");

        var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        return connection;
    }
}
