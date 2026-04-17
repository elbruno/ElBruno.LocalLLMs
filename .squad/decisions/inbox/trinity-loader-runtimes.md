# Decision: NativeLibraryLoader probes NuGet runtimes/{rid}/native/

**Date:** 2026-04-17  
**Author:** Trinity (Core Dev)  
**Status:** Implemented

## Context

Platform-specific native NuGet packages (e.g., `ElBruno.LocalLLMs.BitNet.Native.win-x64`) ship pre-built bitnet.cpp binaries under `runtimes/{rid}/native/`. The .NET SDK usually copies these to the output directory, but self-contained deployments and unusual build configurations may leave them only in the runtimes/ folder.

## Decision

Add `runtimes/{rid}/native/` as a probing path in `NativeLibraryLoader.GetCandidateLibraryPaths()`, after the existing default paths (AppContext.BaseDirectory, CurrentDirectory). Probe relative to both `AppContext.BaseDirectory` and the assembly's own location.

## Search Order (final)

1. User-specified path (`BitNetOptions.NativeLibraryPath`)
2. Environment paths (PATH / LD_LIBRARY_PATH / DYLD_LIBRARY_PATH)
3. Default paths (AppContext.BaseDirectory, CurrentDirectory)
4. **NuGet runtimes paths** (`runtimes/{rid}/native/` relative to base dir and assembly dir)
5. OS default search (`NativeLibrary.TryLoad("llama")`)

## Rationale

- Safety net for edge cases where the SDK doesn't copy native binaries to output
- Uses `RuntimeInformation.RuntimeIdentifier` for exact RID matching
- Only yields paths that actually exist on disk (Directory.Exists check)
- Zero impact on existing scenarios — new paths are checked last before OS fallback

## Error Message Enhancement

When no library is found, the exception now suggests the correct platform-specific NuGet package based on the detected RID, making the fix self-documenting for users.
