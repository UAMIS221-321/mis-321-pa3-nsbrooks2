namespace PaApp.Services.Gemini;

public sealed class GeminiOptions
{
    public const string SectionName = "Gemini";

    /// <summary>From env <c>GEMINI_API_KEY</c> or <c>GOOGLE_API_KEY</c> (set in Program.cs).</summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>Model id without the <c>models/</c> prefix (e.g. <c>gemini-2.5-flash</c>). Override with <c>GEMINI_MODEL</c>.</summary>
    public string Model { get; set; } = "gemini-2.5-flash";

    /// <summary>
    /// When true, the chat orchestrator runs a separate Gemini call with <c>google_search</c> only, then injects the brief into the main turn (function tools cannot share the same request as <c>google_search</c>). Disable with <c>Gemini:UseGoogleSearch</c> or env <c>GEMINI_USE_GOOGLE_SEARCH=false</c>.
    /// </summary>
    public bool UseGoogleSearch { get; set; } = true;
}
