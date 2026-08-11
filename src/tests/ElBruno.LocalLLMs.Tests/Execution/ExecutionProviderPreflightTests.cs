using ElBruno.LocalLLMs.Diagnostics;
using System.Text.RegularExpressions;
using ElBruno.LocalLLMs;
using ElBruno.LocalLLMs.Internal;

namespace ElBruno.LocalLLMs.Tests.Execution;

public class ExecutionProviderPreflightTests
{
    [Fact]
    public void Validate_CudaAvailableOnWindows_ReturnsAvailable()
    {
        var probe = FakeNativeLibraryProbe.Windows(
            knownFiles:
            [
                "cuda.dll",
                "cudart64_13.dll",
                "cublas64_13.dll",
                "cublasLt64_13.dll",
                "cudnn64_9.dll",
                "onnxruntime-genai-cuda.dll",
                "onnxruntime_providers_cuda.dll"
            ],
            loadableFiles:
            [
                "cuda.dll",
                "cudart64_13.dll",
                "cublas64_13.dll",
                "cublasLt64_13.dll",
                "cudnn64_9.dll",
                "onnxruntime-genai-cuda.dll",
                "onnxruntime_providers_cuda.dll"
            ]);

        var result = ExecutionProviderPreflight.Validate(ExecutionProvider.Cuda, probe);

        Assert.True(result.IsAvailable);
        Assert.Null(result.Exception);
    }

    [Fact]
    public void Validate_CudaMissingRuntimeLibraryOnWindows_ReturnsMissingFailure()
    {
        var probe = FakeNativeLibraryProbe.Windows(
            knownFiles:
            [
                "cuda.dll",
                "cudart64_13.dll",
                "cublas64_13.dll",
                "cudnn64_9.dll",
                "onnxruntime-genai-cuda.dll",
                "onnxruntime_providers_cuda.dll"
            ],
            loadableFiles:
            [
                "cuda.dll",
                "cudart64_13.dll",
                "cublas64_13.dll",
                "cudnn64_9.dll",
                "onnxruntime-genai-cuda.dll",
                "onnxruntime_providers_cuda.dll"
            ]);

        var result = ExecutionProviderPreflight.Validate(ExecutionProvider.Cuda, probe);

        Assert.False(result.IsAvailable);
        Assert.IsType<DllNotFoundException>(result.Exception);
        Assert.Contains("cublasLt64_13.dll", result.Exception!.Message);
        Assert.DoesNotContain("mismatch", result.Exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Microsoft.ML.OnnxRuntimeGenAI.Cuda", result.Suggestion);
    }

    [Fact]
    public void Validate_CudaVersionMismatchOnWindows_ReturnsMismatchFailure()
    {
        var probe = FakeNativeLibraryProbe.Windows(
            knownFiles:
            [
                "cuda.dll",
                "cudart64_12.dll",
                "cublas64_13.dll",
                "cublasLt64_13.dll",
                "cudnn64_9.dll",
                "onnxruntime-genai-cuda.dll",
                "onnxruntime_providers_cuda.dll"
            ],
            loadableFiles:
            [
                "cuda.dll",
                "cublas64_13.dll",
                "cublasLt64_13.dll",
                "cudnn64_9.dll",
                "onnxruntime-genai-cuda.dll",
                "onnxruntime_providers_cuda.dll"
            ]);

        var result = ExecutionProviderPreflight.Validate(ExecutionProvider.Cuda, probe);

        Assert.False(result.IsAvailable);
        Assert.IsType<DllNotFoundException>(result.Exception);
        Assert.Contains("mismatch", result.Exception!.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("cudart64_13.dll", result.Exception.Message);
        Assert.Contains("cudart64_12.dll", result.Exception.Message);
    }

    [Fact]
    public void Validate_CudaOnUnsupportedPlatform_ReturnsPlatformFailure()
    {
        var probe = FakeNativeLibraryProbe.MacOS();

        var result = ExecutionProviderPreflight.Validate(ExecutionProvider.Cuda, probe);

        Assert.False(result.IsAvailable);
        Assert.IsType<PlatformNotSupportedException>(result.Exception);
        Assert.Contains("Windows and Linux", result.Exception!.Message);
    }

    [Fact]
    public void Validate_DirectMLOnWindows_ReturnsUnknown()
    {
        var probe = FakeNativeLibraryProbe.Windows();

        var result = ExecutionProviderPreflight.Validate(ExecutionProvider.DirectML, probe);

        Assert.False(result.IsAvailable);
        Assert.Equal(ExecutionProviderDiagnosticStatus.Unknown, result.Status);
        Assert.IsType<InvalidOperationException>(result.Exception);
        Assert.Contains("DirectML", result.Exception!.Message);
        Assert.Contains("cannot be confirmed", result.Exception.Message);
    }

    private sealed class FakeNativeLibraryProbe : INativeLibraryProbe
    {
        private readonly HashSet<string> _knownFiles;
        private readonly HashSet<string> _loadableFiles;

        private FakeNativeLibraryProbe(
            bool isWindows,
            bool isLinux,
            IEnumerable<string>? knownFiles = null,
            IEnumerable<string>? loadableFiles = null)
        {
            IsWindows = isWindows;
            IsLinux = isLinux;
            _knownFiles = new HashSet<string>(knownFiles ?? [], StringComparer.OrdinalIgnoreCase);
            _loadableFiles = new HashSet<string>(loadableFiles ?? [], StringComparer.OrdinalIgnoreCase);
        }

        public bool IsWindows { get; }

        public bool IsLinux { get; }

        internal static FakeNativeLibraryProbe Windows(
            IEnumerable<string>? knownFiles = null,
            IEnumerable<string>? loadableFiles = null)
            => new(true, false, knownFiles, loadableFiles);

        internal static FakeNativeLibraryProbe MacOS()
            => new(false, false);

        public bool TryLoad(string libraryName, out IntPtr handle)
        {
            handle = IntPtr.Zero;
            return _loadableFiles.Contains(libraryName);
        }

        public IReadOnlyList<string> FindLibraries(string searchPattern)
        {
            return _knownFiles
                .Where(file => Matches(searchPattern, file))
                .Select(file => $@"C:\fake\{file}")
                .ToArray();
        }

        private static bool Matches(string pattern, string value)
        {
            var regexPattern = "^" + Regex.Escape(pattern)
                .Replace("\\*", ".*", StringComparison.Ordinal)
                .Replace("\\?", ".", StringComparison.Ordinal) + "$";

            return Regex.IsMatch(value, regexPattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        }
    }
}
