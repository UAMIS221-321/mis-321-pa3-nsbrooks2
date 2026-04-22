using Microsoft.AspNetCore.Mvc;
using PaApp.Models.Api;
using PaApp.Services;

namespace PaApp.Controllers;

[ApiController]
[Route("api/social")]
public sealed class SocialController(
    ISocialStore socialStore,
    ICurrentProfileAccessor currentProfile) : ControllerBase
{
    [HttpGet("profiles")]
    public async Task<ActionResult<IReadOnlyList<SocialProfileListItemDto>>> ListProfilesAsync(
        CancellationToken cancellationToken)
    {
        var rows = await socialStore.ListProfilesAsync(cancellationToken).ConfigureAwait(false);
        return Ok(rows);
    }

    [HttpGet("profiles/{profileId:guid}")]
    public async Task<ActionResult<SocialProfileDetailDto>> GetProfileAsync(
        Guid profileId,
        CancellationToken cancellationToken)
    {
        var viewer = currentProfile.TryGetProfileId();
        var row = await socialStore.GetProfileAsync(profileId, viewer, cancellationToken).ConfigureAwait(false);
        if (row is null)
            return NotFound(new { error = "Profile not found." });

        return Ok(row);
    }

    [HttpGet("me")]
    public async Task<ActionResult<object>> MeAsync(CancellationToken cancellationToken)
    {
        var id = currentProfile.TryGetProfileId();
        if (id is null)
            return Ok(new { profile = (SocialProfileDetailDto?)null });

        var row = await socialStore.GetProfileAsync(id.Value, id.Value, cancellationToken).ConfigureAwait(false);
        return Ok(new { profile = row });
    }

    [HttpPost("session")]
    public async Task<ActionResult<object>> SetSessionAsync(
        [FromBody] SetProfileSessionRequest body,
        CancellationToken cancellationToken)
    {
        if (!await socialStore.ProfileExistsAsync(body.ProfileId, cancellationToken).ConfigureAwait(false))
            return NotFound(new { error = "Profile not found." });

        Response.Cookies.Append(
            CurrentProfileAccessor.CookieName,
            body.ProfileId.ToString("D"),
            new CookieOptions
            {
                HttpOnly = true,
                IsEssential = true,
                MaxAge = TimeSpan.FromDays(90),
                Path = "/",
                SameSite = SameSiteMode.Lax,
            });

        var row = await socialStore.GetProfileAsync(body.ProfileId, body.ProfileId, cancellationToken).ConfigureAwait(false);
        return Ok(new { profile = row });
    }

    [HttpPost("logout")]
    public IActionResult Logout()
    {
        Response.Cookies.Delete(
            CurrentProfileAccessor.CookieName,
            new CookieOptions { Path = "/", SameSite = SameSiteMode.Lax, });
        return Ok();
    }

    [HttpGet("feed")]
    public async Task<ActionResult<IReadOnlyList<FeedPostDto>>> FeedAsync(
        [FromQuery] int take = 40,
        CancellationToken cancellationToken = default)
    {
        var id = currentProfile.TryGetProfileId();
        if (id is null)
            return Unauthorized(new { error = "Choose a profile to see your feed." });

        var feed = await socialStore.GetFeedAsync(id.Value, take, cancellationToken).ConfigureAwait(false);
        return Ok(feed);
    }

    [HttpGet("explore/trails")]
    public async Task<ActionResult<IReadOnlyList<TrailCardDto>>> ExploreTrailsAsync(
        [FromQuery] int take = 24,
        CancellationToken cancellationToken = default)
    {
        var trails = await socialStore.ListExploreTrailsAsync(take, cancellationToken).ConfigureAwait(false);
        return Ok(trails);
    }

    [HttpGet("profiles/{profileId:guid}/posts")]
    public async Task<ActionResult<IReadOnlyList<FeedPostDto>>> ProfilePostsAsync(
        Guid profileId,
        [FromQuery] int take = 30,
        CancellationToken cancellationToken = default)
    {
        if (!await socialStore.ProfileExistsAsync(profileId, cancellationToken).ConfigureAwait(false))
            return NotFound(new { error = "Profile not found." });

        var posts = await socialStore.GetProfilePostsAsync(profileId, take, cancellationToken).ConfigureAwait(false);
        return Ok(posts);
    }

    [HttpPost("follow")]
    public async Task<IActionResult> FollowAsync([FromBody] FollowRequest body, CancellationToken cancellationToken)
    {
        var id = currentProfile.TryGetProfileId();
        if (id is null)
            return Unauthorized(new { error = "Choose a profile first." });

        if (!await socialStore.ProfileExistsAsync(body.FollowingId, cancellationToken).ConfigureAwait(false))
            return NotFound(new { error = "Profile not found." });

        await socialStore.FollowAsync(id.Value, body.FollowingId, cancellationToken).ConfigureAwait(false);
        return Ok();
    }

    [HttpDelete("follow/{followingId:guid}")]
    public async Task<IActionResult> UnfollowAsync(Guid followingId, CancellationToken cancellationToken)
    {
        var id = currentProfile.TryGetProfileId();
        if (id is null)
            return Unauthorized(new { error = "Choose a profile first." });

        await socialStore.UnfollowAsync(id.Value, followingId, cancellationToken).ConfigureAwait(false);
        return Ok();
    }
}
