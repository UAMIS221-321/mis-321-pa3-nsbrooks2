using System.Text;
using System.Text.Json;
using TrailScout.Models;

namespace TrailScout.Services
{
    public class GeminiService
    {
        private readonly string _apiKey;
        private readonly HttpClient _httpClient;
        private readonly string _model = "gemini-3-flash-preview";

        public GeminiService()
        {
            _apiKey = Environment.GetEnvironmentVariable("GEMINI_API_KEY") ?? "";
            _httpClient = new HttpClient();
        }

        public async Task<string> ChatAsync(string message, List<ChatMessage> history, List<Trail> trails)
        {
            var url = $"https://generativelanguage.googleapis.com/v1beta/models/{_model}:generateContent?key={_apiKey}";

            var systemInstruction = $@"You are 'SummitScout AI', an expert hiking assistant.
STACK: C# .NET 8 + MySQL.

TRAIL DATABASE (RAG Context):
{JsonSerializer.Serialize(trails)}

USER ABILITIES (Function Calling):
Your response can trigger actions. If the user wants to 'save', 'favorite', or 'bookmark' a specific trail from the list above, you MUST include a JSON block at the END of your response.

FORMAT FOR SAVING:
{{""action"": ""save_trail"", ""trail_id"": ""ID_FROM_CONTEXT""}}

RULES:
1. RAG: Always check the 'TRAIL DATABASE' first. If a trail isn't there, suggest checking local park sites.
2. PERSONALITY: Professional, outdoorsy, and safety-conscious.
3. If multiple trails fit, list them clearly.
4. When you suggest a trail and the user likes it, remind them they can save it to their favorites.";

            var contents = new List<object>();
            
            // Add system instruction as part of the prompt
            contents.Add(new { role = "user", parts = new[] { new { text = $"System Instruction: {systemInstruction}" } } });
            contents.Add(new { role = "model", parts = new[] { new { text = "Understood. I am SummitScout AI, the hiker's expert guide. I have access to your trail database and can help users save their favorite hikes." } } });

            foreach (var h in history)
            {
                contents.Add(new { role = h.Role == "model" ? "model" : "user", parts = new[] { new { text = h.Content } } });
            }

            contents.Add(new { role = "user", parts = new[] { new { text = message } } });

            var requestBody = new { contents = contents };
            var json = JsonSerializer.Serialize(requestBody);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync(url, content);
            var responseString = await response.Content.ReadAsStringAsync();

            using var doc = JsonDocument.Parse(responseString);
            var text = doc.RootElement.GetProperty("candidates")[0].GetProperty("content").GetProperty("parts")[0].GetProperty("text").GetString();

            return text ?? "Error contacting Gemini.";
        }
    }
}
