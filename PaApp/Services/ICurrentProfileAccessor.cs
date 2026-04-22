namespace PaApp.Services;

/// <summary>Reads the signed-in social profile id from the <c>ss_profile</c> cookie.</summary>
public interface ICurrentProfileAccessor
{
    Guid? TryGetProfileId();
}
