using System.Reflection;
using System.Runtime.InteropServices;
using ElBruno.LocalLLMs.Diagnostics;
using Microsoft.Extensions.Logging;

namespace ElBruno.LocalLLMs.Internal;

internal sealed record ExecutionProviderPreflightResult(
    ExecutionProviderDiagnosticStatus Status,
    Exception? Exception = null,
    string? Suggestion = null)
{
    internal bool IsAvailable => Status == ExecutionProviderDiagnosticStatus.Available;

    internal bool IsUnavailable => Status == ExecutionProviderDiagnosticStatus.Unavailable;

    internal static ExecutionProviderPreflightResult Available { get; } =
        new(ExecutionProviderDiagnosticStatus.Available);

    internal static ExecutionProviderPreflightResult Failure(Exception exception, string? suggestion)
    {
        ArgumentNullException.ThrowIfNull(exception);
        return new ExecutionProviderPreflightResult(
            ExecutionProviderDiagnosticStatus.Unavailable,
            exception,
            suggestion);
    }

    internal static ExecutionProviderPreflightResult Unknown(Exception exception, string? suggestion)
    {
        ArgumentNullException.ThrowIfNull(exception);
        return new ExecutionProviderPreflightResult(
            ExecutionProviderDiagnosticStatus.Unknown,
            exception,
            suggestion);
    }
}

internal sealed record ProviderInitializationResult<TModel>(
    TModel Model,
    ExecutionProvider ActiveProvider,
    string? ProviderSelectionDetails);

internal static class ExecutionProviderSelection
{
    internal static ProviderInitializationResult<TModel> InitializeModel<TModel>(
        ExecutionProvider requestedProvider,
        ILogger logger,
        Func<ExecutionProvider, TModel> createModel,
        Func<ExecutionProvider, ExecutionProviderPreflightResult>? preflight = null)
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(createModel);

        preflight ??= ExecutionProviderPreflight.Validate;

        var providerFailures = new List<string>();

        if (requestedProvider == ExecutionProvider.Auto)
        {
            var candidates = GetProviderFallbackOrder(requestedProvider);
            for (var i = 0; i < candidates.Count; i++)
            {
                var candidate = candidates[i];
                var nextProvider = i + 1 < candidates.Count ? candidates[i + 1] : ExecutionProvider.Cpu;

                var preflightResult = preflight(candidate);
                if (preflightResult.IsUnavailable)
                {
                    var reason = BuildProviderFailureReason(
                        candidate,
                        preflightResult.Exception ?? new InvalidOperationException($"{candidate} preflight failed."));
                    providerFailures.Add(reason);
                    LogMessages.ProviderFallback(logger, candidate, nextProvider, reason);
                    continue;
                }

                try
                {
                    LogMessages.ProviderAttempt(logger, candidate);
                    var model = createModel(candidate);
                    var selectionDetails = providerFailures.Count > 0
                        ? $"Auto selected {candidate} after provider fallbacks: {string.Join(" | ", providerFailures)}"
                        : null;

                    return new ProviderInitializationResult<TModel>(model, candidate, selectionDetails);
                }
                catch (Exception ex) when (candidate != ExecutionProvider.Cpu && ShouldFallbackToNextProvider(candidate, ex, ExecutionProvider.Auto))
                {
                    var reason = BuildProviderFailureReason(candidate, ex);
                    providerFailures.Add(reason);
                    LogMessages.ProviderFallback(logger, candidate, nextProvider, reason);
                }
                catch (Exception ex) when (candidate != ExecutionProvider.Cpu)
                {
                    LogMessages.ModelInitError(logger, $"Hard error with provider {candidate}", ex);
                    throw new ExecutionProviderException(
                        $"Failed to initialize model with provider {candidate}. This was treated as a hard error (no fallback).",
                        candidate,
                        ex);
                }
            }

            var details = providerFailures.Count > 0
                ? " Failures: " + string.Join(" | ", providerFailures)
                : string.Empty;

            throw new ExecutionProviderException(
                "Unable to initialize model with any execution provider." + details,
                ExecutionProvider.Auto);
        }

