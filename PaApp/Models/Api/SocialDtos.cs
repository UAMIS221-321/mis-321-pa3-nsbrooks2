namespace PaApp.Models.Api;

public sealed record SocialProfileListItemDto(
    Guid Id,
    string Handle,
    string DisplayName,
    string Bio,
    int AvatarHue,
    int Followers,
    int Following,
    int Posts);

public sealed record SocialProfileDetailDto(
    Guid Id,
    string Handle,
    string DisplayName,
    string Bio,
    int AvatarHue,
    int Followers,
    int Following,
    int Posts,
    bool IsFollowing,
    bool IsSelf);

public sealed record FeedPostDto(
    long Id,
    string Caption,
    DateTimeOffset CreatedAt,
    Guid AuthorId,
    string AuthorHandle,
    string AuthorDisplayName,
    int AuthorAvatarHue,
    TrailCardDto? Trail);

public sealed record TrailCardDto(
    long Id,
    string Slug,
    string Name,
    string Region,
    string Difficulty,
    bool DogFriendly,
    int ElevationGainFt,
    decimal LengthMi,
    string Excerpt);

public sealed record SetProfileSessionRequest(Guid ProfileId);

public sealed record FollowRequest(Guid FollowingId);
