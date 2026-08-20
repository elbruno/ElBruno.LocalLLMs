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
    /// <summary>Qwen format (Qwen2.5 and earlier).</summary>
    Qwen,
    /// <summary>Qwen3 format — non-thinking mode with XML tool tags.</summary>
    Qwen3,
    /// <summary>Fara1.5 VLM format — Qwen3 ChatML with Qwen-VL vision token injection. No tool calling.</summary>
    Fara,
    /// <summary>DeepSeek format.</summary>
    DeepSeek,
    /// <summary>
    /// OpenAI Harmony format (GPT-OSS) — &lt;|start|&gt;role&lt;|message|&gt;…&lt;|end|&gt; with
    /// analysis/commentary/final channels. GPT-OSS models only work with this format.
    /// </summary>
    Harmony,
    /// <summary>Custom user-defined format.</summary>
    Custom
}
