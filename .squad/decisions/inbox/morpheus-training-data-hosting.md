# Decision: Training Data Hosting — Hybrid (GitHub + HuggingFace)

**Date:** 2026-03-30  
**Author:** Morpheus (Lead/Architect)  
**Status:** Proposed  
**Requested by:** Bruno Capuano

---

## Recommendation: Option 3 — Hybrid

Keep seed data in GitHub. Publish expanded dataset to HuggingFace Datasets.

---

## Analysis

### Option 1: GitHub Only

| Pros | Cons |
|------|------|
| Zero friction — `git clone` and everything works | Expanded dataset (5K+ examples, multi-MB) bloats repo history forever |
| CI tests validate format on every push | Invisible to ML community (HF Hub search, Papers with Code, etc.) |
| Single source of truth | No dataset versioning, no viewer, no download stats |
| .NET devs don't need HF accounts | GitHub isn't where people look for training data |

### Option 2: HuggingFace Only

| Pros | Cons |
|------|------|
| ML-native discoverability (search, tags, dataset viewer) | CI tests can't validate format without network calls to HF |
| Built-in dataset versioning and viewer | .NET devs must create HF account or use `datasets` library |
| Download metrics for community traction | Breaks `git clone → dotnet test` workflow |
| Standard practice for fine-tuning projects | Adds external dependency for reproducibility |

### Option 3: Hybrid ✅

| Pros | Cons |
|------|------|
| Seed data (~210 KB, 94 examples) stays in repo — CI tests pass offline | Two locations to keep in sync |
| Expanded dataset on HF — discoverable, versioned, viewable | Must document which source is canonical for what |
| `git clone → dotnet test` works out of the box | Slight overhead in `prepare_training_data.py` to push to HF |
| ML community finds it; .NET community doesn't need it | |
| Repo stays lean; HF handles scale | |

---

## Why Hybrid Wins

1. **CI already supports it.** `TrainingDataValidationTests.cs` uses `SkippableFact` for file-based tests — they pass when seed data exists and skip gracefully when expanded data isn't present. Zero changes needed.

2. **Audience split is real.** .NET devs clone the repo and run samples. ML researchers search HuggingFace for training datasets. Hybrid serves both without forcing either into an unfamiliar workflow.

3. **210 KB is fine for Git; 5K+ examples isn't.** The seed data is small enough that Git handles it without issue. The expanded dataset (downloaded from Glaive/Alpaca, processed, deduplicated) belongs in a purpose-built data platform.

4. **Precedent.** Projects like `microsoft/phi-3` and `unsloth/unsloth` keep minimal examples in-repo and publish full datasets separately. This is established practice.

5. **Models are already on HuggingFace.** Per the fine-tuning plan (Phase 4), models publish to `elbruno/Qwen2.5-{size}-LocalLLMs-{capability}`. Training data alongside models on the same platform is natural.

---

## Action Items

| # | Action | Owner | Effort |
|---|--------|-------|--------|
| 1 | Keep `training-data/` as-is in GitHub (seed data + README) | — | Done |
| 2 | Create HuggingFace Dataset repo: `elbruno/LocalLLMs-training-data` | Morpheus | 1 hour |
| 3 | Add `--push-to-hub` flag to `prepare_training_data.py` | Mouse | 2 hours |
| 4 | Add HF dataset link to `training-data/README.md` and `docs/fine-tuning-guide.md` | Morpheus | 30 min |
| 5 | Add `.gitignore` entry for expanded data files if generated locally (e.g., `training-data/expanded-*.json`) | Switch | 15 min |
| 6 | Tag HF dataset with `onnx`, `qwen2`, `tool-calling`, `dotnet`, `function-calling` for discoverability | Morpheus | 15 min |

**Total effort:** ~4 hours. No library code changes. No CI changes.

---

## Sync Strategy

- **GitHub `training-data/`**: Seed examples (human-written, format reference). Updated manually when format changes.
- **HuggingFace Dataset**: Full expanded dataset. Regenerated via `prepare_training_data.py --push-to-hub`. Versioned by HF's built-in git.
- **Canonical source for format**: GitHub (the spec lives in `docs/training-data-spec.md`; seed data is the reference implementation).
- **Canonical source for volume**: HuggingFace (5K+ examples for actual training runs).
