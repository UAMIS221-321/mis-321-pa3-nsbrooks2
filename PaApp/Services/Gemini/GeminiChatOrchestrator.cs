using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace PaApp.Services.Gemini;

public interface IGeminiChatOrchestrator
{
    Task<string> CompleteTurnAsync(Guid sessionId, string userMessage, CancellationToken cancellationToken = default);
}

public sealed class GeminiChatOrchestrator : IGeminiChatOrchestrator
{
    private const int MaxToolIterations = 8;

    private readonly IGeminiApiClient _api;
    private readonly IKnowledgeSearchService _rag;
    private readonly IDatabaseToolService _tools;
    private readonly IChatStore _chat;
    private readonly GeminiOptions _options;
    private readonly ILogger<GeminiChatOrchestrator> _logger;

    public GeminiChatOrchestrator(
        IGeminiApiClient api,
        IKnowledgeSearchService rag,
        IDatabaseToolService tools,
        IChatStore chat,
        IOptions<GeminiOptions> options,
        ILogger<GeminiChatOrchestrator> logger)
    {
        _api = api;
        _rag = rag;
        _tools = tools;
        _chat = chat;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<string> CompleteTurnAsync(Guid sessionId, string userMessage, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.ApiKey))
            throw new InvalidOperationException("Set GEMINI_API_KEY (or GOOGLE_API_KEY) to enable chat.");

        var ragDocs = await _rag.SearchAsync(userMessage, 5, cancellationToken).ConfigureAwait(false);
        var ragBlock = FormatRag(ragDocs);

        var webResearchFooter = string.Empty;
        var webResearchSummary = string.Empty;
        if (_options.UseGoogleSearch)
        {
            (webResearchSummary, webResearchFooter) =
                await RunWebResearchPassAsync(userMessage, cancellationToken).ConfigureAwait(false);
        }

        var webResearchBlock = string.IsNullOrWhiteSpace(webResearchSummary)
            ? "(No web supplement for this turn — rely on RAG, tools, and careful general knowledge.)"
            : webResearchSummary;

        var systemText = $"""
            You are SummitScout AI — a conversational hiking and trail assistant (ChatGPT-style helpfulness) with a safety-first mountain-guide mindset.

            **Web research (Google Search)** — when enabled, a separate search-only pass already ran for this user message. Treat the block below as fresh web context; integrate it into your answer.
            Prefer it over guessing for real trail names, parking, fees, closures, and seasonal hazards. Cite or link sources when that context includes URLs.

            ### Web research brief (may be empty)
            {webResearchBlock}

            **SummitScout tools (database + Open-Meteo)** — use when they fit:
            - `search_trails`: ONLY our curated MySQL catalog (demo rows). Never invent catalog trails; if the catalog is empty for their area, say so and use the web brief above when available.
            - `get_trailhead_weather`: hourly forecast at coordinates or a catalog trailhead.
            - `upsert_gear_checklist` / `create_safety_itinerary`: session gear panel and shareable emergency-contact itineraries.

            Tools attach to the active Mission Control session automatically — never ask the user for a session id.

            ### Retrieved knowledge excerpts (local encyclopedia — supplement with web when needed)
            {ragBlock}
            """;

        var history = await _chat.GetMessagesAsync(sessionId, cancellationToken).ConfigureAwait(false);
        var contents = new JsonArray();
        foreach (var row in history)
        {
            var role = row.Role switch
            {
                "assistant" => "model",
                "user" => "user",
                _ => null
            };
            if (role is null)
                continue;

            contents.Add(new JsonObject
            {
                ["role"] = role,
                ["parts"] = new JsonArray(new JsonObject { ["text"] = row.Content })
            });
        }

