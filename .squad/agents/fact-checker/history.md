# Fact Checker — History

## Learnings

Initial scaffold via `squad upgrade`. Ready for work.

---

## 2026-08-19 — Devil's Advocate: s1-mini Repo Boundary

**17:23:00-04:00:** Attacked Morpheus's STAY recommendation for `superwhisper/s1-mini` `TranscriptNormalizer` (brief at `.squad/decisions/inbox/morpheus-s1-mini-repo-boundary.md`), triggered by Bruno's two pushbacks toward `ElBruno.Speech`. Delivered `.squad/decisions/inbox/fact-checker-s1-mini-boundary-da.md`.

**Verdict: SPLIT is correct. Confidence: High.**

Interface (`ITranscriptNormalizer`) belongs in `ElBruno.Speech.Abstractions` now; s1-mini implementation stays in `ElBruno.LocalLLMs.Normalization` implementing it. This is the identical pattern Speech applies to STT/TTS/VAD/Chat.

Reason-by-reason grading of Morpheus's 5 reasons vs. SPLIT (not just MOVE):
- R1 (provider-agnostic): **WEAKENED** — actually endorses SPLIT (Speech pattern is interface-in-Abstractions).
- R2 (no dep edge): **COLLAPSES** vs. SPLIT — Speech.Abstractions has zero dep on LocalLLMs.
- R3 (no pipeline seam): **WEAKENED** — cost argument, not correctness.
- R4 (string→string = LocalLLMs pattern): **WEAKENED** — `IChatClient` is substrate, not domain. `TextSegmenter.cs` in Speech.Pipeline is `internal static` pure string ops, not a strong counter-example but not the decisive one hoped for.
- R5 (ORT-GenAI plumbing): **SURVIVES vs. MOVE, COLLAPSES vs. SPLIT** — implementation stays in LocalLLMs under SPLIT.

Key evidence found in Speech repo:
- `PRD.md:57-63` lists LocalLLMs as external provider — authored by Bruno July 2026, before s1-mini existed. Citing it as constraint against a feature it predates is circular.
- `PRD.md:778` "text normalizer must be replaceable" is inside **§12.6 TTS text segmentation** — refers to TTS pronunciation normalizer, NOT post-STT normalization. **PRD does not contain a post-STT roadmap item.** Both directions of that finding matter.
- Speech applies interface-in-Abstractions to every model capability: STT (`ISpeechToTextClient` → Whisper), Chat (`IChatClient` → LocalLLMs), TTS (`ITextToSpeechClient` → VibeVoice/QwenTTS), VAD (`IVoiceActivityDetector` → Silero). Normalization is the anomaly.
- `TextSegmenter.cs` is `internal static`, no `IChatClient`, no ML — doesn't collapse R4 as hoped.
- Morpheus's §6.1 already conceded SPLIT is "arguably the right API" but filed it as future refactor. Timing is the only disagreement.

Recommended next actions for team: define `ITranscriptNormalizer` in `ElBruno.Speech.Abstractions` (30 LOC), have `TranscriptNormalizer` implement it, ship in coordinated release. Discoverability resolved structurally (IntelliSense + NuGet namespace), not just via README callout.

No source files edited. No commits. Analysis only per task constraints.
