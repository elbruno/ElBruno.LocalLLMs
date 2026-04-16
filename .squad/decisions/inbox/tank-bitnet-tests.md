# Decision: BitNet Test Coverage Strategy

**Date:** 2026-03-30  
**Author:** Tank (Tester/QA)  
**Status:** Active

## Context

BitNetChatClient constructor eagerly loads the native bitnet.cpp library via `EnsureInitializedAsync().GetAwaiter().GetResult()`. This means any test that instantiates `BitNetChatClient` with valid options will fail without the native library present.

## Decision

Split BitNet tests into two categories:

1. **Unit tests (155 tests, no native lib required):** Cover BitNetOptions, BitNetModelDefinition, BitNetKnownModels, BitNetKernelType, BitNetServiceExtensions (registration only, not resolution), exceptions, and BitNetChatClient constructor null-guards + type checks.

2. **Integration tests (future, gated):** Any test that resolves `IChatClient` from DI or tests actual inference must use `[Trait("Category", "Integration")]` and check for native library availability. Same pattern as Decision 13.

## Rationale

The constructor's eager native load is a design choice for fail-fast behavior. Unit tests must not depend on platform-specific native binaries. DI registration tests verify service descriptors without building the service provider's IChatClient resolution.

## Consequences

- CI runs all 155 unit tests without native dependencies
- Integration tests for BitNet inference require separate CI job with native lib installed
- BitNetServiceExtensions test for options propagation builds the provider to resolve `BitNetOptions` (safe — doesn't resolve `IChatClient`)
