using System.Text.Json.Nodes;

namespace PaApp.Services.Gemini;

public static class GeminiToolDefinitions
{
    /// <summary>Custom SQL / Open-Meteo tools (cloned per request).</summary>
    private static readonly JsonObject FunctionDeclarationsTool = BuildFunctionDeclarationsTool();

    /// <summary>Gemini built-in web grounding (see Google "Grounding with Google Search").</summary>
    private static readonly JsonObject GoogleSearchGroundingTool = new()
    {
        ["google_search"] = new JsonObject()
    };

    /// <summary>
    /// Custom function tools only. Gemini rejects <c>google_search</c> and <c>functionDeclarations</c> in the same request.
    /// </summary>
    public static JsonArray BuildFunctionToolsOnly() =>
        new JsonArray(FunctionDeclarationsTool.DeepClone());

    /// <summary>Built-in Google Search grounding only (separate generateContent call from function tools).</summary>
    public static JsonArray BuildGoogleSearchToolsOnly() =>
        new JsonArray(GoogleSearchGroundingTool.DeepClone());

    private static JsonObject BuildFunctionDeclarationsTool()
    {
        return new JsonObject
        {
            ["functionDeclarations"] = new JsonArray(
                new JsonObject
                {
                    ["name"] = "search_trails",
                    ["description"] =
                        "Search ONLY the SummitScout MySQL trail catalog (demo + curated rows). Use for filters like dog-friendly, elevation cap, weekday crowd hints. For real-world trail intel outside the catalog, use the Web research section in the system prompt when present—do not pretend catalog rows exist for places not returned here.",
                    ["parameters"] = new JsonObject
                    {
                        ["type"] = "object",
                        ["properties"] = new JsonObject
                        {
                            ["dog_friendly"] = new JsonObject
                            {
                                ["type"] = "boolean",
                                ["description"] = "If true, only trails that allow dogs on the main route."
                            },
                            ["max_elevation_gain_ft"] = new JsonObject
                            {
                                ["type"] = "integer",
                                ["description"] = "Maximum total elevation gain in feet (e.g. 1000)."
                            },
                            ["preferred_weekday"] = new JsonObject
                            {
                                ["type"] = "string",
                                ["description"] =
                                    "Weekday name to prefer for lower crowding (e.g. Tuesday). Matches trail crowd_calendar_note text."
                            },
                            ["difficulty"] = new JsonObject
                            {
                                ["type"] = "string",
                                ["enum"] = new JsonArray("easy", "moderate", "hard"),
                                ["description"] = "Trail difficulty bucket."
                            },
                            ["region_contains"] = new JsonObject
                            {
                                ["type"] = "string",
                                ["description"] = "Substring match on region name (case-insensitive)."
                            }
                        },
                        ["required"] = new JsonArray()
                    }
                },
                new JsonObject
                {
                    ["name"] = "get_trailhead_weather",
                    ["description"] =
                        "Fetch a hyper-local multi-day hourly forecast (Open-Meteo) for a trailhead using database trail id or slug, OR explicit WGS84 coordinates.",
                    ["parameters"] = new JsonObject
                    {
                        ["type"] = "object",
                        ["properties"] = new JsonObject
                        {
                            ["trail_id"] = new JsonObject
                            {
                                ["type"] = "integer",
                                ["description"] = "Primary key from trails table."
                            },
                            ["trail_slug"] = new JsonObject
                            {
                                ["type"] = "string",
                                ["description"] = "URL slug, e.g. cedar-basin-loop."
                            },
                            ["latitude"] = new JsonObject { ["type"] = "number" },
                            ["longitude"] = new JsonObject { ["type"] = "number" }
                        },
                        ["required"] = new JsonArray()
                    }
                },
                new JsonObject
                {
                    ["name"] = "upsert_gear_checklist",
                    ["description"] =
                        "Persist or replace the Mission Control gear checklist for this session. Items are shown on the dynamic stage panel.",
                    ["parameters"] = new JsonObject
                    {
                        ["type"] = "object",
                        ["properties"] = new JsonObject
                        {
                            ["items"] = new JsonObject
                            {
                                ["type"] = "array",
                                ["items"] = new JsonObject { ["type"] = "string" },
                                ["description"] = "Ordered checklist lines (e.g. '32oz water', 'INSULATED layer')."
                            },
                            ["difficulty_tag"] = new JsonObject
                            {
                                ["type"] = "string",
                                ["description"] = "Trail difficulty context for the list (easy|moderate|hard or free text)."
                            }
                        },
                        ["required"] = new JsonArray("items")
                    }
                },
                new JsonObject
                {
                    ["name"] = "create_safety_itinerary",
                    ["description"] =
                        "Generate a formal, shareable safety itinerary for an emergency contact. Stored in MySQL; returns a read-only viewer path.",
                    ["parameters"] = new JsonObject
                    {
                        ["type"] = "object",
                        ["properties"] = new JsonObject
                        {
                            ["trail_name"] = new JsonObject { ["type"] = "string" },
                            ["planner_name"] = new JsonObject { ["type"] = "string" },
                            ["route_summary"] = new JsonObject { ["type"] = "string" },
                            ["planned_start"] = new JsonObject
                            {
                                ["type"] = "string",
                                ["description"] = "ISO-8601 local or UTC timestamp string, optional."
                            },
                            ["planned_return"] = new JsonObject
                            {
                                ["type"] = "string",
                                ["description"] = "ISO-8601 local or UTC timestamp string, optional."
                            },
                            ["emergency_contact_name"] = new JsonObject { ["type"] = "string" },
                            ["emergency_contact_channel"] = new JsonObject
                            {
                                ["type"] = "string",
                                ["description"] = "Phone number, email, or radio channel string."
                            }
                        },
                        ["required"] = new JsonArray(
                            "trail_name",
                            "planner_name",
                            "route_summary",
                            "emergency_contact_name",
                            "emergency_contact_channel")
                    }
                })
        };
    }
}
