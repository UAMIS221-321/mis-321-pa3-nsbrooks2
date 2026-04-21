using DotNetEnv;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MySql.Data.MySqlClient;

Env.Load();

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Configure MySQL connection
string connectionString = Environment.GetEnvironmentVariable("MYSQL_CONNECTION_STRING") 
    ?? "Server=localhost;Database=trailscout;Uid=root;Pwd=SecretPassword";
builder.Services.AddTransient<MySqlConnection>(_ => new MySqlConnection(connectionString));

// Configure CORS - restrict this in production (e.g. policy.WithOrigins("https://yourdomain.com"))
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// Configure Kestrel to run on the port provided by Heroku (dynamic) or default to 3000
var port = Environment.GetEnvironmentVariable("PORT") ?? "3000";
builder.WebHost.ConfigureKestrel(options =>
{
    if (int.TryParse(port, out int p)) {
        options.ListenAnyIP(p);
    } else {
        options.ListenAnyIP(3000);
    }
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("AllowAll");
app.UseStaticFiles(); // Serves the HTML frontend
app.UseRouting();

app.MapControllers();
app.MapFallbackToFile("index.html"); // SPA fallback

app.Run();