        var requestedPreflight = preflight(requestedProvider);
        if (requestedPreflight.IsUnavailable)
        {
            var preflightException = requestedPreflight.Exception ??
                new InvalidOperationException($"{requestedProvider} preflight failed.");

            LogMessages.ModelInitError(logger, $"Provider {requestedProvider} preflight failed", preflightException);
            throw CreateUnavailableException(requestedProvider, preflightException, requestedPreflight.Suggestion);
        }

        try
        {
            LogMessages.ProviderAttempt(logger, requestedProvider);
            var model = createModel(requestedProvider);
            return new ProviderInitializationResult<TModel>(model, requestedProvider, null);
        }
        catch (Exception ex) when (requestedProvider != ExecutionProvider.Cpu && IsProviderNotInstalledError(requestedProvider, ex))
        {
            LogMessages.ModelInitError(logger, $"Provider {requestedProvider} not installed", ex);
            throw CreateUnavailableException(requestedProvider, ex);
        }
    }

    internal static IReadOnlyList<ExecutionProvider> GetProviderFallbackOrder(ExecutionProvider provider) =>
        provider switch
        {
            ExecutionProvider.Auto => OperatingSystem.IsWindows()
                ? [ExecutionProvider.DirectML, ExecutionProvider.Cuda, ExecutionProvider.Cpu]
                : [ExecutionProvider.Cuda, ExecutionProvider.Cpu],
            _ => [provider]
        };

    internal static bool IsProviderNotInstalledError(ExecutionProvider provider, Exception ex)
    {
        ArgumentNullException.ThrowIfNull(ex);
        return ShouldFallbackToNextProvider(provider, ex, provider);
    }

    internal static bool ShouldFallbackToNextProvider(
        ExecutionProvider provider,
        Exception ex,
        ExecutionProvider initialProvider)
    {
        ArgumentNullException.ThrowIfNull(ex);

        var message = ex.ToString();
        if (string.IsNullOrWhiteSpace(message))
        {
            return false;
        }

        var normalized = message.ToLowerInvariant();

        if (initialProvider == ExecutionProvider.Auto)
        {
            if (ex is DllNotFoundException or BadImageFormatException or EntryPointNotFoundException)
            {
                return true;
            }

            if (normalized.Contains("is not supported", StringComparison.Ordinal) ||
                normalized.Contains("not available", StringComparison.Ordinal) ||
                normalized.Contains("is unavailable", StringComparison.Ordinal) ||
                normalized.Contains("specified provider", StringComparison.Ordinal))
            {
                return true;
            }
        }

        var providerToken = provider switch
        {
            ExecutionProvider.Cuda => "cuda",
            ExecutionProvider.DirectML => "dml",
            _ => provider.ToString().ToLowerInvariant()
        };

        var hasProviderContext = normalized.Contains(providerToken, StringComparison.Ordinal) ||
            (provider == ExecutionProvider.DirectML && normalized.Contains("directml", StringComparison.Ordinal));

        if (!hasProviderContext)
        {
            return false;
        }

        return normalized.Contains("failed to load", StringComparison.Ordinal) ||
               normalized.Contains("not found", StringComparison.Ordinal) ||
               normalized.Contains("not supported", StringComparison.Ordinal) ||
               normalized.Contains("is unavailable", StringComparison.Ordinal) ||
               normalized.Contains("provider is unavailable", StringComparison.Ordinal) ||
               normalized.Contains("is not enabled", StringComparison.Ordinal) ||
               normalized.Contains("not been built with", StringComparison.Ordinal) ||
               normalized.Contains("could not be created", StringComparison.Ordinal) ||
               normalized.Contains("no available provider", StringComparison.Ordinal) ||
               normalized.Contains("unable to find", StringComparison.Ordinal) ||
               normalized.Contains("cannot load", StringComparison.Ordinal) ||
               normalized.Contains("not available", StringComparison.Ordinal);
    }

    internal static string BuildProviderFailureReason(ExecutionProvider provider, Exception ex)
    {
        ArgumentNullException.ThrowIfNull(ex);

        var message = ex.Message.Replace(Environment.NewLine, " ", StringComparison.Ordinal).Trim();
        if (message.Length > 180)
        {
            message = message[..180] + "...";
        }

        return $"{provider}: {ex.GetType().Name}: {message}";
    }

    internal static ExecutionProviderException CreateUnavailableException(
        ExecutionProvider provider,
        Exception ex,
        string? suggestion = null)
    {
        ArgumentNullException.ThrowIfNull(ex);

        suggestion ??= GetUnavailableSuggestion(provider);

        var message = $"The {provider} execution provider is not available. ";
        if (!string.IsNullOrWhiteSpace(suggestion))
        {
            message += suggestion + " ";
        }

        message += $"Inner error: {ex.Message}";

        return new ExecutionProviderException(
            message,
            provider,
            suggestion,
            ex);
    }

    internal static string? GetUnavailableSuggestion(ExecutionProvider provider) =>
        provider switch
        {
            ExecutionProvider.Cuda =>
                "Add the 'Microsoft.ML.OnnxRuntimeGenAI.Cuda' NuGet package to your application project and ensure CUDA 13.*, cuDNN 9.*, and the latest Microsoft Visual C++ 2015-2022 runtime are installed. " +
                "Replace 'Microsoft.ML.OnnxRuntimeGenAI' with 'Microsoft.ML.OnnxRuntimeGenAI.Cuda' — do not reference both packages simultaneously. " +
                "The CUDA 13 build expects libraries such as cublas64_13, cublasLt64_13, cudart64_13, and cudnn64_9 to resolve before native model creation.",
            ExecutionProvider.DirectML =>
                "Add the 'Microsoft.ML.OnnxRuntimeGenAI.DirectML' NuGet package to your application project and ensure the required runtime is installed. " +
                "Replace 'Microsoft.ML.OnnxRuntimeGenAI' with 'Microsoft.ML.OnnxRuntimeGenAI.DirectML' — do not reference both packages simultaneously.",
            _ => null
        };
}

