namespace PaApp.Models;

public sealed class ChatMessage
{
    public long Id { get; init; }
    public Guid SessionId { get; init; }
    public string Role { get; init; } = string.Empty;
    public string Content { get; init; } = string.Empty;
    public string? MetadataJson { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
}
