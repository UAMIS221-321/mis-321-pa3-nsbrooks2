using MySqlConnector;

namespace PaApp.Services;

public interface IMySqlConnectionFactory
{
    /// <summary>Returns a new, undisposed connection. Caller must dispose.</summary>
    ValueTask<MySqlConnection> OpenConnectionAsync(CancellationToken cancellationToken = default);
}
