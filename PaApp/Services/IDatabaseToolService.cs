using System.Text.Json.Nodes;

namespace PaApp.Services;

public interface IDatabaseToolService
{
    Task<JsonObject> ExecuteAsync(string functionName, JsonObject? arguments, CancellationToken cancellationToken = default);
}
