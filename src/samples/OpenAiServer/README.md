# OpenAI-Compatible Local LLM Server

A minimal ASP.NET Core server that exposes local ONNX models via **OpenAI-compatible REST endpoints**. Drop-in replacement for OpenAI API — works with VS Code extensions (Continue, Cody, etc.), LangChain, and any OpenAI SDK client.

## Run

```bash
dotnet run --project src/samples/OpenAiServer
```

Default URL: `http://localhost:5000`

## Endpoints

| Method | Path | Description |
|--------|------|-------------|
| GET | `/` | Welcome message |
| GET | `/v1/models` | List available models |
| POST | `/v1/chat/completions` | Chat completion (streaming & non-streaming) |

## Usage with VS Code Copilot (Custom Model)

To use this server as a custom model in **VS Code Copilot**, add the following to your `chatLanguageModels.json` file:

> **Location:** `%APPDATA%/Code/User/chatLanguageModels.json`  
> (or `%APPDATA%/Code - Insiders/User/profiles/{profile-id}/chatLanguageModels.json` for Insiders)

```json
[
    {
        "name": "http://localhost:5000/v1/chat/completions",
        "vendor": "customoai",
        "models": [
            {
                "model": "phi-3.5-mini-instruct",
                "stream": true,
                "max_tokens": 2048,
                "temperature": 0.7,
                "toolCalling": true,
                "vision": false,
                "maxInputTokens": 4096,
                "maxOutputTokens": 2048
            },
            {
                "model": "qwen2.5-coder-7b-instruct",
                "stream": true,
                "max_tokens": 2048,
                "temperature": 0.7,
                "toolCalling": true,
                "vision": false,
                "maxInputTokens": 8192,
                "maxOutputTokens": 2048
            }
        ]
    }
]
```

> **Tip:** `phi-3.5-mini-instruct` works out of the box (has native ONNX). `qwen2.5-coder-7b-instruct` requires ONNX conversion first.

### Other Extensions (Continue, Cody, etc.)

Set the base URL to:

```
http://localhost:5000/v1
```

## Example: List models

```bash
curl http://localhost:5000/v1/models
```

## Example: Chat completion

```bash
curl http://localhost:5000/v1/chat/completions \
  -H "Content-Type: application/json" \
  -d '{
    "model": "phi-3.5-mini-instruct",
    "messages": [{"role": "user", "content": "Hello, who are you?"}],
    "stream": false,
    "max_tokens": 256,
    "temperature": 0.7
  }'
```

## Example: Streaming

```bash
curl http://localhost:5000/v1/chat/completions \
  -H "Content-Type: application/json" \
  -d '{
    "model": "phi-3.5-mini-instruct",
    "messages": [{"role": "user", "content": "Write a haiku about code"}],
    "stream": true
  }'
```

## Notes

- Default model: **Phi-3.5 mini instruct** (has native ONNX, works out of the box)
- Models are downloaded automatically on first use (~2-4 GB depending on model)
- The `model` field in requests maps to `KnownModels.All` IDs
- Token usage counts are approximate (reported as 0 in current version)
