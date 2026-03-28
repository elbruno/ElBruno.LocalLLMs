# Trinity Wave 1 Decisions

**Date:** 2025-07-25
**Author:** Trinity (Core Dev)

## Decision: Exception hierarchy uses abstract base LocalLLMException

All library-specific exceptions derive from `LocalLLMException : Exception`. This gives consumers a single catch type for all library errors while preserving specific subtypes for targeted handling. `ExecutionProviderException` includes `Provider` and `Suggestion` properties for actionable diagnostics.

## Decision: ShouldFallbackToNextProvider uses explicit initialProvider parameter (not default)

The 2-arg overload defaults to strict matching (provider-specific token required). Only the Auto loop in the constructor passes `ExecutionProvider.Auto` to enable the generic fast-path. This prevents accidental fallback when users explicitly request a provider.

## Decision: ILogger is optional throughout — null defaults everywhere

`LocalChatClient` and `OnnxGenAIModel` accept optional logger parameters that default to `NullLogger`. No breaking changes to existing constructors or factory methods. DI path auto-resolves `ILoggerFactory` from the container.

## Decision: OptionsValidator runs in CreateAsync only, not in constructor

Validation in the async factory path catches invalid options early. The synchronous constructor does NOT validate — this avoids breaking existing code that constructs with intent to modify options later. Constructor-time validation would be a breaking behavioral change.
