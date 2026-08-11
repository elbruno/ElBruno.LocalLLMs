using BlazorDemo;
using ElBruno.LocalLLMs;
using ElBruno.LocalLLMs.BlazorComponents.Extensions;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Register LocalLLMs core (IChatClient + ILocalLLMsDiagnostics)
builder.Services.AddLocalLLMs(options =>
{
    options.Model = KnownModels.Phi35MiniInstruct;
    options.EnsureModelDownloaded = true;
});

// Register all BlazorComponents services; ModelStateService is application-wide
// so a disconnected Blazor circuit does not cancel an active model download.
// Host folder actions are explicit opt-in because they execute on the server host.
builder.Services.AddLocalLLMsBlazorComponents(options =>
{
    options.EnableHostFolderActions = true;
});

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
