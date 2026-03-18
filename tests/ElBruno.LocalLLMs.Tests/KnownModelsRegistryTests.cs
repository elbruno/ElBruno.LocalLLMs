using ElBruno.LocalLLMs;
using ElBruno.LocalLLMs.Internal;

namespace ElBruno.LocalLLMs.Tests;

/// <summary>
/// Validates the KnownModels registry integrity — uniqueness, format, completeness.
/// </summary>
public class KnownModelsRegistryTests
{
    // ──────────────────────────────────────────────
    // ID uniqueness and format
    // ──────────────────────────────────────────────

    [Fact]
    public void AllModelIds_AreUnique()
    {
        var ids = KnownModels.All.Select(m => m.Id).ToList();
        var duplicates = ids.GroupBy(x => x).Where(g => g.Count() > 1).Select(g => g.Key).ToList();

        Assert.Empty(duplicates);
    }

    [Fact]
    public void AllModelIds_AreLowercaseKebabCase()
    {
        foreach (var model in KnownModels.All)
        {
            // kebab-case: lowercase letters, digits, hyphens, dots (for versions)
            Assert.Matches(@"^[a-z0-9][a-z0-9.\-]*$", model.Id);
        }
    }

    // ──────────────────────────────────────────────
    // HuggingFace repo ID format
    // ──────────────────────────────────────────────

    [Fact]
    public void AllHuggingFaceRepoIds_FollowOwnerSlashRepoFormat()
    {
        foreach (var model in KnownModels.All)
        {
            Assert.Contains("/", model.HuggingFaceRepoId);
            var parts = model.HuggingFaceRepoId.Split('/');
            Assert.Equal(2, parts.Length);
            Assert.False(string.IsNullOrWhiteSpace(parts[0]), $"Model {model.Id}: empty owner in HF repo ID");
            Assert.False(string.IsNullOrWhiteSpace(parts[1]), $"Model {model.Id}: empty repo in HF repo ID");
        }
    }

    // ──────────────────────────────────────────────
    // RequiredFiles validation
    // ──────────────────────────────────────────────

    [Fact]
    public void AllModels_HaveNonEmptyRequiredFiles()
    {
        foreach (var model in KnownModels.All)
        {
            Assert.NotNull(model.RequiredFiles);
            Assert.NotEmpty(model.RequiredFiles);
            Assert.All(model.RequiredFiles, f => Assert.False(string.IsNullOrWhiteSpace(f)));
        }
    }

    // ──────────────────────────────────────────────
    // ChatTemplate → Formatter coverage
    // ──────────────────────────────────────────────

    [Fact]
    public void AllChatTemplateFormats_UsedByModels_HaveFormatterInFactory()
    {
        var usedFormats = KnownModels.All.Select(m => m.ChatTemplate).Distinct();

        foreach (var format in usedFormats)
        {
            var formatter = ChatTemplateFactory.Create(format);
            Assert.NotNull(formatter);
        }
    }

    // ──────────────────────────────────────────────
    // FindById roundtrip for every model
    // ──────────────────────────────────────────────

    [Fact]
    public void FindById_WorksForEveryModelInAll()
    {
        foreach (var model in KnownModels.All)
        {
            var found = KnownModels.FindById(model.Id);
            Assert.NotNull(found);
            Assert.Same(model, found);
        }
    }

    [Fact]
    public void FindById_IsCaseInsensitive_ForAllModels()
    {
        foreach (var model in KnownModels.All)
        {
            var found = KnownModels.FindById(model.Id.ToUpperInvariant());
            Assert.NotNull(found);
            Assert.Equal(model.Id, found!.Id);
        }
    }

    [Fact]
    public void FindById_ReturnsNull_ForUnknownModel()
    {
        Assert.Null(KnownModels.FindById("not-a-real-model-id"));
    }

    // ──────────────────────────────────────────────
    // Tier coverage
    // ──────────────────────────────────────────────

    [Theory]
    [InlineData(ModelTier.Tiny)]
    [InlineData(ModelTier.Small)]
    [InlineData(ModelTier.Medium)]
    [InlineData(ModelTier.Large)]
    public void EachTier_HasAtLeastOneModel(ModelTier tier)
    {
        var models = KnownModels.All.Where(m => m.Tier == tier).ToList();
        Assert.NotEmpty(models);
    }

    [Fact]
    public void TotalModelCount_IsAtLeast20()
    {
        Assert.True(KnownModels.All.Count >= 20, $"Expected at least 20 models, found {KnownModels.All.Count}");
    }

    // ──────────────────────────────────────────────
    // Native ONNX consistency
    // ──────────────────────────────────────────────

    [Fact]
    public void NativeOnnxModels_ExistInRegistry()
    {
        var nativeOnnx = KnownModels.All.Where(m => m.HasNativeOnnx).ToList();
        Assert.True(nativeOnnx.Count >= 2, "Expected at least Phi-3.5 and Phi-4 as native ONNX");
    }

    [Fact]
    public void AllModels_HaveValidDisplayNames()
    {
        foreach (var model in KnownModels.All)
        {
            Assert.False(string.IsNullOrWhiteSpace(model.DisplayName), $"Model {model.Id} has empty DisplayName");
            Assert.NotEqual(model.Id, model.DisplayName); // display name should differ from ID
        }
    }
}
