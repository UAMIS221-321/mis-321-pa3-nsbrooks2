using Microsoft.AspNetCore.Mvc;
using MySqlConnector;
using PaApp.Configuration;
using PaApp.Services;

namespace PaApp.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class HealthController(IMySqlConnectionFactory connectionFactory, IConfiguration configuration)
    : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<object>> GetAsync(CancellationToken cancellationToken)
    {
        var configured = !string.IsNullOrWhiteSpace(ConnectionStringResolver.Resolve(configuration));
        if (!configured)
            return Ok(new { status = "ok", configured = false, database = "not_configured" });

        try
        {
            await using var conn = await connectionFactory.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
            await using var cmd = new MySqlCommand("SELECT 1;", conn);
            var scalar = await cmd.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
            var dbOk = Equals(scalar, 1) || scalar is long l && l == 1;
            return Ok(new { status = "ok", configured = true, database = dbOk ? "reachable" : "unreachable" });
        }
        catch
        {
            return Ok(new { status = "ok", configured = true, database = "unreachable" });
        }
    }
}
