# Decision: Fine-Tuning Documentation Completeness

**Date:** 2026-03-28
**Author:** Trinity (Core Dev)
**Status:** Proposed

## Context

Bruno requested a review of all fine-tuning documentation and samples. The fine-tuning guide and FineTunedToolCalling sample were already solid, but several cross-references were missing: the root README, getting-started.md, and supported-models.md did not mention fine-tuned models at all.

## Decision

1. **No separate RAG fine-tuning sample needed.** The existing RagChatbot sample covers the RAG pipeline, and the fine-tuning guide covers the `Qwen25_05B_RAG` model. Adding the model recommendation to RagChatbot's README is sufficient.

2. **Fine-tuned models should appear in every model selection surface.** README model table, supported-models.md, and getting-started.md decision tree all now include fine-tuned variants so developers discover them naturally.

3. **Keep fine-tuning guide as the canonical deep reference.** Other docs link to it rather than duplicating content.

## Consequences

- Developers browsing README, getting-started, or supported-models will discover fine-tuned options without needing to find the fine-tuning guide first.
- RagChatbot users see a natural next step toward the fine-tuned RAG model.