        for (var iteration = 0; iteration < MaxToolIterations; iteration++)
        {
            // DeepClone: JsonNode can only have one parent. The same `contents` grows each tool round, and
            // `ToolsArray` is a static singleton — both must be cloned per request, not re-attached to a new body.
            var body = new JsonObject
            {
                ["systemInstruction"] = new JsonObject
                {
                    ["parts"] = new JsonArray(new JsonObject { ["text"] = systemText })
                },
                ["contents"] = contents.DeepClone(),
                ["tools"] = GeminiToolDefinitions.BuildFunctionToolsOnly(),
                ["generationConfig"] = new JsonObject
                {
                    ["temperature"] = 0.55
                }
            };

            using var doc = await _api.GenerateContentAsync(body, cancellationToken).ConfigureAwait(false);
            var root = doc.RootElement;

            if (root.TryGetProperty("promptFeedback", out var fb) &&
                fb.TryGetProperty("blockReason", out var br))
            {
                return $"The model blocked this prompt ({br.GetString()}). Try rephrasing.";
            }

            if (!root.TryGetProperty("candidates", out var candidates) || candidates.GetArrayLength() == 0)
                return "No response from Gemini (empty candidates). Check API key, model name, and quota.";

            var candidate = candidates[0];
            if (candidate.TryGetProperty("finishReason", out var fr) &&
                fr.GetString() is "SAFETY" or "RECITATION" or "OTHER")
            {
                return $"Generation stopped ({fr.GetString()}).";
            }

            if (!candidate.TryGetProperty("content", out var contentEl))
                return "Malformed Gemini response (missing content).";

            var calls = new List<FunctionCallData>();
            var textParts = new List<string>();
            ExtractParts(contentEl, calls, textParts);

            if (calls.Count == 0)
            {
                var text = textParts.Count > 0 ? string.Join("", textParts) : "No text returned from the model.";
                return text + webResearchFooter + FormatGroundingFooter(candidate);
            }

            var modelNode = JsonNode.Parse(contentEl.GetRawText())!.AsObject();
            if (!modelNode.TryGetPropertyValue("role", out var roleNode) ||
                string.IsNullOrWhiteSpace(roleNode?.GetValue<string>()))
            {
                modelNode["role"] = "model";
            }

            contents.Add(modelNode);

            var responseParts = new JsonArray();
            foreach (var call in calls)
            {
                var result = await _tools.ExecuteAsync(call.Name, call.Args, cancellationToken).ConfigureAwait(false);
                var functionResponse = new JsonObject
                {
                    ["name"] = call.Name,
                    ["response"] = result
                };
                if (!string.IsNullOrEmpty(call.Id))
                    functionResponse["id"] = call.Id;

                responseParts.Add(new JsonObject { ["functionResponse"] = functionResponse });
            }

            contents.Add(new JsonObject
            {
                ["role"] = "user",
                ["parts"] = responseParts
            });
        }

