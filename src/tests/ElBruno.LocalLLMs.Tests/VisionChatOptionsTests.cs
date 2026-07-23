using Microsoft.Extensions.AI;

namespace ElBruno.LocalLLMs.Tests;

/// <summary>
/// Tests for <see cref="VisionChatOptions"/> — the ChatOptions subclass
/// that carries image paths for vision models.
/// </summary>
[Trait("Category", "Fara")]
public class VisionChatOptionsTests
{
    [Fact]
    public void VisionChatOptions_CanBeConstructed()
    {
        var options = new VisionChatOptions();

        Assert.NotNull(options);
    }

    [Fact]
    public void VisionChatOptions_IsSubclassOf_ChatOptions()
    {
        var options = new VisionChatOptions();

        Assert.IsAssignableFrom<ChatOptions>(options);
    }

    [Fact]
    public void VisionChatOptions_ImagePaths_DefaultsToEmptyArray()
    {
        var options = new VisionChatOptions();

        Assert.NotNull(options.ImagePaths);
        Assert.Empty(options.ImagePaths);
    }

    [Fact]
    public void VisionChatOptions_ImagePaths_CanBeSetToStringArray()
    {
        var options = new VisionChatOptions
        {
            ImagePaths = ["C:\\images\\photo.jpg", "C:\\images\\diagram.png"]
        };

        Assert.Equal(2, options.ImagePaths.Length);
        Assert.Contains("C:\\images\\photo.jpg", options.ImagePaths);
        Assert.Contains("C:\\images\\diagram.png", options.ImagePaths);
    }

    [Fact]
    public void VisionChatOptions_ImagePaths_NullThrowsOrIsHandled()
    {
        var options = new VisionChatOptions();

        // Setting to null should either throw ArgumentNullException or be accepted
        // depending on implementation. Either behavior is acceptable.
        try
        {
            options.ImagePaths = null!;
            // If no exception is thrown, the property accepts null — that is valid
        }
        catch (ArgumentNullException)
        {
            // ArgumentNullException is also acceptable
        }
    }
}
