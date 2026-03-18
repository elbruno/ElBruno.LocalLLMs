// TODO: DependencyInjection sample — Trinity is building the core library
// See docs/architecture.md §10.4 for the target implementation

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.MapGet("/", () => "ElBruno.LocalLLMs — DependencyInjection Sample (waiting for core library)");

app.Run();
