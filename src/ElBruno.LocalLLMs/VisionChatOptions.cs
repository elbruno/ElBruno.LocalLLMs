using Microsoft.Extensions.AI;

namespace ElBruno.LocalLLMs;

/// <summary>
/// Chat completion options for vision-language models. Extends <see cref="ChatOptions"/>
/// with image inputs for multimodal inference.
/// </summary>
public sealed class VisionChatOptions : ChatOptions
{
    /// <summary>
    /// File paths of images to include in the next generation call.
    /// Paths must be accessible to the process at call time.
    /// Leave empty for text-only requests on a VLM.
    /// </summary>
    public string[] ImagePaths { get; set; } = [];
}
