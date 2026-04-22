using PaApp.Models.Api;

namespace PaApp.Services;

public interface ISocialStore
{
    Task<IReadOnlyList<SocialProfileListItemDto>> ListProfilesAsync(CancellationToken cancellationToken = default);

    Task<SocialProfileDetailDto?> GetProfileAsync(Guid profileId, Guid? viewerId, CancellationToken cancellationToken = default);

    Task<bool> ProfileExistsAsync(Guid profileId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<FeedPostDto>> GetFeedAsync(Guid viewerId, int take, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TrailCardDto>> ListExploreTrailsAsync(int take, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<FeedPostDto>> GetProfilePostsAsync(Guid profileId, int take, CancellationToken cancellationToken = default);

    Task FollowAsync(Guid followerId, Guid followingId, CancellationToken cancellationToken = default);

    Task UnfollowAsync(Guid followerId, Guid followingId, CancellationToken cancellationToken = default);

    Task<bool> IsFollowingAsync(Guid followerId, Guid followingId, CancellationToken cancellationToken = default);
}
