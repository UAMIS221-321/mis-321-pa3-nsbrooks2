using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using MySqlConnector;

namespace PaApp.Services;

/// <summary>Gemini function-calling backend: trails, Open-Meteo weather, gear lists, safety itineraries.</summary>
public sealed class ScoutToolService(
    IMySqlConnectionFactory connectionFactory,
    IChatStore chatStore,
    IScoutTurnContext turn,
    IHttpClientFactory httpFactory,
    ILogger<ScoutToolService> log) : IDatabaseToolService
{
    public async Task<JsonObject> ExecuteAsync(string functionName, JsonObject? arguments, CancellationToken cancellationToken = default)
    {
        var name = functionName.Trim().ToLowerInvariant().Replace("-", "_");
        return name switch
        {
            "search_trails" => await SearchTrailsAsync(arguments, cancellationToken).ConfigureAwait(false),
            "get_trailhead_weather" => await GetTrailheadWeatherAsync(arguments, cancellationToken).ConfigureAwait(false),
            "upsert_gear_checklist" => await UpsertGearChecklistAsync(arguments, cancellationToken).ConfigureAwait(false),
            "create_safety_itinerary" => await CreateSafetyItineraryAsync(arguments, cancellationToken).ConfigureAwait(false),
            _ => new JsonObject { ["error"] = $"Unknown function: {functionName}" }
        };
    }

    private Guid RequireSession()
    {
        if (turn.SessionId == Guid.Empty)
            throw new InvalidOperationException("Active Mission Control session is not set.");
        return turn.SessionId;
    }

    private async Task<JsonObject> SearchTrailsAsync(JsonObject? args, CancellationToken cancellationToken)
    {
        args ??= new JsonObject();
        var dogFriendly = GetBool(args, "dog_friendly");
        var maxElev = GetInt(args, "max_elevation_gain_ft");
        var weekday = GetString(args, "preferred_weekday");
        var difficulty = GetString(args, "difficulty")?.ToLowerInvariant();
        var region = GetString(args, "region_contains");

        await using var conn = await connectionFactory.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        var sql = new StringBuilder(
            """
            SELECT id, slug, name, region, dog_friendly, elevation_gain_ft, length_mi, difficulty,
                   crowd_calendar_note, trailhead_lat, trailhead_lng,
                   LEFT(guide_excerpt, 380) AS excerpt
            FROM trails
            WHERE 1 = 1
            """);
        await using var cmd = new MySqlCommand(string.Empty, conn);

        if (dogFriendly is true)
            sql.Append(" AND dog_friendly = 1");

        if (maxElev is int me)
        {
            sql.Append(" AND elevation_gain_ft <= @max_elev");
            cmd.Parameters.AddWithValue("@max_elev", me);
        }

        if (!string.IsNullOrWhiteSpace(difficulty) && difficulty is "easy" or "moderate" or "hard")
        {
            sql.Append(" AND difficulty = @diff");
            cmd.Parameters.AddWithValue("@diff", difficulty);
        }

        if (!string.IsNullOrWhiteSpace(region))
        {
            sql.Append(" AND region LIKE @region");
            cmd.Parameters.AddWithValue("@region", "%" + EscapeLike(region.Trim()) + "%");
        }

        if (!string.IsNullOrWhiteSpace(weekday))
        {
            sql.Append(" AND crowd_calendar_note LIKE @weekday");
            cmd.Parameters.AddWithValue("@weekday", "%" + EscapeLike(weekday.Trim()) + "%");
        }

        sql.Append(" ORDER BY elevation_gain_ft ASC LIMIT 12;");
        cmd.CommandText = sql.ToString();

        var rows = new JsonArray();
        await using (var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false))
        {
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                rows.Add(new JsonObject
                {
                    ["id"] = reader.GetInt64(0),
                    ["slug"] = reader.GetString(1),
                    ["name"] = reader.GetString(2),
                    ["region"] = reader.GetString(3),
                    ["dogFriendly"] = reader.GetBoolean(4),
                    ["elevationGainFt"] = reader.GetInt32(5),
                    ["lengthMi"] = reader.GetDecimal(6),
                    ["difficulty"] = reader.GetString(7),
                    ["crowdCalendarNote"] = reader.GetString(8),
                    ["trailheadLat"] = reader.GetDecimal(9),
                    ["trailheadLng"] = reader.GetDecimal(10),
                    ["excerpt"] = reader.GetString(11)
                });
            }
        }

        var echo = new JsonObject();
        if (dogFriendly is not null) echo["dogFriendly"] = dogFriendly.Value;
        if (maxElev is not null) echo["maxElevationGainFt"] = maxElev.Value;
        if (weekday is not null) echo["preferredWeekday"] = weekday;
        if (difficulty is not null) echo["difficulty"] = difficulty;
        if (region is not null) echo["regionContains"] = region;

        // Do not attach `rows` / `echo` to both the stage merge tree and the tool return object (JsonNode single-parent rule).
        await chatStore
            .MergeStagePatchAsync(
                RequireSession(),
                new JsonObject
                {
                    ["trails"] = new JsonObject
                    {
                        ["results"] = rows.DeepClone(),
                        ["queryEcho"] = echo.DeepClone()
                    }
                },
                cancellationToken)
            .ConfigureAwait(false);

        return new JsonObject { ["matchCount"] = rows.Count, ["trails"] = rows, ["queryEcho"] = echo };
    }

    private async Task<JsonObject> GetTrailheadWeatherAsync(JsonObject? args, CancellationToken cancellationToken)
    {
        args ??= new JsonObject();
        decimal lat;
        decimal lng;
        string label;

        var trailId = GetLong(args, "trail_id");
        var slug = GetString(args, "trail_slug");
        var latArg = GetDecimal(args, "latitude");
        var lngArg = GetDecimal(args, "longitude");

        if (trailId is not null || !string.IsNullOrWhiteSpace(slug))
        {
            await using var conn = await connectionFactory.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
            await using var cmd = trailId is not null
                ? new MySqlCommand(
                    "SELECT name, trailhead_lat, trailhead_lng FROM trails WHERE id = @id LIMIT 1;",
                    conn)
                : new MySqlCommand(
                    "SELECT name, trailhead_lat, trailhead_lng FROM trails WHERE slug = @slug LIMIT 1;",
                    conn);

            if (trailId is not null)
                cmd.Parameters.AddWithValue("@id", trailId.Value);
            else
                cmd.Parameters.AddWithValue("@slug", slug!.Trim());

            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                return new JsonObject { ["error"] = "Trail not found for weather lookup." };

            label = reader.GetString(0);
            lat = reader.GetDecimal(1);
            lng = reader.GetDecimal(2);
        }
        else if (latArg is not null && lngArg is not null)
        {
            lat = latArg.Value;
            lng = lngArg.Value;
            label = $"Coordinates {lat:F4},{lng:F4}";
        }
        else
        {
            return new JsonObject { ["error"] = "Provide trail_id, trail_slug, or latitude and longitude." };
        }

        var forecast = await FetchOpenMeteoAsync(lat, lng, cancellationToken).ConfigureAwait(false);
        var stageWeather = new JsonObject
        {
            ["label"] = label,
            ["trailheadLat"] = lat,
            ["trailheadLng"] = lng,
            ["hourly"] = forecast,
            ["source"] = "Open-Meteo (CC BY 4.0)",
            ["fetchedAtUtc"] = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture)
        };

        if (trailId is not null)
            stageWeather["trailId"] = trailId.Value;

        await chatStore.MergeStagePatchAsync(RequireSession(), new JsonObject { ["weather"] = stageWeather }, cancellationToken)
            .ConfigureAwait(false);

        return new JsonObject
        {
            ["label"] = label,
            ["hourlySampleHours"] = forecast.Count,
            ["summary"] = "Hourly rows returned in stage panel (temp °F, precip %, wind mph, WMO weather code)."
        };
    }

    private async Task<JsonArray> FetchOpenMeteoAsync(decimal lat, decimal lng, CancellationToken cancellationToken)
    {
        var client = httpFactory.CreateClient("openmeteo");
        var url =
            $"v1/forecast?latitude={Uri.EscapeDataString(lat.ToString(CultureInfo.InvariantCulture))}" +
            $"&longitude={Uri.EscapeDataString(lng.ToString(CultureInfo.InvariantCulture))}" +
            "&hourly=temperature_2m,precipitation_probability,weathercode,windspeed_10m" +
            "&temperature_unit=fahrenheit&wind_speed_unit=mph&forecast_days=3";

        string json;
        try
        {
            json = await client.GetStringAsync(url, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            log.LogWarning(ex, "Open-Meteo request failed.");
            return new JsonArray(new JsonObject { ["error"] = "Weather service unreachable." });
        }

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        if (!root.TryGetProperty("hourly", out var hourly))
            return new JsonArray();

        var times = hourly.GetProperty("time").EnumerateArray().Select(x => x.GetString()).ToList();
        var temps = hourly.GetProperty("temperature_2m").EnumerateArray().ToList();
        var precips = hourly.GetProperty("precipitation_probability").EnumerateArray().ToList();
        var codes = hourly.GetProperty("weathercode").EnumerateArray().ToList();
        var winds = hourly.GetProperty("windspeed_10m").EnumerateArray().ToList();

        var take = Math.Min(36, times.Count);
        var arr = new JsonArray();
        for (var i = 0; i < take; i++)
        {
            arr.Add(new JsonObject
            {
                ["time"] = times[i] ?? "",
                ["tempF"] = i < temps.Count && temps[i].ValueKind == JsonValueKind.Number ? temps[i].GetDecimal() : 0,
                ["precipPct"] = i < precips.Count && precips[i].ValueKind == JsonValueKind.Number ? precips[i].GetInt32() : 0,
                ["wmoCode"] = i < codes.Count && codes[i].ValueKind == JsonValueKind.Number ? codes[i].GetInt32() : 0,
                ["windMph"] = i < winds.Count && winds[i].ValueKind == JsonValueKind.Number ? winds[i].GetDecimal() : 0
            });
        }

        return arr;
    }

    private async Task<JsonObject> UpsertGearChecklistAsync(JsonObject? args, CancellationToken cancellationToken)
    {
        args ??= new JsonObject();
        var items = GetStringArray(args, "items");
        if (items.Count == 0)
            return new JsonObject { ["error"] = "items array is required." };

        var difficulty = GetString(args, "difficulty_tag") ?? string.Empty;
        var sessionId = RequireSession();

        var itemsJson = JsonSerializer.Serialize(items);
        await using var conn = await connectionFactory.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var cmd = new MySqlCommand(
            """
            INSERT INTO hiker_gear_lists (session_id, difficulty_tag, items_json)
            VALUES (@sid, @diff, @items)
            ON DUPLICATE KEY UPDATE difficulty_tag = VALUES(difficulty_tag), items_json = VALUES(items_json), updated_at = CURRENT_TIMESTAMP;
            """,
            conn);
        cmd.Parameters.AddWithValue("@sid", sessionId.ToString("D"));
        cmd.Parameters.AddWithValue("@diff", difficulty.Length > 64 ? difficulty[..64] : difficulty);
        cmd.Parameters.AddWithValue("@items", itemsJson);
        await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);

        var itemNodes = new JsonArray();
        foreach (var s in items)
            itemNodes.Add(JsonValue.Create(s));

        var stageGear = new JsonObject
        {
            ["items"] = itemNodes,
            ["difficultyTag"] = difficulty
        };

        await chatStore.MergeStagePatchAsync(sessionId, new JsonObject { ["gear"] = stageGear }, cancellationToken)
            .ConfigureAwait(false);

        return new JsonObject { ["itemCount"] = items.Count, ["difficultyTag"] = difficulty };
    }

    private async Task<JsonObject> CreateSafetyItineraryAsync(JsonObject? args, CancellationToken cancellationToken)
    {
        args ??= new JsonObject();
        var trailName = GetString(args, "trail_name");
        var planner = GetString(args, "planner_name");
        var routeSummaryText = GetString(args, "route_summary");
        var contactName = GetString(args, "emergency_contact_name");
        var contactChannel = GetString(args, "emergency_contact_channel");

        if (string.IsNullOrWhiteSpace(trailName) || string.IsNullOrWhiteSpace(planner) ||
            string.IsNullOrWhiteSpace(routeSummaryText) || string.IsNullOrWhiteSpace(contactName) ||
            string.IsNullOrWhiteSpace(contactChannel))
        {
            return new JsonObject { ["error"] = "Missing required itinerary fields." };
        }

        DateTime? start = ParseIsoDate(GetString(args, "planned_start"));
        DateTime? end = ParseIsoDate(GetString(args, "planned_return"));

        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(12)).ToLowerInvariant();
        var formal = BuildFormalItineraryBody(
            trailName.Trim(),
            planner.Trim(),
            routeSummaryText.Trim(),
            start,
            end,
            contactName.Trim(),
            contactChannel.Trim());

        var sessionId = RequireSession();
        await using var conn = await connectionFactory.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var cmd = new MySqlCommand(
            """
            INSERT INTO safety_itineraries
              (session_id, share_token, trail_name, planner_name, route_summary, planned_start, planned_return,
               emergency_contact_name, emergency_contact_channel, formal_body)
            VALUES
              (@sid, @tok, @trail, @plan, @route, @ps, @pr, @ecname, @ecch, @body);
            """,
            conn);
        cmd.Parameters.AddWithValue("@sid", sessionId.ToString("D"));
        cmd.Parameters.AddWithValue("@tok", token);
        cmd.Parameters.AddWithValue("@trail", trailName.Length > 255 ? trailName[..255] : trailName);
        cmd.Parameters.AddWithValue("@plan", planner.Length > 255 ? planner[..255] : planner);
        cmd.Parameters.AddWithValue("@route", routeSummaryText);
        cmd.Parameters.AddWithValue("@ps", start.HasValue ? start.Value : DBNull.Value);
        cmd.Parameters.AddWithValue("@pr", end.HasValue ? end.Value : DBNull.Value);
        cmd.Parameters.AddWithValue("@ecname", contactName.Length > 255 ? contactName[..255] : contactName);
        cmd.Parameters.AddWithValue("@ecch", contactChannel.Length > 255 ? contactChannel[..255] : contactChannel);
        cmd.Parameters.AddWithValue("@body", formal);

        await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);

        var viewerPath = $"/api/scout/share/{token}";
        var stageIt = new JsonObject
        {
            ["shareToken"] = token,
            ["trailName"] = trailName.Trim(),
            ["viewerPath"] = viewerPath,
            ["summaryLine"] = routeSummaryText.Trim().Length > 200
                ? routeSummaryText.Trim()[..200] + "…"
                : routeSummaryText.Trim()
        };

        await chatStore.MergeStagePatchAsync(sessionId, new JsonObject { ["itinerary"] = stageIt }, cancellationToken)
            .ConfigureAwait(false);

        return new JsonObject
        {
            ["shareToken"] = token,
            ["viewerPath"] = viewerPath,
            ["message"] = "Share this read-only link with your emergency contact."
        };
    }

    private static string BuildFormalItineraryBody(
        string trail,
        string planner,
        string routeSummary,
        DateTime? start,
        DateTime? end,
        string emergencyName,
        string emergencyChannel)
    {
        var sb = new StringBuilder();
        sb.AppendLine("SUMMITSCOUT — FORMAL SAFETY ITINERARY");
        sb.AppendLine("-------------------------------------");
        sb.Append("Trail / objective: ").AppendLine(trail);
        sb.Append("Primary planner on trail: ").AppendLine(planner);
        sb.AppendLine();
        sb.AppendLine("Route & decision points:");
        sb.AppendLine(routeSummary);
        sb.AppendLine();
        sb.Append("Planned start: ").AppendLine(start?.ToString("u") ?? "(not specified)");
        sb.Append("Planned return: ").AppendLine(end?.ToString("u") ?? "(not specified)");
        sb.AppendLine();
        sb.AppendLine("Emergency contact (off-trail):");
        sb.Append("Name: ").AppendLine(emergencyName);
        sb.Append("Channel: ").AppendLine(emergencyChannel);
        sb.AppendLine();
        sb.AppendLine("If overdue:");
        sb.AppendLine("- Attempt voice/SMS on the channel above.");
        sb.AppendLine("- Provide this itinerary and last known objective to local SAR.");
        sb.AppendLine();
        sb.Append("Generated (UTC): ").AppendLine(DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture));
        sb.AppendLine("SummitScout AI — not a substitute for official land-manager permits or closures.");
        return sb.ToString();
    }

    private static DateTime? ParseIsoDate(string? s)
    {
        if (string.IsNullOrWhiteSpace(s))
            return null;
        return DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var dt)
            ? dt
            : null;
    }

    private static string? GetString(JsonObject? o, string key)
    {
        if (o is null || !o.TryGetPropertyValue(key, out var n) || n is null)
            return null;
        return n switch
        {
            JsonValue v when v.TryGetValue<string>(out var s) => s,
            JsonValue v => v.ToString(),
            _ => null
        };
    }

    private static bool? GetBool(JsonObject? o, string key)
    {
        if (o is null || !o.TryGetPropertyValue(key, out var n) || n is null)
            return null;
        return n is JsonValue v && v.TryGetValue<bool>(out var b) ? b : null;
    }

    private static int? GetInt(JsonObject? o, string key)
    {
        if (o is null || !o.TryGetPropertyValue(key, out var n) || n is null)
            return null;
        return n is JsonValue v && v.TryGetValue<int>(out var i) ? i : null;
    }

    private static long? GetLong(JsonObject? o, string key)
    {
        if (o is null || !o.TryGetPropertyValue(key, out var n) || n is null)
            return null;
        return n is JsonValue v && v.TryGetValue<long>(out var l) ? l : null;
    }

    private static decimal? GetDecimal(JsonObject? o, string key)
    {
        if (o is null || !o.TryGetPropertyValue(key, out var n) || n is null)
            return null;
        return n is JsonValue v && v.TryGetValue<decimal>(out var d) ? d : null;
    }

    private static List<string> GetStringArray(JsonObject? o, string key)
    {
        var list = new List<string>();
        if (o is null || !o.TryGetPropertyValue(key, out var n) || n is not JsonArray arr)
            return list;

        foreach (var item in arr)
        {
            if (item is JsonValue jv && jv.TryGetValue<string>(out var s) && !string.IsNullOrWhiteSpace(s))
                list.Add(s.Trim());
        }

        return list;
    }

    private static string EscapeLike(string input) =>
        input.Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("%", "\\%", StringComparison.Ordinal)
            .Replace("_", "\\_", StringComparison.Ordinal);
}
