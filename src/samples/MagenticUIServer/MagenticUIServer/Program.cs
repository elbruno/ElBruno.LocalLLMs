using MagenticUIServer.Hubs;
using MagenticUIServer.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSignalR();
builder.Services.AddHttpClient();
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
        policy.WithOrigins("http://localhost:5173")
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials());
});
builder.Services.AddSingleton<AgentSessionService>();

var app = builder.Build();

app.UseCors();
app.UseDefaultFiles();
app.UseStaticFiles();

app.MapHub<AgentHub>("/hubs/agent");
app.MapGet("/health", () => Results.Ok(new { status = "ok", version = "3.0.0-alpha" }));
app.MapFallbackToFile("index.html");

await app.RunAsync();
