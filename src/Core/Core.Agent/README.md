# Core.Agent

Simple CLI agent using Microsoft Agent Framework with Azure OpenAI.

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
