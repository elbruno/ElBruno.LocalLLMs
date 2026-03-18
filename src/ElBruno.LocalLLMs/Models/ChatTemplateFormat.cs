namespace ElBruno.LocalLLMs;

/// <summary>
/// Chat template formatting standard.
/// </summary>
public enum ChatTemplateFormat
{
    /// <summary>ChatML format (&lt;|im_start|&gt;).</summary>
    ChatML,
    /// <summary>Llama 3 format (&lt;|begin_of_text|&gt;).</summary>
    Llama3,
    /// <summary>Phi-3 format (&lt;|user|&gt;).</summary>
    Phi3,
    /// <summary>Gemma format.</summary>
    Gemma,
    /// <summary>Mistral format ([INST]).</summary>
    Mistral,
    /// <summary>Qwen format.</summary>
    Qwen,
    /// <summary>DeepSeek format.</summary>
    DeepSeek,
    /// <summary>Custom user-defined format.</summary>
    Custom
}