internal static class ExecutionProviderPreflight
{
    private static readonly string[] WindowsCudaProviderLibraries =
    [
        "onnxruntime-genai-cuda.dll",
        "onnxruntime_providers_cuda.dll"
    ];

    private static readonly string[] LinuxCudaProviderLibraries =
    [
        "libonnxruntime-genai-cuda.so",
        "libonnxruntime_providers_cuda.so"
    ];

    private static readonly NativeLibraryRequirement[] WindowsCudaRuntimeLibraries =
    [
        new("cuda.dll", null, "NVIDIA driver library"),
        new("cudart64_13.dll", "cudart64_*.dll", "CUDA runtime library"),
        new("cublas64_13.dll", "cublas64_*.dll", "CUDA BLAS library"),
        new("cublasLt64_13.dll", "cublasLt64_*.dll", "CUDA BLAS Lt library"),
        new("cudnn64_9.dll", "cudnn64_*.dll", "cuDNN runtime library")
    ];

    private static readonly NativeLibraryRequirement[] LinuxCudaRuntimeLibraries =
    [
        new("libcuda.so.1", "libcuda.so*", "NVIDIA driver library"),
        new("libcudart.so.13", "libcudart.so*", "CUDA runtime library"),
        new("libcublas.so.13", "libcublas.so*", "CUDA BLAS library"),
        new("libcublasLt.so.13", "libcublasLt.so*", "CUDA BLAS Lt library"),
        new("libcudnn.so.9", "libcudnn.so*", "cuDNN runtime library")
    ];

    internal static ExecutionProviderPreflightResult Validate(ExecutionProvider provider)
        => Validate(provider, new DefaultNativeLibraryProbe());

