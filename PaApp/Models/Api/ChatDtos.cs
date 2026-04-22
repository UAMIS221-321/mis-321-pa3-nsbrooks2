using System.Text.Json.Nodes;

namespace PaApp.Models.Api;

public sealed record CreateSessionRequest(string? Title);

public sealed record CreateSessionResponse(Guid Id, string Title);

public sealed record SessionSummaryDto(Guid Id, string Title, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);

public sealed record ChatMessageDto(string Role, string Content);

public sealed record SendMessageRequest(string Text);

public sealed record SendMessageResponse(string Reply, JsonNode? Stage);
