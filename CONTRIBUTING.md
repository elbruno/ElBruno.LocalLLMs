# Contributing to ElBruno.LocalLLMs

Thank you for your interest in contributing! This guide explains how to build, test, add models, and submit pull requests.

---

## Quick Start

### Prerequisites

- **.NET 8.0 or 10.0** — [Download](https://dotnet.microsoft.com/en-us/download)
- **Git** — for version control
- **Python 3.10+** — only if converting models to ONNX

### Build the Project

```bash
# Clone the repository
git clone https://github.com/elbruno/ElBruno.LocalLLMs.git
cd ElBruno.LocalLLMs

# Restore NuGet packages and build
dotnet build

# Build with verbose output (if needed)
dotnet build --verbosity detailed
```

### Run Tests

```bash
# Run all unit tests
dotnet test

# Run unit tests only (skip integration tests)
dotnet test --filter "Category!=Integration"

# Run a specific test
dotnet test --filter "Name=ChatTemplateFactory_ChatML_FormatsSystemAndUserMessages"

# Run with verbose output
dotnet test --verbosity detailed
```

### Integration Tests

Integration tests require downloading real models and running inference. They're gated by environment variable:

```bash
# Enable integration tests (downloads GBs of models, may take 10+ minutes)
set RUN_INTEGRATION_TESTS=true
dotnet test

# Or on macOS/Linux:
export RUN_INTEGRATION_TESTS=true
dotnet test
```

---

## Project Structure

```
.
├── src/
│   └── ElBruno.LocalLLMs/           # Core library
│       ├── LocalChatClient.cs        # Main IChatClient implementation
│       ├── LocalLLMsOptions.cs       # Configuration class
│       ├── LocalLLMsServiceExtensions.cs  # DI helpers
│       ├── Models/
│       │   ├── KnownModels.cs        # Pre-defined models (where you add new models)
│       │   └── ModelDefinition.cs    # Model metadata record
│       ├── Internal/
│       │   ├── ChatTemplateFactory.cs    # Chat template resolution
│       │   ├── ChatTemplateFormatters/   # Phi3, ChatML, Llama3, Qwen, Mistral
│       │   ├── ModelDownloader.cs        # HuggingFace download logic
│       │   └── OnnxGenAIModel.cs         # ONNX Runtime GenAI wrapper
│       └── *Enums.cs                 # ExecutionProvider, ChatTemplateFormat, ModelTier
├── tests/
│   ├── ElBruno.LocalLLMs.Tests/      # Unit tests (fast, mocked)
│   └── ElBruno.LocalLLMs.IntegrationTests/  # Integration tests (slow, real models)
├── samples/
│   ├── HelloChat/                    # Minimal example
│   ├── StreamingChat/                # Streaming example
│   ├── MultiModelChat/               # Switch models
│   └── DependencyInjection/          # ASP.NET Core DI
├── scripts/
│   ├── convert_to_onnx.py            # ONNX conversion script
│   └── requirements.txt              # Python dependencies
├── docs/
│   ├── architecture.md               # Design decisions
│   ├── getting-started.md            # User guide (you probably read this)
│   └── supported-models.md           # Model reference
├── .github/workflows/
│   ├── ci.yml                        # Build & test on every push
│   └── release.yml                   # Publish NuGet package on release
└── .editorconfig                     # Code style rules
```

---

## Code Style

This project enforces code style via `.editorconfig`. Most rules are automatically checked and enforced during build:

```
✅ Required
- C# language version 12.0 (implicit usings, nullable reference types)
- Treat warnings as errors (TreatWarningsAsErrors=true)
- Nullable reference types enabled (Nullable=enable)
- Enforce code style in build (EnforceCodeStyleInBuild=true)

❌ Discouraged
- Over-commented code — only comment if the code is confusing
- Unnecessarily complex logic
- Public surface bloat
```

### Key Style Points

```csharp
// ✅ Good: Clear naming, single responsibility
public async Task<ChatResponse> GetResponseAsync(
    IEnumerable<ChatMessage> messages,
    ChatOptions? options = null,
    CancellationToken cancellationToken = default)
{
    // Implementation
}

// ❌ Bad: Overloaded methods with slight variations
public async Task<ChatResponse> CompleteAsync(IEnumerable<ChatMessage> messages) { }
public async Task<ChatResponse> CompleteAsync(IEnumerable<ChatMessage> messages, ChatOptions options) { }

// ✅ Good: Minimal comments (code is self-documenting)
await EnsureInitializedAsync(progress: null, cancellationToken);

// ❌ Bad: Unnecessary comments
// Check if initialized
if (_model == null) { }
```

---

## Adding a New Model

Adding a new model to the library involves three steps:

### Step 1: Create a `ModelDefinition` in `KnownModels.cs`

Edit `src/ElBruno.LocalLLMs/Models/KnownModels.cs`:

```csharp
/// <summary>Qwen2.5-7B-Instruct — medium-sized production model.</summary>
public static readonly ModelDefinition Qwen25_7BInstruct = new()
{
    Id = "qwen2.5-7b-instruct",
    DisplayName = "Qwen2.5-7B-Instruct",
    HuggingFaceRepoId = "Qwen/Qwen2.5-7B-Instruct",
    RequiredFiles = ["onnx/model.onnx", "onnx/model.onnx_data"],
    ModelType = OnnxModelType.GenAI,
    ChatTemplate = ChatTemplateFormat.Qwen,
    Tier = ModelTier.Medium,
    HasNativeOnnx = false  // Requires conversion
};
```

Then add it to the `All` collection:

```csharp
public static IReadOnlyList<ModelDefinition> All { get; } =
[
    Qwen25_05BInstruct,
    Phi35MiniInstruct,
    Phi4,
    Qwen25_7BInstruct,  // ← Add here
];
```

**Field explanations:**
- `Id` — unique kebab-case identifier (used in `KnownModels.FindById()`)
- `DisplayName` — human-readable name (shown in UI/logs)
- `HuggingFaceRepoId` — exact HuggingFace repo (used for auto-download)
- `RequiredFiles` — file paths within the HuggingFace repo (glob patterns supported with `*`)
- `ModelType` — always `OnnxModelType.GenAI` for LLMs
- `ChatTemplate` — format enum (ChatML, Phi3, Llama3, Qwen, or Mistral)
- `Tier` — size category (Tiny, Small, Medium, Large, NextGen)
- `HasNativeOnnx` — whether HuggingFace repo contains ONNX weights

### Step 2: Ensure the Model is in ONNX Format

**If `HasNativeOnnx = true`:**
- The model is ready — no conversion needed

**If `HasNativeOnnx = false`:**
- Convert using the Python script in `/scripts/`:
  ```bash
  cd scripts/
  python convert_to_onnx.py --model-id Qwen/Qwen2.5-7B-Instruct --output-dir ./onnx-models/qwen2.5-7b
  ```
- Upload ONNX files to HuggingFace (optional; users can convert themselves)

### Step 3: Add Tests

Tests ensure the model can be downloaded, loaded, and generate text. Add to `tests/ElBruno.LocalLLMs.IntegrationTests/`:

```csharp
[Trait("Category", "Integration")]
public class Qwen25_7BInstructIntegrationTests
{
    [SkippableFact]
    public async Task GetResponseAsync_WithQwen25_7B_GeneratesResponse()
    {
        Skip.If(!ShouldRunIntegrationTests());

        var options = new LocalLLMsOptions
        {
            Model = KnownModels.Qwen25_7BInstruct
        };

        using var client = await LocalChatClient.CreateAsync(options);
        
        var response = await client.GetResponseAsync([
            new(ChatRole.User, "What is 2+2?")
        ]);

        Assert.NotNull(response);
        Assert.NotEmpty(response.Text);
        Assert.DoesNotContain("error", response.Text, StringComparison.OrdinalIgnoreCase);
    }

    [SkippableFact]
    public async Task GetStreamingResponseAsync_WithQwen25_7B_StreamsTokens()
    {
        Skip.If(!ShouldRunIntegrationTests());

        var options = new LocalLLMsOptions
        {
            Model = KnownModels.Qwen25_7BInstruct
        };

        using var client = await LocalChatClient.CreateAsync(options);
        
        var updates = new List<ChatResponseUpdate>();
        await foreach (var update in client.GetStreamingResponseAsync([
            new(ChatRole.User, "Say hello")
        ]))
        {
            updates.Add(update);
        }

        Assert.NotEmpty(updates);
    }

    private static bool ShouldRunIntegrationTests() =>
        Environment.GetEnvironmentVariable("RUN_INTEGRATION_TESTS") == "true";
}
```

### Step 4: Submit a PR

Create a pull request with:
- [ ] `ModelDefinition` added to `KnownModels`
- [ ] Chat template is correct (test with the model if unsure)
- [ ] Unit tests pass: `dotnet test`
- [ ] Integration tests pass (if you have local setup): `RUN_INTEGRATION_TESTS=true dotnet test`
- [ ] Documentation updated if needed

---

## Converting Models to ONNX

If a model doesn't have native ONNX weights, use the conversion script:

### Prerequisites

```bash
# Install Python 3.10+
python --version  # Should be 3.10+

# Create virtual environment (optional but recommended)
python -m venv venv
# Activate:
# Windows: venv\Scripts\activate
# macOS/Linux: source venv/bin/activate

# Install dependencies
cd scripts/
pip install -r requirements.txt
```

### Convert a Model

```bash
python convert_to_onnx.py \
    --model-id meta-llama/Llama-3.2-3B-Instruct \
    --output-dir ./onnx-models/llama-3.2-3b
```

**Options:**
- `--model-id` — HuggingFace model ID (required)
- `--output-dir` — where to save ONNX files (default: `./onnx-models/`)
- `--quantization` — quantization level (default: int4)

See `scripts/README.md` for detailed conversion steps.

---

## Architecture Overview

### Layered Design

```
User Code
    ↓
IChatClient interface (Microsoft.Extensions.AI)
    ↓
LocalChatClient (public API)
    ↓
Internal Services:
    ├─ ChatTemplateFormatter (message formatting)
    ├─ ModelDownloader (HuggingFace download)
    ├─ OnnxGenAIModel (ONNX Runtime wrapper)
    └─ Generation parameters (temp, top-p, etc.)
    ↓
ONNX Runtime GenAI
    ↓
Model Files (ONNX weights)
```

### Key Classes

| Class | Purpose | Location |
|-------|---------|----------|
| `LocalChatClient` | Main public class, implements `IChatClient` | `LocalChatClient.cs` |
| `LocalLLMsOptions` | Configuration (model, provider, generation params) | `LocalLLMsOptions.cs` |
| `ModelDefinition` | Metadata for a model (HF repo, files, template) | `Models/ModelDefinition.cs` |
| `KnownModels` | Registry of pre-defined models | `Models/KnownModels.cs` |
| `IChatTemplateFormatter` | Strategy for formatting messages (internal) | `Internal/ChatTemplateFormatters/` |
| `ModelDownloader` | Downloads models from HuggingFace | `Internal/ModelDownloader.cs` |
| `OnnxGenAIModel` | Wraps ONNX Runtime GenAI inference | `Internal/OnnxGenAIModel.cs` |

---

## Testing Strategy

### Unit Tests

Fast, isolated, use mocks. Run every build.

```bash
dotnet test --filter "Category!=Integration"
```

**Covers:**
- Chat template formatting (exact string matching)
- Options validation
- Model registry (FindById, All)
- Generation parameter building

### Integration Tests

Slow, require real models and GPU/CPU. Gated by env var.

```bash
RUN_INTEGRATION_TESTS=true dotnet test
```

**Covers:**
- Model download from HuggingFace
- ONNX model loading
- E2E inference (response generation)
- Streaming output
- Multi-turn conversations

### Running Locally

```bash
# Unit tests only (fast, always safe)
dotnet test --filter "Category!=Integration"

# Integration tests (slow, downloads models)
# WARNING: Downloads 1–8 GB per model
set RUN_INTEGRATION_TESTS=true
dotnet test
```

---

## Debugging

### Enable Verbose Output

```bash
dotnet test --verbosity detailed
```

### Debug a Single Test

```bash
# From Visual Studio/VS Code: Set breakpoint and press F5
# Or use CLI:
dotnet test --filter "Name=TestName" --verbosity detailed
```

### Check Model Downloads

Model cache is stored in:
- **Windows:** `%LOCALAPPDATA%\ElBruno\LocalLLMs\models\`
- **Linux:** `~/.cache/elbruno/localllms/models/`
- **macOS:** `~/Library/Caches/ElBruno/LocalLLMs/models/`

### Common Issues

**Build fails with "TreatWarningsAsErrors":**
- Fix compiler warnings (they're now errors)
- Or temporarily remove `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>` from `Directory.Build.props`

**Tests fail to find models:**
- Ensure internet connection (HuggingFace download needs it)
- Check HuggingFace repo ID is correct in `ModelDefinition`

**ONNX conversion fails:**
- Ensure Python 3.10+ is installed
- Install dependencies: `pip install -r scripts/requirements.txt`
- Try with `--quantization int4` (default)

---

## CI/CD Pipeline

Every push to `main` or PR triggers:

1. **Build** — `dotnet build`
2. **Unit Tests** — `dotnet test --filter "Category!=Integration"`
3. **Code Analysis** — StyleCop, warnings-as-errors
4. **NuGet Pack** — Create .nupkg file

On release tag (`v*`):
- Publish to NuGet: `dotnet nuget push`

See `.github/workflows/` for workflow definitions.

---

## License & CLA

This project is licensed under the **MIT License**. By submitting a PR, you agree that your contribution is licensed under the same terms.

---

## PR Guidelines

### Before Submitting

- [ ] Code passes `dotnet build` (warnings = errors)
- [ ] Unit tests pass: `dotnet test --filter "Category!=Integration"`
- [ ] Added tests for new functionality
- [ ] Updated documentation if API changed
- [ ] Commit message is clear and concise

### Commit Message Format

```
Brief summary (50 chars max)

Optional longer explanation (72 chars per line max).
If this is a new feature, explain the use case.
If this is a bug fix, explain the issue.

Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>
```

### Example PR Title

```
Add support for Qwen2.5-7B model

- Added ModelDefinition to KnownModels
- Added integration tests
- Updated supported-models.md
```

---

## Questions?

- 📖 Read [getting-started.md](docs/getting-started.md) for user docs
- 🏗️ Read [architecture.md](docs/architecture.md) for design details
- 💬 Open an issue for bugs or features
- 🐍 See [scripts/README.md](scripts/README.md) for ONNX conversion help

Thank you for contributing! 🚀
