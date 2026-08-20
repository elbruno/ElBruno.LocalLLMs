# GPT-OSS 20B ONNX — Local Smoke Test

**Date:** 2026-08-12
**Model:** `onnxruntime/gpt-oss-20b-onnx`, CPU INT4 (`cpu_and_mobile/cpu-int4-rtn-block-32-acc-level-4`)
**Runtime:** `Microsoft.ML.OnnxRuntimeGenAI` 0.15.1, CPU execution provider
**Artifacts:** 12.26 GB (`model.onnx.data` 12,525 MB, `tokenizer.json` 26.6 MB)
**Local path:** `C:\models\gpt-oss-20b-onnx\...`
**Harness:** `src/samples/GptOssChat`

## Contract verification

The `chat_template.jinja` shipped inside the ONNX repository was compared byte-for-byte
against `openai/gpt-oss-20b/chat_template.jinja`. They are **identical** (16,714 chars),
so `HarmonyFormatter` is implemented against the authoritative template rather than a
blog description.

`genai_config.json` confirms native ORT-GenAI support:

```json
"model": { "type": "gptoss", "context_length": 131072,
           "decoder": { "num_hidden_layers": 24, "num_attention_heads": 64,
                        "num_key_value_heads": 8, "head_size": 64 } }
```

Special token IDs confirmed from `tokenizer_config.json`:
`<|return|>` 200002, `<|constrain|>` 200003, `<|channel|>` 200005, `<|start|>` 200006,
`<|end|>` 200007, `<|message|>` 200008, `<|call|>` 200012.

## Raw model output (captured directly from ORT-GenAI)

Answer:

```
<|channel|>analysis<|message|>Need answer: Paris.<|end|>
<|start|>assistant<|channel|>final<|message|>The capital of France is Paris.<|return|>
```

Tool call:

```
<|channel|>analysis<|message|>The user asks: "What is the weather in Paris?"
We need to call the get_weather function with city="Paris".<|end|>
<|start|>assistant<|channel|>commentary to=functions.get_weather <|constrain|>json<|message|>{"city":"Paris"}<|call|>
```

Both fixtures are pinned as regression tests in
`src/tests/ElBruno.LocalLLMs.Tests/ToolCalling/HarmonyRealModelOutputTests.cs`.

## Defect found and fixed during validation

The first end-to-end run produced corrupted output:

| Scenario | Before | After |
|---|---|---|
| Chat | `The capital of France is Paris..` | `Paris is the capital of France.` |
| Streaming | `Red / Blue / YellowYellow` | `Red / Blue / Yellow` |
| Tool calling | `get_weather()` | `get_weather(city=Paris)` |

**Root cause:** `OnnxGenAIModel` read the newest token with `GetSequence(0)[^1]` in both
the buffered and streaming loops. On the final iteration this re-reads the previous token,
duplicating it. Isolating the runtime confirmed it — the same prompt decoded through
`GetNextTokens()` produced `...Red.<|return|>` while `GetSequence(0)[^1]` produced `...Red..`.

The duplicated character also truncated the closing brace of tool-call JSON, which is why
arguments parsed as empty.

**Fix:** both loops now use `Generator.GetNextTokens()[0]`.

This bug was **pre-existing and affected every ONNX GenAI model**, not only GPT-OSS. It was
fixed because it directly corrupted the output of the feature being added.

## Final result

```
── Chat ──
Paris is the capital of France.

── Streaming ──
Red
Blue
Yellow

── Tool calling ──
Model requested: get_weather(city=Paris)
```

- Chain-of-thought was never surfaced in either the buffered or streaming path.
- No Harmony control markers leaked into user-visible text.
- Tool call name and arguments were recovered correctly from the commentary channel.

## Performance note

GPT-OSS 20B is a mixture-of-experts model. On CPU, each of the three scenarios above took
on the order of minutes. The `gpt-oss-20b-cuda` variant with
`Microsoft.ML.OnnxRuntimeGenAI.Cuda` is strongly preferred for interactive use. The CUDA
variant was **not** exercised in this run — no CUDA-capable PyTorch/ORT GPU device was
available in this environment.

## Unit test suite

`dotnet test src/tests/ElBruno.LocalLLMs.Tests` — **1481 passed, 0 failed**.
