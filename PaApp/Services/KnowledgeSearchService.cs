using MySqlConnector;
using PaApp.Models;

namespace PaApp.Services;

public sealed class KnowledgeSearchService(IMySqlConnectionFactory connectionFactory) : IKnowledgeSearchService
{
    public async Task<IReadOnlyList<KnowledgeDocument>> SearchAsync(
        string query,
        int take = 5,
        CancellationToken cancellationToken = default)
    {
        var trimmed = query.Trim();
        if (trimmed.Length == 0)
            return Array.Empty<KnowledgeDocument>();

        take = Math.Clamp(take, 1, 12);
        await using var conn = await connectionFactory.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);

        var results = new List<KnowledgeDocument>();

        await using (var cmd = new MySqlCommand(
                         """
                         SELECT id, title, content, source_label, created_at, updated_at
                         FROM knowledge_documents
                         WHERE MATCH(title, content) AGAINST (@q IN NATURAL LANGUAGE MODE)
                         LIMIT @take;
                         """,
                         conn))
        {
            cmd.Parameters.AddWithValue("@q", trimmed);
            cmd.Parameters.AddWithValue("@take", take);
            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                results.Add(ReadDoc(reader));
        }

        if (results.Count > 0)
            return results;

        var like = "%" + EscapeLike(trimmed) + "%";
        await using var fallback = new MySqlCommand(
            """
            SELECT id, title, content, source_label, created_at, updated_at
            FROM knowledge_documents
            WHERE title LIKE @p ESCAPE '\\' OR content LIKE @p ESCAPE '\\'
            ORDER BY id DESC
            LIMIT @take;
            """,
            conn);
        fallback.Parameters.AddWithValue("@p", like);
        fallback.Parameters.AddWithValue("@take", take);
        await using var r2 = await fallback.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await r2.ReadAsync(cancellationToken).ConfigureAwait(false))
            results.Add(ReadDoc(r2));

        return results;
    }

    private static string EscapeLike(string input)
    {
        return input.Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("%", "\\%", StringComparison.Ordinal)
            .Replace("_", "\\_", StringComparison.Ordinal);
    }

    private static KnowledgeDocument ReadDoc(MySqlDataReader reader) => new()
    {
        Id = reader.GetInt64(0),
        Title = reader.GetString(1),
        Content = reader.GetString(2),
        SourceLabel = reader.IsDBNull(3) ? null : reader.GetString(3),
        CreatedAt = reader.GetDateTimeOffset(4),
        UpdatedAt = reader.GetDateTimeOffset(5)
    };
}
