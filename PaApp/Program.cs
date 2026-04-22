using System.Reflection;
using Microsoft.AspNetCore.HttpOverrides;
using PaApp.Services;
using PaApp.Services.Gemini;

// Heroku: Procfile may run `dotnet PaApp/bin/publish/PaApp.dll` with cwd `/app`, so wwwroot is not at `/app/wwwroot`.
// Use the folder that contains the entry DLL (publish output) as the content root.
try
{
    var entry = Assembly.GetEntryAssembly();
    var dllDir = Path.GetDirectoryName(entry?.Location);
    if (!string.IsNullOrEmpty(dllDir)
        && Directory.Exists(Path.Combine(dllDir, "wwwroot"))
        && !Directory.Exists(Path.Combine(Directory.GetCurrentDirectory(), "wwwroot")))
    {
        Directory.SetCurrentDirectory(dllDir);
    }
}
catch
{
    // keep default cwd
}

var builder = WebApplication.CreateBuilder(args);

// Heroku (and similar hosts) assign a dynamic PORT; Docker / Procfile may also rely on this.
var port = Environment.GetEnvironmentVariable("PORT");
if (!string.IsNullOrWhiteSpace(port))
    builder.WebHost.UseUrls($"http://0.0.0.0:{port}");

// Behind Heroku’s router the app sees HTTP while clients use HTTPS — restore correct scheme for redirects and cookies.
if (!builder.Environment.IsDevelopment())
{
    builder.Services.Configure<ForwardedHeadersOptions>(options =>
    {
        options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
        options.KnownNetworks.Clear();
        options.KnownProxies.Clear();
    });
}

builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton<IMySqlConnectionFactory, MySqlConnectionFactory>();
builder.Services.AddScoped<ICurrentProfileAccessor, CurrentProfileAccessor>();
builder.Services.AddScoped<ISocialStore, SocialStore>();
builder.Services.AddScoped<IChatStore, ChatStore>();
builder.Services.AddScoped<IKnowledgeSearchService, KnowledgeSearchService>();
builder.Services.AddScoped<IScoutTurnContext, ScoutTurnContext>();
builder.Services.AddScoped<IDatabaseToolService, ScoutToolService>();
builder.Services.AddScoped<IGeminiChatOrchestrator, GeminiChatOrchestrator>();

builder.Services.AddOptions<GeminiOptions>()
    .Bind(builder.Configuration.GetSection(GeminiOptions.SectionName))
    .PostConfigure(o =>
    {
        var envKey = Environment.GetEnvironmentVariable("GEMINI_API_KEY")
                     ?? Environment.GetEnvironmentVariable("GOOGLE_API_KEY");
        if (!string.IsNullOrWhiteSpace(envKey))
            o.ApiKey = envKey;

        var envModel = Environment.GetEnvironmentVariable("GEMINI_MODEL");
        if (!string.IsNullOrWhiteSpace(envModel))
            o.Model = envModel.Trim();
        else if (string.IsNullOrWhiteSpace(o.Model))
            o.Model = "gemini-2.5-flash";

        var envGround = Environment.GetEnvironmentVariable("GEMINI_USE_GOOGLE_SEARCH");
        if (!string.IsNullOrWhiteSpace(envGround) && bool.TryParse(envGround, out var useGround))
            o.UseGoogleSearch = useGround;
    });

builder.Services.AddHttpClient<IGeminiApiClient, GeminiApiClient>(client =>
{
    client.BaseAddress = new Uri("https://generativelanguage.googleapis.com/");
    client.Timeout = TimeSpan.FromMinutes(2);
});

builder.Services.AddHttpClient("openmeteo", client =>
{
    client.BaseAddress = new Uri("https://api.open-meteo.com/");
    client.Timeout = TimeSpan.FromSeconds(30);
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

if (!app.Environment.IsDevelopment())
{
    app.UseForwardedHeaders();
}

app.UseDefaultFiles();
app.UseStaticFiles();

// With the "http" launch profile (HTTP only), HTTPS redirection breaks same-origin /api fetch calls
// because static files are served over HTTP first, then XHR is redirected to an HTTPS URL that is not listening.
if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

app.UseAuthorization();
app.MapControllers();
app.MapFallbackToFile("index.html");

app.Run();
