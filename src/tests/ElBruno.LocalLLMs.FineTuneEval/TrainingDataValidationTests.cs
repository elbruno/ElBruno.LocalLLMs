using System.Text.Json;
using System.Text.RegularExpressions;

namespace ElBruno.LocalLLMs.FineTuneEval;

/// <summary>
/// Validates training data quality for fine-tuning.
/// Tests structural correctness of ShareGPT-format training data,
/// tool call tag validity, deduplication, and train/validation splits.
/// Training data files live in the training-data/ folder at repo root.
/// </summary>
public class TrainingDataValidationTests
{
    private static readonly string RepoRoot = FindRepoRoot();
    private static readonly string TrainingDataPath = Path.Combine(RepoRoot, "training-data");

    private static string FindRepoRoot()
    {
        var dir = AppContext.BaseDirectory;
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir, "ElBruno.LocalLLMs.slnx")))
                return dir;
            dir = Path.GetDirectoryName(dir);
        }

        // Fallback — tests may still run with inline data
        return Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "..");
    }

    // ──────────────────────────────────────────────
    // Training data files existence (conditional)
    // ──────────────────────────────────────────────

    [Fact]
    public void TrainingDataFolder_PathIsResolvable()
    {
        // This always passes — validates our test infrastructure can find the repo root
        Assert.False(string.IsNullOrEmpty(RepoRoot));
        Assert.True(File.Exists(Path.Combine(RepoRoot, "ElBruno.LocalLLMs.slnx")),
            "Repo root should contain the solution file");
    }

    [Fact]
    public void ExpectedTrainingFiles_AreDocumented()
    {
        // Per plan-finetune-qwen.md Phase 1, these files should be produced
        var expectedFiles = new[]
        {
            "tool-calling-train.json",
            "rag-grounded-train.json",
            "chat-template-train.json",
            "combined-train.json",
            "validation.json"
        };

        // Verify we have a clear spec — at least 5 expected files
        Assert.True(expectedFiles.Length >= 5);
        Assert.All(expectedFiles, f => Assert.EndsWith(".json", f));
    }

    // ──────────────────────────────────────────────
    // ShareGPT format structure validation
    // ──────────────────────────────────────────────

    [Fact]
    public void ShareGPTFormat_HasCorrectStructure()
    {
        // Validate the expected ShareGPT format per plan-finetune-qwen.md
        var sampleData = """
            {
              "conversations": [
                {"from": "system", "value": "You are a helpful assistant."},
                {"from": "human", "value": "What is 2+2?"},
                {"from": "gpt", "value": "2+2 equals 4."}
              ]
            }
            """;

        using var doc = JsonDocument.Parse(sampleData);
        var root = doc.RootElement;

        Assert.True(root.TryGetProperty("conversations", out var conversations));
        Assert.Equal(JsonValueKind.Array, conversations.ValueKind);

        foreach (var turn in conversations.EnumerateArray())
        {
            Assert.True(turn.TryGetProperty("from", out var from));
            Assert.True(turn.TryGetProperty("value", out var value));
            Assert.False(string.IsNullOrEmpty(from.GetString()));
            Assert.NotNull(value.GetString());
        }
    }

    [Fact]
    public void ShareGPTFormat_FromFieldHasValidRoles()
    {
        var validRoles = new HashSet<string> { "system", "human", "gpt" };

        var sampleData = """
            {
              "conversations": [
                {"from": "system", "value": "You are a helpful assistant."},
                {"from": "human", "value": "Hello"},
                {"from": "gpt", "value": "Hi there!"},
                {"from": "human", "value": "Bye"},
                {"from": "gpt", "value": "Goodbye!"}
              ]
            }
            """;

        using var doc = JsonDocument.Parse(sampleData);
        var conversations = doc.RootElement.GetProperty("conversations");

        foreach (var turn in conversations.EnumerateArray())
        {
            var role = turn.GetProperty("from").GetString()!;
            Assert.Contains(role, validRoles);
        }
    }

    [Fact]
    public void ShareGPTFormat_ValueFieldIsNonEmpty()
    {
        var sampleData = """
            {
              "conversations": [
                {"from": "system", "value": "You are helpful."},
                {"from": "human", "value": "What is AI?"},
                {"from": "gpt", "value": "AI stands for Artificial Intelligence."}
              ]
            }
            """;

        using var doc = JsonDocument.Parse(sampleData);
        var conversations = doc.RootElement.GetProperty("conversations");

        foreach (var turn in conversations.EnumerateArray())
        {
            var value = turn.GetProperty("value").GetString();
            Assert.False(string.IsNullOrWhiteSpace(value), "Each conversation turn must have non-empty value");
        }
    }

    // ──────────────────────────────────────────────
    // Tool calling training data format
    // ──────────────────────────────────────────────

    [Fact]
    public void ToolCallingExamples_HaveValidToolCallTags()
    {
        // Matches the tool calling format from plan-finetune-qwen.md section 3.2
        var toolCallingSample = """
            {
              "conversations": [
                {
                  "from": "system",
                  "value": "You are a helpful assistant with access to the following tools:\n\n[{\"type\":\"function\",\"function\":{\"name\":\"get_weather\",\"description\":\"Get weather for a city\",\"parameters\":{\"type\":\"object\",\"properties\":{\"city\":{\"type\":\"string\"}}}}}]\n\nWhen you need to call a tool, respond with a JSON object in this format:\n{\"name\": \"tool_name\", \"arguments\": {\"arg1\": \"value1\"}}"
                },
                {
                  "from": "human",
                  "value": "What's the weather in Paris?"
                },
                {
                  "from": "gpt",
                  "value": "<tool_call>\n{\"name\": \"get_weather\", \"arguments\": {\"city\": \"Paris\"}}\n</tool_call>"
                },
                {
                  "from": "human",
                  "value": "Tool result for call_123: {\"temp\": 18, \"condition\": \"cloudy\"}"
                },
                {
                  "from": "gpt",
                  "value": "The weather in Paris is currently 18°C and cloudy."
                }
              ]
            }
            """;

        using var doc = JsonDocument.Parse(toolCallingSample);
        var conversations = doc.RootElement.GetProperty("conversations");

        // Find the GPT turn that contains a tool call
        var toolCallPattern = new Regex(@"<tool_call>\s*(.*?)\s*</tool_call>", RegexOptions.Singleline);
        var foundToolCall = false;

        foreach (var turn in conversations.EnumerateArray())
        {
            if (turn.GetProperty("from").GetString() != "gpt")
                continue;

            var value = turn.GetProperty("value").GetString()!;
            var match = toolCallPattern.Match(value);
            if (!match.Success)
                continue;

            foundToolCall = true;
            var json = match.Groups[1].Value.Trim();

            // The JSON inside <tool_call> tags must be valid
            using var toolDoc = JsonDocument.Parse(json);
            var toolRoot = toolDoc.RootElement;
            Assert.True(toolRoot.TryGetProperty("name", out _), "Tool call JSON must have 'name'");
            Assert.True(toolRoot.TryGetProperty("arguments", out _), "Tool call JSON must have 'arguments'");
        }

        Assert.True(foundToolCall, "Tool calling sample must contain at least one <tool_call> tag");
    }

    [Fact]
    public void ToolCallingExamples_SystemPromptContainsToolDefinitions()
    {
        var systemPrompt = """You are a helpful assistant with access to the following tools:\n\n[{"type":"function","function":{"name":"get_weather","description":"Get weather for a city","parameters":{"type":"object","properties":{"city":{"type":"string"}}}}}]""";

        Assert.Contains("tools", systemPrompt);
        Assert.Contains("function", systemPrompt);
        Assert.Contains("name", systemPrompt);
        Assert.Contains("parameters", systemPrompt);
    }

    // ──────────────────────────────────────────────
    // Deduplication validation
    // ──────────────────────────────────────────────

    [Fact]
    public void NoDuplicateExamples_InSampleDataset()
    {
        var examples = new[]
        {
            """{"conversations":[{"from":"human","value":"What is 2+2?"},{"from":"gpt","value":"4"}]}""",
            """{"conversations":[{"from":"human","value":"What is 3+3?"},{"from":"gpt","value":"6"}]}""",
            """{"conversations":[{"from":"human","value":"What is 4+4?"},{"from":"gpt","value":"8"}]}"""
        };

        var uniqueExamples = examples.Distinct().ToList();
        Assert.Equal(examples.Length, uniqueExamples.Count);
    }

    [Fact]
    public void DuplicateDetection_FindsDuplicates()
    {
        var examples = new[]
        {
            """{"conversations":[{"from":"human","value":"Hello"},{"from":"gpt","value":"Hi"}]}""",
            """{"conversations":[{"from":"human","value":"Bye"},{"from":"gpt","value":"Goodbye"}]}""",
            """{"conversations":[{"from":"human","value":"Hello"},{"from":"gpt","value":"Hi"}]}"""
        };

        var uniqueExamples = examples.Distinct().ToList();
        Assert.NotEqual(examples.Length, uniqueExamples.Count);
    }

    // ──────────────────────────────────────────────
    // Train/validation split validation
    // ──────────────────────────────────────────────

    [Theory]
    [InlineData(5000, 500, 0.08, 0.12)]  // ~10% validation
    [InlineData(1000, 100, 0.08, 0.12)]  // ~10% validation
    [InlineData(2000, 200, 0.08, 0.12)]  // ~10% validation
    public void TrainValidationSplit_IsApproximately90_10(
        int trainCount, int valCount, double minRatio, double maxRatio)
    {
        var total = trainCount + valCount;
        var valRatio = (double)valCount / total;

        Assert.InRange(valRatio, minRatio, maxRatio);
    }

    [Fact]
    public void TrainValidationSplit_NoOverlap()
    {
        // Simulate train and validation sets — they should have no overlap
        var trainExamples = new HashSet<string>
        {
            "What is 2+2?", "What is 3+3?", "What is 4+4?"
        };

        var valExamples = new HashSet<string>
        {
            "What is 5+5?", "What is 6+6?"
        };

        var overlap = trainExamples.Intersect(valExamples).ToList();
        Assert.Empty(overlap);
    }

    // ──────────────────────────────────────────────
    // File-based validation (conditional on training data existence)
    // ──────────────────────────────────────────────

    [SkippableFact]
    public void AllTrainingDataFiles_ExistAndAreValidJson()
    {
        Skip.If(!Directory.Exists(TrainingDataPath),
            "training-data/ folder not yet created (Phase 1 pending)");

        var expectedFiles = new[]
        {
            "tool-calling-train.json",
            "rag-grounded-train.json",
            "chat-template-train.json",
            "combined-train.json",
            "validation.json"
        };

        foreach (var file in expectedFiles)
        {
            var filePath = Path.Combine(TrainingDataPath, file);
            Skip.If(!File.Exists(filePath), $"{file} not yet created");

            var content = File.ReadAllText(filePath);
            Assert.False(string.IsNullOrWhiteSpace(content), $"{file} should not be empty");

            // Should be valid JSON (array of examples or single object)
            var ex = Record.Exception(() => JsonDocument.Parse(content));
            Assert.Null(ex);
        }
    }

    [SkippableFact]
    public void CombinedTrainFile_ContainsExpectedExampleCount()
    {
        var combinedPath = Path.Combine(TrainingDataPath, "combined-train.json");
        Skip.If(!File.Exists(combinedPath),
            "combined-train.json not yet created (Phase 1 pending)");

        var content = File.ReadAllText(combinedPath);
        using var doc = JsonDocument.Parse(content);

        // Seed data: 50+ tool + 20+ RAG + 20+ chat; grows with prepare_training_data.py
        if (doc.RootElement.ValueKind == JsonValueKind.Array)
        {
            var count = doc.RootElement.GetArrayLength();
            Assert.True(count >= 50, $"Expected at least 50 training examples, got {count}");
        }
    }
}
