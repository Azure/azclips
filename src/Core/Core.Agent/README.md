# Core.Agent

An AI-powered natural language interface for Azure CLI that understands intent and generates executable commands.

## Architecture

Core.Agent implements the `ICommandEngine` interface and integrates with the Core CLI through a **fallback pattern**:

1. **DefaultCommandEngine** attempts to parse the command using standard syntax (e.g., `az group create --name test`)
2. If parsing fails, control passes to **AgentEngine** which interprets natural language queries
3. When AgentEngine generates an executable command, it hands control back to DefaultCommandEngine for execution

This allows seamless switching between traditional command syntax and natural language:
- `az group create --name test --location eastus` → DefaultCommandEngine executes directly
- `az create a resource group called test in east us` → AgentEngine interprets, generates command, DefaultCommandEngine executes
- `az I would like to login` → AgentEngine provides guidance with command examples

## Agent Capabilities

The agent supports three interaction types:

1. **"How to" questions** - Returns command syntax with placeholders (not executable)
2. **"Do it for me" requests** - Generates complete, executable commands from natural language
3. **Command validation** - Confirms and executes complete commands after user approval

The agent maintains conversation history and streams responses in real-time.

## Deployment Options

1. **Azure OpenAI** (default) - Cloud-based using Azure OpenAI service
2. **Local Model** - Fully offline using Phi-3.5-mini via LlamaSharp
3. **Azure Copilot** - Production Copilot service with Azure documentation grounding

## Program.cs - Testing Interface

The `Program.cs` file provides a **standalone executable for testing and evaluation**. It allows running the agent independently from the Core CLI for development purposes. In production, the agent is invoked through `AgentEngine` as part of the Core CLI fallback mechanism.

## Setup

Create a `.env` file in `/workspaces/cirrus/src/Core/Core.Agent/`:

```bash
AZURE_OPENAI_ENDPOINT=https://<your-resource>.openai.azure.com
AZURE_OPENAI_API_KEY=<your-api-key>
AZURE_OPENAI_DEPLOYMENT=gpt-4o-mini
```

**Note**: `.env` is in `.gitignore` and won't be committed.

## Running the Agent

### Through Core CLI (Production)
```bash
az create a storage account called mystore in westus
```

The agent automatically activates when DefaultCommandEngine cannot parse the command.

### Standalone Testing (via Program.cs)

**Cloud mode (default):**
```bash
dotnet run --project src/Core/Core.Agent/Core.Agent.csproj
```

**Local model:**
```bash
dotnet run --project src/Core/Core.Agent/Core.Agent.csproj -- --local-model
```
Automatically downloads Phi-3.5-mini (~2.4GB) on first run to `~/.cache/llama_models/`.

**Azure Copilot:**
```bash
dotnet run --project src/Core/Core.Agent/Core.Agent.csproj -- --use-copilot
```
Requires Azure authentication (e.g., `az login`).

## Example Interactions

### Natural Language Command
```
$ az create a resource group called mygroup in westus

[Agent generates and confirms]
az group create --name mygroup --location westus

Execute this command? (yes/no): yes
[Command executes via DefaultCommandEngine]
```

### "How to" Question
```
$ az how do I create a storage account

Azure CLI:
az storage account create --name <account-name> --resource-group <rg-name> --location <location>
[DONE - not executable, contains placeholders]
```

### Command with Typo
```
$ az I would like to logni

Azure CLI:
az login
az login --service-principal --username <appId> --password <password> --tenant <tenantId>
[DONE]
```