    internal static ExecutionProviderPreflightResult Validate(
        ExecutionProvider provider,
        INativeLibraryProbe probe)
    {
        ArgumentNullException.ThrowIfNull(probe);

        return provider switch
        {
            ExecutionProvider.Auto or ExecutionProvider.Cpu => ExecutionProviderPreflightResult.Available,
            ExecutionProvider.DirectML => probe.IsWindows
                ? ExecutionProviderPreflightResult.Unknown(
                    new InvalidOperationException(
                        "DirectML availability cannot be confirmed from provider preflight alone. Initialize a model in an application that references Microsoft.ML.OnnxRuntimeGenAI.DirectML to verify it."),
                    ExecutionProviderSelection.GetUnavailableSuggestion(ExecutionProvider.DirectML))
                : ExecutionProviderPreflightResult.Failure(
                    new PlatformNotSupportedException("DirectML is only supported on Windows."),
                    ExecutionProviderSelection.GetUnavailableSuggestion(ExecutionProvider.DirectML)),
            ExecutionProvider.Cuda => ValidateCuda(probe),
            _ => ExecutionProviderPreflightResult.Failure(
                new ArgumentOutOfRangeException(nameof(provider), provider, "Unknown execution provider."),
                null)
        };
    }

    private static ExecutionProviderPreflightResult ValidateCuda(INativeLibraryProbe probe)
    {
        var suggestion = ExecutionProviderSelection.GetUnavailableSuggestion(ExecutionProvider.Cuda);

        if (!probe.IsWindows && !probe.IsLinux)
        {
            return ExecutionProviderPreflightResult.Failure(
                new PlatformNotSupportedException("CUDA execution is only supported on Windows and Linux."),
                suggestion);
        }

        var runtimeRequirements = probe.IsWindows
            ? WindowsCudaRuntimeLibraries
            : LinuxCudaRuntimeLibraries;

        foreach (var requirement in runtimeRequirements)
        {
            var result = ValidateRequirement(probe, requirement, suggestion);
            if (!result.IsAvailable)
            {
                return result;
            }
        }

        var providerLibraries = probe.IsWindows
            ? WindowsCudaProviderLibraries
            : LinuxCudaProviderLibraries;

        foreach (var libraryName in providerLibraries)
        {
            if (probe.TryLoad(libraryName, out _))
            {
                continue;
            }

            var exactMatches = GetDistinctLibraryNames(probe.FindLibraries(libraryName));
            var message = exactMatches.Count > 0
                ? $"CUDA provider library '{libraryName}' was found but could not be loaded."
                : $"CUDA provider library '{libraryName}' was not found on the native library search path.";

            return ExecutionProviderPreflightResult.Failure(
                new DllNotFoundException(message),
                suggestion);
        }

        return ExecutionProviderPreflightResult.Available;
    }

    private static ExecutionProviderPreflightResult ValidateRequirement(
        INativeLibraryProbe probe,
        NativeLibraryRequirement requirement,
        string? suggestion)
    {
        if (probe.TryLoad(requirement.LibraryName, out _))
        {
            return ExecutionProviderPreflightResult.Available;
        }

        var exactMatches = GetDistinctLibraryNames(probe.FindLibraries(requirement.LibraryName));
        if (exactMatches.Count > 0)
        {
            return ExecutionProviderPreflightResult.Failure(
                new DllNotFoundException(
                    $"{requirement.Description} '{requirement.LibraryName}' was found but could not be loaded."),
                suggestion);
        }

        if (!string.IsNullOrWhiteSpace(requirement.AlternativeSearchPattern))
        {
            var alternatives = GetDistinctLibraryNames(probe.FindLibraries(requirement.AlternativeSearchPattern))
                .Where(name => !string.Equals(name, requirement.LibraryName, StringComparison.OrdinalIgnoreCase))
                .ToArray();

            if (alternatives.Length > 0)
            {
                return ExecutionProviderPreflightResult.Failure(
                    new DllNotFoundException(
                        $"{requirement.Description} mismatch: expected '{requirement.LibraryName}' but found {string.Join(", ", alternatives)} on the native library search path."),
                    suggestion);
            }
        }

        return ExecutionProviderPreflightResult.Failure(
            new DllNotFoundException(
                $"{requirement.Description} '{requirement.LibraryName}' was not found on the native library search path."),
            suggestion);
    }

