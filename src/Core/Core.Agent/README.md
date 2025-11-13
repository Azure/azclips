# Core.Agent

A CLI-based conversational AI agent powered by Azure OpenAI or local models via LlamaSharp.

## Usage

### Cloud Mode (Default)
Uses Azure OpenAI:
```bash
dotnet run
```

Requires environment variables:
- `AZURE_OPENAI_ENDPOINT`
- `AZURE_OPENAI_API_KEY`
- `AZURE_OPENAI_DEPLOYMENT`

### Local Mode
Uses LlamaSharp with Phi-3.5-mini for standalone local inference:
```bash
dotnet run -- --local-model
```

**Features:**
- Automatically downloads Phi-3.5-mini-instruct model (~2.4GB) on first run
- Model cached in `~/.cache/llama_models/`
- No external services required - fully bundleable as standalone executable
- Progress indicator during model download and loading

**Platform Requirements:**
- ⚠️ **Currently requires x86-64 CPU with AVX2 support**
- Not compatible with Apple Silicon (ARM64) when running in emulation
- For Apple Silicon Macs, use Cloud Mode or run native ARM64 builds

Both modes use the Microsoft Agent Framework and maintain conversation history with streaming responses.

## Setup

1. **Create a `.env` file** in the project root (`/workspaces/cirrus/.env`):

```bash
AZURE_OPENAI_ENDPOINT=https://<your-resource>.openai.azure.com
AZURE_OPENAI_API_KEY=<your-api-key>
AZURE_OPENAI_DEPLOYMENT=gpt-4o-mini
```

**Note**: `.env` is already in `.gitignore` and won't be committed.

2. **Run the agent**:

```bash
dotnet run --project src/Core/Core.Agent/Core.Agent.csproj
```

## Usage

The agent prompts for one question and returns one answer:

```
Ask me anything: What is the capital of France?

Paris is the capital of France.
```