        return "Stopped after too many tool rounds (possible loop).";
    }

    /// <summary>
    /// Gemini does not allow <c>google_search</c> and custom <c>functionDeclarations</c> in one request; this pass is search-only.
    /// </summary>
    private async Task<(string Summary, string Footer)> RunWebResearchPassAsync(
        string userMessage,
        CancellationToken cancellationToken)
    {
        const string researchSystem = """
            Use Google Search. Write a compact research brief (max 400 words) for a hiking assistant answering the user's latest message.
            Include current conditions, official or reputable sources, closures, parking or fees when found, and cite URLs when grounding provides them.
            If the message is purely conversational with no outdoor or trail question, reply in one short sentence that no web research was needed.
            """;

        var body = new JsonObject
        {
            ["systemInstruction"] = new JsonObject
            {
                ["parts"] = new JsonArray(new JsonObject { ["text"] = researchSystem })
            },
            ["contents"] = new JsonArray(new JsonObject
            {
                ["role"] = "user",
                ["parts"] = new JsonArray(new JsonObject { ["text"] = userMessage })
            }),
            ["tools"] = GeminiToolDefinitions.BuildGoogleSearchToolsOnly(),
            ["generationConfig"] = new JsonObject { ["temperature"] = 0.35 }
        };

        try
        {
            using var doc = await _api.GenerateContentAsync(body, cancellationToken).ConfigureAwait(false);
            var root = doc.RootElement;

            if (root.TryGetProperty("promptFeedback", out var fb) &&
                fb.TryGetProperty("blockReason", out _))
            {
                return (string.Empty, string.Empty);
            }

            if (!root.TryGetProperty("candidates", out var candidates) || candidates.GetArrayLength() == 0)
                return (string.Empty, string.Empty);

            var candidate = candidates[0];
            if (!candidate.TryGetProperty("content", out var contentEl))
                return (string.Empty, string.Empty);

            var calls = new List<FunctionCallData>();
            var textParts = new List<string>();
            ExtractParts(contentEl, calls, textParts);
            if (calls.Count > 0)
                _logger.LogWarning("Web research pass returned function calls; ignoring tool output.");

            var summary = textParts.Count > 0 ? string.Join("", textParts).Trim() : string.Empty;
            var footer = FormatGroundingFooter(candidate);
            return (summary, footer);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Google Search research pass failed; continuing without web supplement.");
            return (string.Empty, string.Empty);
        }
    }

    private static void ExtractParts(JsonElement contentEl, List<FunctionCallData> calls, List<string> textParts)
    {
        if (!contentEl.TryGetProperty("parts", out var parts))
            return;

        foreach (var part in parts.EnumerateArray())
        {
            if (part.TryGetProperty("text", out var text))
                textParts.Add(text.GetString() ?? string.Empty);

            if (!part.TryGetProperty("functionCall", out var fc))
                continue;

            var name = fc.GetProperty("name").GetString() ?? string.Empty;
            var id = fc.TryGetProperty("id", out var idEl) ? idEl.GetString() : null;
            JsonObject args;
            if (fc.TryGetProperty("args", out var argsEl) && argsEl.ValueKind == JsonValueKind.Object)
                args = JsonNode.Parse(argsEl.GetRawText())!.AsObject();
            else
                args = new JsonObject();

            calls.Add(new FunctionCallData(name, id, args));
        }
    }

    private static string FormatGroundingFooter(JsonElement candidate)
    {
        if (!candidate.TryGetProperty("groundingMetadata", out var gm))
            return string.Empty;

        var lines = new List<string>();

        if (gm.TryGetProperty("webSearchQueries", out var queries) && queries.ValueKind == JsonValueKind.Array)
        {
            var qs = queries.EnumerateArray()
                .Select(e => e.GetString())
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .ToArray();
            if (qs.Length > 0)
                lines.Add("Web search used: " + string.Join(" · ", qs));
        }

        if (gm.TryGetProperty("groundingChunks", out var chunks) && chunks.ValueKind == JsonValueKind.Array)
        {
            var urls = new List<string>();
            foreach (var ch in chunks.EnumerateArray())
            {
                if (ch.TryGetProperty("web", out var web) && web.TryGetProperty("uri", out var uri) &&
                    uri.ValueKind == JsonValueKind.String)
                {
                    var u = uri.GetString();
                    if (!string.IsNullOrWhiteSpace(u))
                        urls.Add(u!);
                }
            }

            if (urls.Count > 0)
                lines.Add("Sources: " + string.Join(" · ", urls.Distinct(StringComparer.Ordinal).Take(10)));
        }

        if (lines.Count == 0)
            return string.Empty;

        return "\n\n---\n" + string.Join("\n", lines);
    }

    private static string FormatRag(IReadOnlyList<Models.KnowledgeDocument> docs)
    {
        if (docs.Count == 0)
            return "(none — answer from general reasoning and tools as appropriate.)";

        var sb = new System.Text.StringBuilder();
        foreach (var d in docs)
        {
            var excerpt = d.Content.Length <= 700 ? d.Content : d.Content[..700] + "…";
            sb.Append("- **").Append(d.Title).Append("** (id ").Append(d.Id).Append("): ").AppendLine(excerpt);
            sb.AppendLine();
        }

        return sb.ToString().TrimEnd();
    }

    private sealed record FunctionCallData(string Name, string? Id, JsonObject Args);
}
