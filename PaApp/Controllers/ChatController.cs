using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Mvc;
using PaApp.Models.Api;
using PaApp.Services;
using PaApp.Services.Gemini;

namespace PaApp.Controllers;

[ApiController]
[Route("api/chat")]
public sealed class ChatController(
    IChatStore chat,
    IGeminiChatOrchestrator orchestrator,
    IScoutTurnContext scoutTurn,
    ILogger<ChatController> log) : ControllerBase
{
    [HttpPost("sessions")]
    public async Task<ActionResult<CreateSessionResponse>> CreateSessionAsync(
        [FromBody] CreateSessionRequest? body,
        CancellationToken cancellationToken)
    {
        var title = string.IsNullOrWhiteSpace(body?.Title) ? "New briefing" : body.Title.Trim();
        var id = await chat.CreateSessionAsync(title, cancellationToken).ConfigureAwait(false);
        return Ok(new CreateSessionResponse(id, title));
    }

    [HttpGet("sessions")]
    public async Task<ActionResult<IReadOnlyList<SessionSummaryDto>>> ListSessionsAsync(
        [FromQuery] int take = 30,
        CancellationToken cancellationToken = default)
    {
        var rows = await chat.ListSessionsAsync(take, cancellationToken).ConfigureAwait(false);
        var dto = rows.Select(r => new SessionSummaryDto(r.Id, r.Title, r.CreatedAt, r.UpdatedAt)).ToList();
        return Ok(dto);
    }

    [HttpGet("sessions/{sessionId:guid}/messages")]
    public async Task<ActionResult<IReadOnlyList<ChatMessageDto>>> GetMessagesAsync(
        Guid sessionId,
        CancellationToken cancellationToken = default)
    {
        var rows = await chat.GetMessagesAsync(sessionId, cancellationToken).ConfigureAwait(false);
        var list = rows
            .Where(r => r.Role is "user" or "assistant")
            .Select(r => new ChatMessageDto(r.Role, r.Content))
            .ToList();
        return Ok(list);
    }

    [HttpGet("sessions/{sessionId:guid}/stage")]
    public async Task<IActionResult> GetStageAsync(Guid sessionId, CancellationToken cancellationToken = default)
    {
        var raw = await chat.GetStageJsonAsync(sessionId, cancellationToken).ConfigureAwait(false);
        JsonNode? node = string.IsNullOrWhiteSpace(raw) ? null : JsonNode.Parse(raw);
        return Ok(new { stage = node });
    }

    [HttpPost("sessions/{sessionId:guid}/messages")]
    public async Task<IActionResult> SendAsync(
        Guid sessionId,
        [FromBody] SendMessageRequest body,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(body.Text))
            return BadRequest(new { error = "text is required." });

        scoutTurn.SessionId = sessionId;

        var text = body.Text.Trim();
        await chat.AddMessageAsync(sessionId, "user", text, null, cancellationToken).ConfigureAwait(false);

        var prior = await chat.GetMessagesAsync(sessionId, cancellationToken).ConfigureAwait(false);
        if (prior.Count(m => m.Role == "user") == 1)
        {
            var autoTitle = text.Length > 80 ? text[..80] + "…" : text;
            await chat.SetSessionTitleAsync(sessionId, autoTitle, cancellationToken).ConfigureAwait(false);
        }

        await chat.TouchSessionAsync(sessionId, cancellationToken).ConfigureAwait(false);

        string reply;
        try
        {
            reply = await orchestrator.CompleteTurnAsync(sessionId, text, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            log.LogError(ex, "Gemini turn failed.");
            return StatusCode(StatusCodes.Status502BadGateway, new { error = ex.Message });
        }

        await chat.AddMessageAsync(sessionId, "assistant", reply, null, cancellationToken).ConfigureAwait(false);
        await chat.TouchSessionAsync(sessionId, cancellationToken).ConfigureAwait(false);

        var stageRaw = await chat.GetStageJsonAsync(sessionId, cancellationToken).ConfigureAwait(false);
        JsonNode? stageNode = string.IsNullOrWhiteSpace(stageRaw) ? null : JsonNode.Parse(stageRaw);
        return Ok(new SendMessageResponse(reply, stageNode));
    }
}
