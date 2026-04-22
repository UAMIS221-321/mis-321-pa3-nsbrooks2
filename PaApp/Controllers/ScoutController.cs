using System.Net.Mime;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using MySqlConnector;
using PaApp.Services;

namespace PaApp.Controllers;

[ApiController]
[Route("api/scout")]
public sealed class ScoutController(IMySqlConnectionFactory db) : ControllerBase
{
    [HttpGet("share/{token}")]
    public async Task<IActionResult> ShareItineraryAsync(string token, CancellationToken cancellationToken = default)
    {
        if (token.Length is < 12 or > 32)
            return NotFound();

        await using var conn = await db.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var cmd = new MySqlCommand(
            """
            SELECT trail_name, planner_name, route_summary, planned_start, planned_return,
                   emergency_contact_name, emergency_contact_channel, formal_body, created_at
            FROM safety_itineraries
            WHERE share_token = @t
            LIMIT 1;
            """,
            conn);
        cmd.Parameters.AddWithValue("@t", token);

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            return NotFound();

        var trail = reader.GetString(0);
        var planner = reader.GetString(1);
        var route = reader.GetString(2);
        var ps = reader.IsDBNull(3) ? "" : reader.GetDateTime(3).ToString("u");
        var pr = reader.IsDBNull(4) ? "" : reader.GetDateTime(4).ToString("u");
        var ecName = reader.GetString(5);
        var ecCh = reader.GetString(6);
        var body = reader.GetString(7);
        DateTimeOffset created;
        try
        {
            created = reader.GetDateTimeOffset(8);
        }
        catch
        {
            created = new DateTimeOffset(reader.GetDateTime(8), TimeSpan.Zero);
        }

        static string H(string? s) => System.Net.WebUtility.HtmlEncode(s ?? "");

        var html = new StringBuilder();
        html.Append("<!DOCTYPE html><html lang=\"en\"><head><meta charset=\"utf-8\"/>");
        html.Append("<title>SummitScout Itinerary — ").Append(H(trail)).Append("</title>");
        html.Append("<style>");
        html.Append("body{font-family:ui-sans-serif,system-ui,sans-serif;background:#1a1f1c;color:#e8ebe6;padding:2rem;max-width:52rem;margin:0 auto;line-height:1.5;}");
        html.Append("h1{font-size:1.15rem;letter-spacing:.08em;text-transform:uppercase;color:#9fb3a8;}");
        html.Append(".card{border:1px solid #2d3832;background:#222a26;padding:1.25rem;margin-top:1rem;}");
        html.Append("pre{white-space:pre-wrap;font-family:ui-monospace,monospace;font-size:.85rem;color:#c9d4ce;}");
        html.Append(".meta{font-size:.8rem;color:#7a8f84;}");
        html.Append("</style></head><body>");
        html.Append("<h1>SummitScout — Read-only safety itinerary</h1>");
        html.Append("<p class=\"meta\">Issued ").Append(H(created.ToString("u"))).Append("</p>");
        html.Append("<div class=\"card\">");
        html.Append("<p><strong>Trail</strong> — ").Append(H(trail)).Append("</p>");
        html.Append("<p><strong>Planner</strong> — ").Append(H(planner)).Append("</p>");
        html.Append("<p><strong>Route</strong> — ").Append(H(route)).Append("</p>");
        html.Append("<p><strong>Start</strong> — ").Append(H(ps)).Append(" &nbsp; <strong>Return</strong> — ").Append(H(pr)).Append("</p>");
        html.Append("<p><strong>Emergency contact</strong> — ").Append(H(ecName)).Append(" / ").Append(H(ecCh)).Append("</p>");
        html.Append("</div><div class=\"card\"><pre>").Append(H(body)).Append("</pre></div></body></html>");

        return Content(html.ToString(), MediaTypeNames.Text.Html);
    }
}
