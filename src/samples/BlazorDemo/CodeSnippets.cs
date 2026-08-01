namespace BlazorDemo;

/// <summary>
/// Source code snippets displayed on each demo page via &lt;CodeSample&gt;.
/// Kept in a plain .cs file to avoid Razor parser issues with multi-line strings.
/// </summary>
internal static class CodeSnippets
{
    public static readonly string ModelStatusCard =
        "// Program.cs — register services once\n" +
        "builder.Services.AddLocalLLMs(options =>\n" +
        "{\n" +
        "    options.Model = KnownModels.Phi35MiniInstruct;\n" +
        "    options.EnsureModelDownloaded = true;\n" +
        "});\n" +
        "builder.Services.AddLocalLLMsBlazorComponents();\n" +
        "\n" +
        "// MyPage.razor — use the component\n" +
        "@using ElBruno.LocalLLMs.BlazorComponents.Components\n" +
        "\n" +
        "<ModelStatusCard Model=\"KnownModels.Phi35MiniInstruct\"\n" +
        "                 OnModelSelected=\"OnModelSelected\" />\n" +
        "\n" +
        "@code {\n" +
        "    void OnModelSelected(ModelDefinition model)\n" +
        "    {\n" +
        "        // model.DisplayName, model.Id, model.Tier are all available\n" +
        "        // store in state, pass to ChatBox, or navigate to /chat\n" +
        "    }\n" +
        "}";

    public static readonly string ModelGallery =
        "@using ElBruno.LocalLLMs.BlazorComponents.Components\n" +
        "\n" +
        "// Show all KnownModels with tier / vision / tools / downloaded filters\n" +
        "<ModelGallery OnModelSelected=\"OnModelSelected\" />\n" +
        "\n" +
        "// Limit to a custom subset\n" +
        "<ModelGallery Models=\"myModels\" OnModelSelected=\"OnModelSelected\" />\n" +
        "\n" +
        "@code {\n" +
        "    ModelDefinition[] myModels = new[]\n" +
        "    {\n" +
        "        KnownModels.Phi35MiniInstruct,\n" +
        "        KnownModels.Llama32_3BInstruct,\n" +
        "        KnownModels.Qwen25_05BInstruct\n" +
        "    };\n" +
        "\n" +
        "    void OnModelSelected(ModelDefinition model)\n" +
        "    {\n" +
        "        // navigate to /chat or update shared state\n" +
        "    }\n" +
        "}";

    public static readonly string ChatBox =
        "// Program.cs\n" +
        "builder.Services.AddLocalLLMs(options =>\n" +
        "{\n" +
        "    options.Model = KnownModels.Phi35MiniInstruct;\n" +
        "    options.EnsureModelDownloaded = true;\n" +
        "});\n" +
        "builder.Services.AddLocalLLMsBlazorComponents();\n" +
        "\n" +
        "// MyPage.razor\n" +
        "@inject IChatClient ChatClient\n" +
        "@using ElBruno.LocalLLMs.BlazorComponents.Components\n" +
        "\n" +
        "// Optional: let the user pick a model\n" +
        "<ModelSelector @bind-Value=\"selectedModel\" />\n" +
        "\n" +
        "// Streaming chat box — works with any IChatClient\n" +
        "<ChatBox Client=\"ChatClient\"\n" +
        "         ModelName=\"@selectedModel?.DisplayName\"\n" +
        "         SystemPrompt=\"You are a helpful assistant.\"\n" +
        "         ShowMetrics=\"true\" />\n" +
        "\n" +
        "@code {\n" +
        "    ModelDefinition? selectedModel = KnownModels.Phi35MiniInstruct;\n" +
        "}";

    public static readonly string RagPlayground =
        "// Program.cs — no extra DI needed beyond core services\n" +
        "builder.Services.AddLocalLLMs(options =>\n" +
        "{\n" +
        "    options.Model = KnownModels.Phi35MiniInstruct;\n" +
        "    options.EnsureModelDownloaded = true;\n" +
        "});\n" +
        "builder.Services.AddLocalLLMsBlazorComponents();\n" +
        "\n" +
        "// MyPage.razor — drop in the playground\n" +
        "@using ElBruno.LocalLLMs.BlazorComponents.Components\n" +
        "\n" +
        "<RagPlayground />\n" +
        "\n" +
        "// The component manages its own LocalRagPipeline internally.\n" +
        "// Add text documents in the UI, then query for the top-K chunks.";

    public static readonly string EnvironmentDashboard =
        "@using ElBruno.LocalLLMs.BlazorComponents.Components\n" +
        "\n" +
        "// Full dashboard (default)\n" +
        "// Shows CPU/CUDA/DirectML badges, .NET version, OS, cache path and size\n" +
        "<EnvironmentDashboard />\n" +
        "\n" +
        "// Compact mode — single line for nav bars or sidebars\n" +
        "<EnvironmentDashboard Compact=\"true\" />\n" +
        "\n" +
        "// Hide the cache info section\n" +
        "<EnvironmentDashboard ShowCacheInfo=\"false\" />\n" +
        "\n" +
        "// Pass a LocalChatClient to include per-model cache sizes\n" +
        "<EnvironmentDashboard Client=\"@localClient\" ShowCacheInfo=\"true\" />";
}
