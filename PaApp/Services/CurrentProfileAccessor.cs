namespace PaApp.Services;

public sealed class CurrentProfileAccessor(IHttpContextAccessor httpContextAccessor) : ICurrentProfileAccessor
{
    public const string CookieName = "ss_profile";

    public Guid? TryGetProfileId()
    {
        var ctx = httpContextAccessor.HttpContext;
        if (ctx?.Request.Cookies.TryGetValue(CookieName, out var raw) != true ||
            string.IsNullOrWhiteSpace(raw) ||
            !Guid.TryParse(raw, out var id))
        {
            return null;
        }

        return id;
    }
}
