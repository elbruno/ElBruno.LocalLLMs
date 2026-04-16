using System.Runtime.InteropServices;

namespace ElBruno.LocalLLMs.BitNet.Native;

/// <summary>
/// P/Invoke declarations for the llama.h C API (bitnet.cpp build).
/// </summary>
internal static class LlamaNative
{
    private const string LibraryName = "llama";

    [StructLayout(LayoutKind.Sequential)]
    internal struct LlamaModelParams
    {
        public IntPtr devices;
        public IntPtr tensor_buft_overrides;
        public int n_gpu_layers;
        public int split_mode;
        public int main_gpu;
        public IntPtr tensor_split;
        public IntPtr progress_callback;
        public IntPtr progress_callback_user_data;
        public IntPtr kv_overrides;
        [MarshalAs(UnmanagedType.I1)] public bool vocab_only;
        [MarshalAs(UnmanagedType.I1)] public bool use_mmap;
        [MarshalAs(UnmanagedType.I1)] public bool use_direct_io;
        [MarshalAs(UnmanagedType.I1)] public bool use_mlock;
        [MarshalAs(UnmanagedType.I1)] public bool check_tensors;
        [MarshalAs(UnmanagedType.I1)] public bool use_extra_bufts;
        [MarshalAs(UnmanagedType.I1)] public bool no_host;
        [MarshalAs(UnmanagedType.I1)] public bool no_alloc;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct LlamaContextParams
    {
        public uint n_ctx;
        public uint n_batch;
        public uint n_ubatch;
        public uint n_seq_max;
        public int n_threads;
        public int n_threads_batch;
        public int rope_scaling_type;
        public int pooling_type;
        public int attention_type;
        public int flash_attn_type;
        public float rope_freq_base;
        public float rope_freq_scale;
        public float yarn_ext_factor;
        public float yarn_attn_factor;
        public float yarn_beta_fast;
        public float yarn_beta_slow;
        public uint yarn_orig_ctx;
        public float defrag_thold;
        public IntPtr cb_eval;
        public IntPtr cb_eval_user_data;
        public int type_k;
        public int type_v;
        public IntPtr abort_callback;
        public IntPtr abort_callback_data;
        [MarshalAs(UnmanagedType.I1)] public bool embeddings;
        [MarshalAs(UnmanagedType.I1)] public bool offload_kqv;
        [MarshalAs(UnmanagedType.I1)] public bool no_perf;
        [MarshalAs(UnmanagedType.I1)] public bool op_offload;
        [MarshalAs(UnmanagedType.I1)] public bool swa_full;
        [MarshalAs(UnmanagedType.I1)] public bool kv_unified;
        public IntPtr samplers;
        public UIntPtr n_samplers;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct LlamaBatch
    {
        public int n_tokens;
        public IntPtr token;
        public IntPtr embd;
        public IntPtr pos;
        public IntPtr n_seq_id;
        public IntPtr seq_id;
        public IntPtr logits;
    }

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void llama_backend_init();

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void llama_backend_free();

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern LlamaModelParams llama_model_default_params();

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern LlamaContextParams llama_context_default_params();

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern IntPtr llama_model_load_from_file(
        [MarshalAs(UnmanagedType.LPUTF8Str)] string path_model,
        LlamaModelParams @params);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void llama_model_free(IntPtr model);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern IntPtr llama_new_context_with_model(
        IntPtr model,
        LlamaContextParams @params);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void llama_free(IntPtr ctx);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern IntPtr llama_model_get_vocab(IntPtr model);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int llama_tokenize(
        IntPtr vocab,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string text,
        int text_len,
        [Out] int[] tokens,
        int n_tokens_max,
        [MarshalAs(UnmanagedType.I1)] bool add_special,
        [MarshalAs(UnmanagedType.I1)] bool parse_special);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int llama_token_to_piece(
        IntPtr vocab,
        int token,
        [Out] byte[] buf,
        int length,
        int lstrip,
        [MarshalAs(UnmanagedType.I1)] bool special);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int llama_token_bos(IntPtr vocab);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int llama_token_eos(IntPtr vocab);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int llama_decode(IntPtr ctx, LlamaBatch batch);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern LlamaBatch llama_batch_init(int n_tokens, int embd, int n_seq_max);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void llama_batch_free(LlamaBatch batch);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern IntPtr llama_get_logits_ith(IntPtr ctx, int i);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int llama_n_vocab(IntPtr vocab);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void llama_kv_cache_clear(IntPtr ctx);
}