    private static IReadOnlyList<string> GetDistinctLibraryNames(IEnumerable<string> paths)
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var path in paths)
        {
            var name = Path.GetFileName(path);
            if (!string.IsNullOrWhiteSpace(name))
            {
                names.Add(name);
            }
        }

        return names.ToArray();
    }
}

internal interface INativeLibraryProbe
{
    bool IsWindows { get; }

    bool IsLinux { get; }

    bool TryLoad(string libraryName, out IntPtr handle);

    IReadOnlyList<string> FindLibraries(string searchPattern);
}

file sealed class DefaultNativeLibraryProbe : INativeLibraryProbe
{
    private readonly Assembly _assembly = typeof(ExecutionProviderSelection).Assembly;

    public bool IsWindows => OperatingSystem.IsWindows();

    public bool IsLinux => OperatingSystem.IsLinux();

    public bool TryLoad(string libraryName, out IntPtr handle)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(libraryName);

        if (NativeLibrary.TryLoad(libraryName, _assembly, searchPath: null, out handle))
        {
            NativeLibrary.Free(handle);
            return true;
        }

        foreach (var candidate in GetCandidatePaths(libraryName))
        {
            if (NativeLibrary.TryLoad(candidate, out handle))
            {
                NativeLibrary.Free(handle);
                return true;
            }
        }

        handle = IntPtr.Zero;
        return false;
    }

    public IReadOnlyList<string> FindLibraries(string searchPattern)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(searchPattern);

        var matches = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var directory in GetSearchDirectories())
        {
            try
            {
                if (!Directory.Exists(directory))
                {
                    continue;
                }

                foreach (var file in Directory.EnumerateFiles(directory, searchPattern, SearchOption.TopDirectoryOnly))
                {
                    matches.Add(file);
                }
            }
            catch
            {
            }
        }

        return matches.ToArray();
    }

    private IEnumerable<string> GetCandidatePaths(string libraryName)
    {
        foreach (var directory in GetSearchDirectories())
        {
            yield return Path.Combine(directory, libraryName);
        }
    }

    private IEnumerable<string> GetSearchDirectories()
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var directory in EnumerateBaseDirectories())
        {
            if (!string.IsNullOrWhiteSpace(directory) && seen.Add(directory))
            {
                yield return directory;
            }
        }

        var variable = IsWindows
            ? "PATH"
            : IsLinux
                ? "LD_LIBRARY_PATH"
                : "DYLD_LIBRARY_PATH";

        var searchPath = Environment.GetEnvironmentVariable(variable);
        if (string.IsNullOrWhiteSpace(searchPath))
        {
            yield break;
        }

        foreach (var entry in searchPath.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (seen.Add(entry))
            {
                yield return entry;
            }
        }
    }

    private IEnumerable<string> EnumerateBaseDirectories()
    {
        yield return AppContext.BaseDirectory;
        yield return Environment.CurrentDirectory;

        if (IsWindows)
        {
            yield return Environment.SystemDirectory;
        }

        var rid = RuntimeInformation.RuntimeIdentifier;
        if (!string.IsNullOrWhiteSpace(rid))
        {
            yield return Path.Combine(AppContext.BaseDirectory, "runtimes", rid, "native");

            var assemblyDirectory = Path.GetDirectoryName(_assembly.Location);
            if (!string.IsNullOrWhiteSpace(assemblyDirectory))
            {
                yield return Path.Combine(assemblyDirectory, "runtimes", rid, "native");
            }
        }
    }
}

internal sealed record NativeLibraryRequirement(
    string LibraryName,
    string? AlternativeSearchPattern,
    string Description);
