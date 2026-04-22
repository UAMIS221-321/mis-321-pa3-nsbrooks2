using System.Text.Json.Nodes;
using MySqlConnector;

namespace PaApp.Services;

public interface IChatStore
{
    Task<Guid> CreateSessionAsync(string title, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ChatSessionRow>> ListSessionsAsync(int take, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ChatMessageRow>> GetMessagesAsync(Guid sessionId, CancellationToken cancellationToken = default);
    Task AddMessageAsync(Guid sessionId, string role, string content, string? metadataJson, CancellationToken cancellationToken = default);
    Task TouchSessionAsync(Guid sessionId, CancellationToken cancellationToken = default);
    Task SetSessionTitleAsync(Guid sessionId, string title, CancellationToken cancellationToken = default);
    Task<string?> GetStageJsonAsync(Guid sessionId, CancellationToken cancellationToken = default);
    Task MergeStagePatchAsync(Guid sessionId, JsonObject patch, CancellationToken cancellationToken = default);
}

public sealed record ChatSessionRow(Guid Id, string Title, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);

public sealed record ChatMessageRow(string Role, string Content);

public sealed class ChatStore(IMySqlConnectionFactory connectionFactory) : IChatStore
{
    /// <summary>MySQL <c>CHAR(36)</c> / UUID columns may surface as <see cref="Guid"/> or <see cref="string"/> depending on server and connector mapping.</summary>
    private static Guid ReadGuid(MySqlDataReader reader, int ordinal)
    {
        var v = reader.GetValue(ordinal);
        return v switch
        {
            Guid g => g,
            string s => Guid.Parse(s),
            _ => Guid.Parse(Convert.ToString(v, System.Globalization.CultureInfo.InvariantCulture)!)
        };
    }

    public async Task<Guid> CreateSessionAsync(string title, CancellationToken cancellationToken = default)
    {
        var id = Guid.NewGuid();
        await using var conn = await connectionFactory.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var cmd = new MySqlCommand(
            "INSERT INTO chat_sessions (id, title) VALUES (@id, @title);",
            conn);
        cmd.Parameters.AddWithValue("@id", id.ToString("D"));
        cmd.Parameters.AddWithValue("@title", title.Length > 255 ? title[..255] : title);
        await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        return id;
    }

    public async Task<IReadOnlyList<ChatSessionRow>> ListSessionsAsync(int take, CancellationToken cancellationToken = default)
    {
        take = Math.Clamp(take, 1, 100);
        await using var conn = await connectionFactory.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var cmd = new MySqlCommand(
            """
            SELECT id, title, created_at, updated_at
            FROM chat_sessions
            ORDER BY updated_at DESC
            LIMIT @take;
            """,
            conn);
        cmd.Parameters.AddWithValue("@take", take);

        var list = new List<ChatSessionRow>();
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            list.Add(new ChatSessionRow(
                ReadGuid(reader, 0),
                reader.GetString(1),
                reader.GetDateTimeOffset(2),
                reader.GetDateTimeOffset(3)));
        }

        return list;
    }

    public async Task<IReadOnlyList<ChatMessageRow>> GetMessagesAsync(Guid sessionId, CancellationToken cancellationToken = default)
    {
        await using var conn = await connectionFactory.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var cmd = new MySqlCommand(
            """
            SELECT role, content
            FROM chat_messages
            WHERE session_id = @sid
            ORDER BY id ASC;
            """,
            conn);
        cmd.Parameters.AddWithValue("@sid", sessionId.ToString("D"));

        var list = new List<ChatMessageRow>();
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            list.Add(new ChatMessageRow(reader.GetString(0), reader.GetString(1)));

        return list;
    }

    public async Task AddMessageAsync(
        Guid sessionId,
        string role,
        string content,
        string? metadataJson,
        CancellationToken cancellationToken = default)
    {
        await using var conn = await connectionFactory.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var cmd = new MySqlCommand(
            """
            INSERT INTO chat_messages (session_id, role, content, metadata_json)
            VALUES (@sid, @role, @content, @meta);
            """,
            conn);
        cmd.Parameters.AddWithValue("@sid", sessionId.ToString("D"));
        cmd.Parameters.AddWithValue("@role", role);
        cmd.Parameters.AddWithValue("@content", content);
        cmd.Parameters.AddWithValue("@meta", metadataJson is null ? DBNull.Value : metadataJson);
        await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task TouchSessionAsync(Guid sessionId, CancellationToken cancellationToken = default)
    {
        await using var conn = await connectionFactory.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var cmd = new MySqlCommand(
            "UPDATE chat_sessions SET updated_at = CURRENT_TIMESTAMP WHERE id = @id;",
            conn);
        cmd.Parameters.AddWithValue("@id", sessionId.ToString("D"));
        await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task SetSessionTitleAsync(Guid sessionId, string title, CancellationToken cancellationToken = default)
    {
        await using var conn = await connectionFactory.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var cmd = new MySqlCommand(
            "UPDATE chat_sessions SET title = @title, updated_at = CURRENT_TIMESTAMP WHERE id = @id;",
            conn);
        cmd.Parameters.AddWithValue("@id", sessionId.ToString("D"));
        cmd.Parameters.AddWithValue("@title", title.Length > 255 ? title[..255] : title);
        await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<string?> GetStageJsonAsync(Guid sessionId, CancellationToken cancellationToken = default)
    {
        await using var conn = await connectionFactory.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var cmd = new MySqlCommand(
            "SELECT stage_json FROM chat_sessions WHERE id = @id LIMIT 1;",
            conn);
        cmd.Parameters.AddWithValue("@id", sessionId.ToString("D"));
        var o = await cmd.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return o is null or DBNull ? null : (string?)o;
    }

    public async Task MergeStagePatchAsync(Guid sessionId, JsonObject patch, CancellationToken cancellationToken = default)
    {
        await using var conn = await connectionFactory.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var tx = await conn.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            string? current = null;
            await using (var read = new MySqlCommand(
                               "SELECT stage_json FROM chat_sessions WHERE id = @id FOR UPDATE;",
                               conn, tx))
            {
                read.Parameters.AddWithValue("@id", sessionId.ToString("D"));
                var scalar = await read.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
                if (scalar is string s)
                    current = s;
            }

            var merged = string.IsNullOrWhiteSpace(current)
                ? new JsonObject()
                : JsonNode.Parse(current)!.AsObject();

            foreach (var kv in patch)
            {
                if (kv.Value is null)
                    merged.Remove(kv.Key);
                else
                    merged[kv.Key] = kv.Value.DeepClone();
            }

            await using (var write = new MySqlCommand(
                              "UPDATE chat_sessions SET stage_json = @j, updated_at = CURRENT_TIMESTAMP WHERE id = @id;",
                              conn, tx))
            {
                write.Parameters.AddWithValue("@id", sessionId.ToString("D"));
                write.Parameters.AddWithValue("@j", merged.ToJsonString());
                await write.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }

            await tx.CommitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            await tx.RollbackAsync(cancellationToken).ConfigureAwait(false);
            throw;
        }
    }
}
