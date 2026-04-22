using System.Globalization;
using MySqlConnector;
using PaApp.Models.Api;

namespace PaApp.Services;

public sealed class SocialStore(IMySqlConnectionFactory connectionFactory) : ISocialStore
{
    private static Guid ReadGuid(MySqlDataReader reader, int ordinal)
    {
        var v = reader.GetValue(ordinal);
        return v switch
        {
            Guid g => g,
            string s => Guid.Parse(s, CultureInfo.InvariantCulture),
            _ => Guid.Parse(Convert.ToString(v, CultureInfo.InvariantCulture)!, CultureInfo.InvariantCulture)
        };
    }

    public async Task<IReadOnlyList<SocialProfileListItemDto>> ListProfilesAsync(CancellationToken cancellationToken = default)
    {
        await using var conn = await connectionFactory.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var cmd = new MySqlCommand(
            """
            SELECT
              p.id,
              p.handle,
              p.display_name,
              p.bio,
              p.avatar_hue,
              (SELECT COUNT(*) FROM social_follows f WHERE f.following_id = p.id) AS followers,
              (SELECT COUNT(*) FROM social_follows f WHERE f.follower_id = p.id) AS following,
              (SELECT COUNT(*) FROM social_posts po WHERE po.author_id = p.id) AS posts
            FROM social_profiles p
            ORDER BY p.display_name;
            """,
            conn);

        var list = new List<SocialProfileListItemDto>();
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            list.Add(new SocialProfileListItemDto(
                ReadGuid(reader, 0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetString(3),
                reader.GetInt32(4),
                reader.GetInt32(5),
                reader.GetInt32(6),
                reader.GetInt32(7)));
        }

        return list;
    }

