using System.Reflection;
using System.Runtime.InteropServices;
using ElBruno.LocalLLMs.BitNet;

namespace ElBruno.LocalLLMs.BitNet.Native;

/// <summary>
/// Resolves and loads the BitNet native library for P/Invoke.
/// </summary>
internal static class NativeLibraryLoader
{
    private static readonly object SyncLock = new();
    private static bool _initialized;
    private static string? _nativeLibraryPath;

    internal static void EnsureLoaded(string? nativeLibraryPath)
    {
        lock (SyncLock)
        {
            if (_initialized)
            {
                return;
            }

            _nativeLibraryPath = nativeLibraryPath;
            PrependToNativeSearchPath(nativeLibraryPath);

            NativeLibrary.SetDllImportResolver(
                typeof(LlamaNative).Assembly,
                Resolve);

            if (!TryLoadLibrary(out var handle))
            {
                var rid = RuntimeInformation.RuntimeIdentifier;
                var packageSuggestion = rid switch
                {
                    string r when r.StartsWith("win") && r.Contains("x64") => "ElBruno.LocalLLMs.BitNet.Native.win-x64",
                    string r when r.StartsWith("linux") && r.Contains("x64") => "ElBruno.LocalLLMs.BitNet.Native.linux-x64",
                    string r when r.StartsWith("osx") && r.Contains("arm64") => "ElBruno.LocalLLMs.BitNet.Native.osx-arm64",
                    _ => "ElBruno.LocalLLMs.BitNet.Native.{your-rid}"
                };

                throw new BitNetNativeLibraryException(
                    $"Unable to locate the BitNet native library (llama). " +
                    $"Install the platform-specific NuGet package:\n" +
                    $"  dotnet add package {packageSuggestion}\n" +
                    $"Or set BitNetOptions.NativeLibraryPath to the directory containing " +
                    $"llama.dll/libllama.so/libllama.dylib, " +
                    $"or add it to your PATH/LD_LIBRARY_PATH/DYLD_LIBRARY_PATH.");
            }

            NativeLibrary.Free(handle);
            _initialized = true;
        }
    }

    private static IntPtr Resolve(string libraryName, Assembly assembly, DllImportSearchPath? searchPath)
    {
        if (!string.Equals(libraryName, "llama", StringComparison.OrdinalIgnoreCase))
        {
            return IntPtr.Zero;
        }

        return TryLoadLibrary(out var handle) ? handle : IntPtr.Zero;
    }

    private static bool TryLoadLibrary(out IntPtr handle)
    {
        handle = IntPtr.Zero;

        foreach (var candidate in GetCandidateLibraryPaths())
        {
            if (NativeLibrary.TryLoad(candidate, out handle))
            {
                return true;
            }
        }

        return NativeLibrary.TryLoad("llama", out handle);
    }

    private static IEnumerable<string> GetCandidateLibraryPaths()
    {
        var libraryNames = GetLibraryFileNames();

        if (!string.IsNullOrWhiteSpace(_nativeLibraryPath))
        {
            if (File.Exists(_nativeLibraryPath))
            {
                yield return _nativeLibraryPath;
            }
            else
            {
                foreach (var name in libraryNames)
                {
                    yield return Path.Combine(_nativeLibraryPath, name);
                }
            }
        }

        foreach (var path in GetEnvironmentLibraryPaths())
        {
            foreach (var name in libraryNames)
            {
                yield return Path.Combine(path, name);
            }
        }

        foreach (var path in GetDefaultLibraryPaths())
        {
            foreach (var name in libraryNames)
            {
                yield return Path.Combine(path, name);
            }
        }

        // 4. NuGet runtimes path (from platform-specific native packages)
        foreach (var path in GetNuGetRuntimePaths())
        {
            foreach (var name in libraryNames)
            {
                yield return Path.Combine(path, name);
            }
        }
    }

    internal static IEnumerable<string> GetCandidateLibraryPathsForTesting()
        => GetCandidateLibraryPaths();

    private static IEnumerable<string> GetNuGetRuntimePaths()
    {
        var rid = RuntimeInformation.RuntimeIdentifier;

        // Check runtimes/{rid}/native/ relative to AppContext.BaseDirectory
        var basePath = Path.Combine(AppContext.BaseDirectory, "runtimes", rid, "native");
        if (Directory.Exists(basePath))
        {
            yield return basePath;
        }

        // Also check runtimes/{rid}/native/ relative to the assembly location
        var assemblyDir = Path.GetDirectoryName(typeof(NativeLibraryLoader).Assembly.Location);
        if (!string.IsNullOrEmpty(assemblyDir))
        {
            var assemblyPath = Path.Combine(assemblyDir, "runtimes", rid, "native");
            if (Directory.Exists(assemblyPath) && assemblyPath != basePath)
            {
                yield return assemblyPath;
            }
        }
    }

    private static IEnumerable<string> GetEnvironmentLibraryPaths()
    {
        var variable = OperatingSystem.IsWindows()
            ? "PATH"
            : OperatingSystem.IsMacOS()
                ? "DYLD_LIBRARY_PATH"
                : "LD_LIBRARY_PATH";

        var value = Environment.GetEnvironmentVariable(variable);
        if (string.IsNullOrWhiteSpace(value))
        {
            yield break;
        }

        foreach (var entry in value.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            yield return entry.Trim();
        }
    }

    private static IEnumerable<string> GetDefaultLibraryPaths()
    {
        yield return AppContext.BaseDirectory;
        yield return Environment.CurrentDirectory;
    }

    private static string[] GetLibraryFileNames()
    {
        if (OperatingSystem.IsWindows())
        {
            return ["llama.dll"];
        }

        if (OperatingSystem.IsMacOS())
        {
            return ["libllama.dylib"];
        }

        return ["libllama.so"];
    }

    private static void PrependToNativeSearchPath(string? nativeLibraryPath)
    {
        if (string.IsNullOrWhiteSpace(nativeLibraryPath))
        {
            return;
        }

        var variable = OperatingSystem.IsWindows()
            ? "PATH"
            : OperatingSystem.IsMacOS()
                ? "DYLD_LIBRARY_PATH"
                : "LD_LIBRARY_PATH";

        var current = Environment.GetEnvironmentVariable(variable) ?? string.Empty;
        if (current.Contains(nativeLibraryPath, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var updated = string.IsNullOrWhiteSpace(current)
            ? nativeLibraryPath
            : $"{nativeLibraryPath}{Path.PathSeparator}{current}";

        Environment.SetEnvironmentVariable(variable, updated);
    }
}
