namespace PaApp.Models;

public sealed class ChatSession
{
    public Guid Id { get; init; }
    public string Title { get; init; } = string.Empty;
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset UpdatedAt { get; init; }
}