    public async Task<SocialProfileDetailDto?> GetProfileAsync(
        Guid profileId,
        Guid? viewerId,
        CancellationToken cancellationToken = default)
    {
        await using var conn = await connectionFactory.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var cmd = new MySqlCommand(
            """
            SELECT
              p.id,
              p.handle,
              p.display_name,
              p.bio,
              p.avatar_hue,
              (SELECT COUNT(*) FROM social_follows f WHERE f.following_id = p.id) AS followers,
              (SELECT COUNT(*) FROM social_follows f WHERE f.follower_id = p.id) AS following,
              (SELECT COUNT(*) FROM social_posts po WHERE po.author_id = p.id) AS posts
            FROM social_profiles p
            WHERE p.id = @id
            LIMIT 1;
            """,
            conn);
        cmd.Parameters.AddWithValue("@id", profileId.ToString("D"));

        await using (var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false))
        {
            if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                return null;

            var id = ReadGuid(reader, 0);
            var handle = reader.GetString(1);
            var displayName = reader.GetString(2);
            var bio = reader.GetString(3);
            var hue = reader.GetInt32(4);
            var followers = reader.GetInt32(5);
            var following = reader.GetInt32(6);
            var posts = reader.GetInt32(7);

            var isSelf = viewerId == id;
            var isFollowing = false;
            if (viewerId is { } v && !isSelf)
                isFollowing = await IsFollowingAsync(v, id, cancellationToken).ConfigureAwait(false);

            return new SocialProfileDetailDto(
                id,
                handle,
                displayName,
                bio,
                hue,
                followers,
                following,
                posts,
                isFollowing,
                isSelf);
        }

    }

    public async Task<bool> ProfileExistsAsync(Guid profileId, CancellationToken cancellationToken = default)
    {
        await using var conn = await connectionFactory.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var cmd = new MySqlCommand(
            "SELECT 1 FROM social_profiles WHERE id = @id LIMIT 1;",
            conn);
        cmd.Parameters.AddWithValue("@id", profileId.ToString("D"));
        var o = await cmd.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return o is not null;
    }

    public async Task<IReadOnlyList<FeedPostDto>> GetFeedAsync(Guid viewerId, int take, CancellationToken cancellationToken = default)
    {
        take = Math.Clamp(take, 1, 100);
        await using var conn = await connectionFactory.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var cmd = new MySqlCommand(
            """
            SELECT
              p.id,
              p.caption,
              p.created_at,
              p.author_id,
              pr.handle,
              pr.display_name,
              pr.avatar_hue,
              t.id,
              t.slug,
              t.name,
              t.region,
              t.difficulty,
              t.dog_friendly,
              t.elevation_gain_ft,
              t.length_mi,
              LEFT(t.guide_excerpt, 260) AS excerpt
            FROM social_posts p
            INNER JOIN social_profiles pr ON pr.id = p.author_id
            LEFT JOIN trails t ON t.id = p.trail_id
            WHERE p.author_id IN (
              SELECT following_id FROM social_follows WHERE follower_id = @viewer
              UNION ALL
              SELECT @viewer
            )
            ORDER BY p.created_at DESC
            LIMIT @take;
            """,
            conn);
        cmd.Parameters.AddWithValue("@viewer", viewerId.ToString("D"));
        cmd.Parameters.AddWithValue("@take", take);

        return await ReadFeedRowsAsync(cmd, cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<TrailCardDto>> ListExploreTrailsAsync(int take, CancellationToken cancellationToken = default)
    {
        take = Math.Clamp(take, 1, 60);
        await using var conn = await connectionFactory.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var cmd = new MySqlCommand(
            """
            SELECT
              id,
              slug,
              name,
              region,
              difficulty,
              dog_friendly,
              elevation_gain_ft,
              length_mi,
              LEFT(guide_excerpt, 260) AS excerpt
            FROM trails
            ORDER BY id DESC
            LIMIT @take;
            """,
            conn);
        cmd.Parameters.AddWithValue("@take", take);

        var list = new List<TrailCardDto>();
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            list.Add(ReadTrailCard(reader, 0));
        }

        return list;
    }

    public async Task<IReadOnlyList<FeedPostDto>> GetProfilePostsAsync(
        Guid profileId,
        int take,
        CancellationToken cancellationToken = default)
    {
        take = Math.Clamp(take, 1, 100);
        await using var conn = await connectionFactory.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var cmd = new MySqlCommand(
            """
            SELECT
              p.id,
              p.caption,
              p.created_at,
              p.author_id,
              pr.handle,
              pr.display_name,
              pr.avatar_hue,
              t.id,
              t.slug,
              t.name,
              t.region,
              t.difficulty,
              t.dog_friendly,
              t.elevation_gain_ft,
              t.length_mi,
              LEFT(t.guide_excerpt, 260) AS excerpt
            FROM social_posts p
            INNER JOIN social_profiles pr ON pr.id = p.author_id
            LEFT JOIN trails t ON t.id = p.trail_id
            WHERE p.author_id = @author
            ORDER BY p.created_at DESC
            LIMIT @take;
            """,
            conn);
        cmd.Parameters.AddWithValue("@author", profileId.ToString("D"));
        cmd.Parameters.AddWithValue("@take", take);

        return await ReadFeedRowsAsync(cmd, cancellationToken).ConfigureAwait(false);
    }

    public async Task FollowAsync(Guid followerId, Guid followingId, CancellationToken cancellationToken = default)
    {
        if (followerId == followingId)
            return;

        await using var conn = await connectionFactory.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var cmd = new MySqlCommand(
            """
            INSERT IGNORE INTO social_follows (follower_id, following_id)
            VALUES (@follower, @following);
            """,
            conn);
        cmd.Parameters.AddWithValue("@follower", followerId.ToString("D"));
        cmd.Parameters.AddWithValue("@following", followingId.ToString("D"));
        await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task UnfollowAsync(Guid followerId, Guid followingId, CancellationToken cancellationToken = default)
    {
        await using var conn = await connectionFactory.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var cmd = new MySqlCommand(
            """
            DELETE FROM social_follows
            WHERE follower_id = @follower AND following_id = @following;
            """,
            conn);
        cmd.Parameters.AddWithValue("@follower", followerId.ToString("D"));
        cmd.Parameters.AddWithValue("@following", followingId.ToString("D"));
        await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<bool> IsFollowingAsync(Guid followerId, Guid followingId, CancellationToken cancellationToken = default)
    {
        await using var conn = await connectionFactory.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var cmd = new MySqlCommand(
            """
            SELECT 1 FROM social_follows
            WHERE follower_id = @follower AND following_id = @following
            LIMIT 1;
            """,
            conn);
        cmd.Parameters.AddWithValue("@follower", followerId.ToString("D"));
        cmd.Parameters.AddWithValue("@following", followingId.ToString("D"));
        var o = await cmd.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return o is not null;
    }

    private static async Task<IReadOnlyList<FeedPostDto>> ReadFeedRowsAsync(MySqlCommand cmd, CancellationToken cancellationToken)
    {
        var list = new List<FeedPostDto>();
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            var trail = reader.IsDBNull(7) ? null : ReadTrailCard(reader, 7);
            list.Add(new FeedPostDto(
                reader.GetInt64(0),
                reader.GetString(1),
                reader.GetDateTimeOffset(2),
                ReadGuid(reader, 3),
                reader.GetString(4),
                reader.GetString(5),
                reader.GetInt32(6),
                trail));
        }

        return list;
    }

    private static TrailCardDto ReadTrailCard(MySqlDataReader reader, int offset)
    {
        return new TrailCardDto(
            reader.GetInt64(offset + 0),
            reader.GetString(offset + 1),
            reader.GetString(offset + 2),
            reader.GetString(offset + 3),
            reader.GetString(offset + 4),
            reader.GetBoolean(offset + 5),
            reader.GetInt32(offset + 6),
            reader.GetDecimal(offset + 7),
            reader.IsDBNull(offset + 8) ? string.Empty : reader.GetString(offset + 8));
    }
}
