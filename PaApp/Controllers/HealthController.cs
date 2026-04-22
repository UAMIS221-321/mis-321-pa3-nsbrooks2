using Microsoft.AspNetCore.Mvc;
using MySqlConnector;
using PaApp.Services;

namespace PaApp.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class HealthController(IMySqlConnectionFactory connectionFactory) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<object>> GetAsync(CancellationToken cancellationToken)
    {
        var dbOk = false;
        try
        {
            await using var conn = await connectionFactory.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
            await using var cmd = new MySqlCommand("SELECT 1;", conn);
            var scalar = await cmd.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
            dbOk = Equals(scalar, 1) || scalar is long l && l == 1;
        }
        catch
        {
            dbOk = false;
        }

        return Ok(new { status = "ok", database = dbOk ? "reachable" : "unconfigured_or_unreachable" });
    }
}
