using Microsoft.AspNetCore.Mvc;
using MySql.Data.MySqlClient;
using System.Text.Json;
using TrailScout.Models;
using TrailScout.Services;

namespace TrailScout.Controllers
{
    [ApiController]
    [Route("api")]
    public class TrailsController : ControllerBase
    {
        private readonly MySqlConnection _connection;
        private readonly GeminiService _geminiService;

        public TrailsController(MySqlConnection connection)
        {
            _connection = connection;
            _geminiService = new GeminiService();
        }

        [HttpGet("health")]
        public async Task<IActionResult> HealthCheck()
        {
            var status = new
            {
                MySql = "Unknown",
                Gemini = "Unknown",
                Timestamp = DateTime.UtcNow
            };

            // Check MySQL
            string mysqlStatus;
            try
            {
                await _connection.OpenAsync();
                mysqlStatus = "Connected";
                await _connection.CloseAsync();
            }
            catch (Exception ex)
            {
                mysqlStatus = $"Error: {ex.Message}";
            }

            // Check Gemini API Key
            string geminiStatus;
            string apiKey = Environment.GetEnvironmentVariable("GEMINI_API_KEY") ?? "";
            if (string.IsNullOrEmpty(apiKey))
            {
                geminiStatus = "Error: GEMINI_API_KEY is missing";
            }
            else
            {
                try
                {
                    // Minimal connectivity test
                    var result = await _geminiService.ChatAsync("Ping", new List<ChatMessage>(), new List<Trail>());
                    geminiStatus = !string.IsNullOrEmpty(result) ? "Connected" : "Error: No response from Gemini";
                }
                catch (Exception ex)
                {
                    geminiStatus = $"Error: {ex.Message}";
                }
            }

            return Ok(new { MySql = mysqlStatus, Gemini = geminiStatus, Timestamp = DateTime.UtcNow });
        }

        [HttpGet("trails")]
        public async Task<IActionResult> GetTrails()
        {
            var trails = new List<Trail>();
            try
            {
                await _connection.OpenAsync();
                var query = "SELECT * FROM trails";
                using var command = new MySqlCommand(query, _connection);
                using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    trails.Add(new Trail
                    {
                        Id = reader.GetString("id"),
                        Name = reader.GetString("name"),
                        Location = reader.GetString("location"),
                        Difficulty = reader.GetString("difficulty"),
                        DistanceMiles = reader.GetDouble("distanceMiles"),
                        ElevationGainFeet = reader.GetInt32("elevationGainFeet"),
                        Features = reader.GetString("features"),
                        Description = reader.GetString("description"),
                        ScenicViews = reader.GetBoolean("scenicViews"),
                        Waterfalls = reader.GetBoolean("waterfalls"),
                        Lakes = reader.GetBoolean("lakes"),
                        CrowdLevel = reader.GetString("crowdLevel")
                    });
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, ex.Message);
            }
            finally
            {
                await _connection.CloseAsync();
            }
            return Ok(trails);
        }

        [HttpPost("chat")]
        public async Task<IActionResult> Chat([FromBody] ChatRequest request)
        {
            var trails = await GetTrailsListInternal();
            var response = await _geminiService.ChatAsync(request.Message, request.History, trails);
            return Ok(new { response });
        }

        [HttpPost("save-trail")]
        public async Task<IActionResult> SaveTrail([FromBody] JsonElement body)
        {
            if (!body.TryGetProperty("trailId", out var trailIdProp))
            {
                return BadRequest("trailId is required");
            }
            
            string trailId = trailIdProp.GetString();
            try
            {
                if (_connection.State != System.Data.ConnectionState.Open)
                    await _connection.OpenAsync();
                    
                var query = "INSERT INTO saved_trails (trailId) VALUES (@trailId) ON DUPLICATE KEY UPDATE trailId=trailId";
                using var command = new MySqlCommand(query, _connection);
                command.Parameters.AddWithValue("@trailId", trailId);
                await command.ExecuteNonQueryAsync();
                return Ok(new { success = true });
            }
            catch (Exception ex)
            {
                return StatusCode(500, ex.Message);
            }
            finally
            {
                if (_connection.State == System.Data.ConnectionState.Open)
                    await _connection.CloseAsync();
            }
        }

        [HttpGet("saved-trails")]
        public async Task<IActionResult> GetSavedTrails()
        {
            var savedIds = new List<string>();
            try
            {
                await _connection.OpenAsync();
                var query = "SELECT trailId FROM saved_trails";
                using var command = new MySqlCommand(query, _connection);
                using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    savedIds.Add(reader.GetString("trailId"));
                }
                return Ok(new { savedTrailIds = savedIds });
            }
            catch (Exception ex)
            {
                return StatusCode(500, ex.Message);
            }
            finally
            {
                await _connection.CloseAsync();
            }
        }

        private async Task<List<Trail>> GetTrailsListInternal()
        {
            var trails = new List<Trail>();
            try
            {
                if (_connection.State != System.Data.ConnectionState.Open)
                    await _connection.OpenAsync();
                
                using var command = new MySqlCommand("SELECT * FROM trails", _connection);
                using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    trails.Add(new Trail
                    {
                        Id = reader.GetString("id"),
                        Name = reader.GetString("name"),
                        Location = reader.GetString("location"),
                        Difficulty = reader.GetString("difficulty"),
                        DistanceMiles = reader.GetDouble("distanceMiles"),
                        Description = reader.GetString("description"),
                        ScenicViews = reader.GetBoolean("scenicViews"),
                        Waterfalls = reader.GetBoolean("waterfalls"),
                        Lakes = reader.GetBoolean("lakes")
                    });
                }
            }
            finally 
            { 
                if (_connection.State == System.Data.ConnectionState.Open)
                    await _connection.CloseAsync(); 
            }
            return trails;
        }
    }
}
