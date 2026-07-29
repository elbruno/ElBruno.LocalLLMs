# MagenticBrainAgent

Round-based local agentic sample using `ElBruno.LocalLLMs` with `KnownModels.MagenticBrain`.

## Run

```bash
dotnet run --project src/samples/MagenticBrainAgent
```

## What it shows

- Tool registration with `AIFunctionFactory`
- Multi-round `GetResponseAsync(..., new ChatOptions { Tools = ... })` loop
- `submit` as terminal tool signal
- Local auto-download of `elbruno/MagenticBrain-onnx` on first run
