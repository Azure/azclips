# Core.Agent

An interactive Azure CLI and PowerShell command assistant with three deployment options:
1. **Azure OpenAI** (default) - Cloud-based using Azure OpenAI service
2. **Local Model** - Fully offline using Phi-3.5-mini via LlamaSharp
3. **Azure Copilot** - Production Copilot service with CLI/PowerShell handlers

## What It Does

This agent specializes in Azure command-line operations with three interaction types:
1. **"How to" questions** - Get command syntax with placeholders
2. **"Do it for me" requests** - Interactive gathering of parameters with final complete commands
3. **Command validation** - Verify Azure CLI/PowerShell command correctness

The agent maintains conversation history and uses streaming responses for real-time interaction.

## Usage

### Mode 1: Cloud (Azure OpenAI) - Default
Uses Azure OpenAI for production-grade responses:
```bash
dotnet run
```

Requires environment variables:
- `AZURE_OPENAI_ENDPOINT`
- `AZURE_OPENAI_API_KEY`
- `AZURE_OPENAI_DEPLOYMENT`

**Features:**
- Production-grade model quality
- No local resource requirements
- Scalable and easy to update

### Mode 2: Local Model (Phi-3.5-mini)
Uses LlamaSharp with Phi-3.5-mini for standalone local inference:
```bash
dotnet run -- --local-model
```

**Features:**
- Automatically downloads Phi-3.5-mini-instruct model (~2.4GB) on first run
- Model cached in `~/.cache/llama_models/`
- No external services required - fully bundleable as standalone executable
- Progress indicator during model download and loading
- Cross-platform support including Apple Silicon
- Full privacy - data never leaves the machine
- Works offline

### Mode 3: Azure Copilot Service
Uses Azure's production Copilot infrastructure with CLI/PowerShell handlers:
```bash
dotnet run -- --use-copilot
```

Authentication via Azure SDK `DefaultAzureCredential` (Azure CLI, Managed Identity, etc.).

**Features:**
- Leverages Azure's production Copilot infrastructure
- Built-in grounding to Azure documentation and schemas
- Telemetry and monitoring included
- Consistent experience with Azure Portal Copilot
- Enterprise-grade security and compliance

All modes use the Microsoft Agent Framework (`Microsoft.Agents.AI`) with conversation history and streaming responses.

## Setup

### For Cloud Mode (Default)

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

### For Local Model Mode

No setup required! Just run:

```bash
dotnet run --project src/Core/Core.Agent/Core.Agent.csproj -- --local-model
```

The Phi-3.5-mini model will be downloaded automatically on first run (~2.4GB).

### For Azure Copilot Mode

Ensure you're authenticated with Azure (e.g., `az login`):

```bash
dotnet run --project src/Core/Core.Agent/Core.Agent.csproj -- --use-copilot
```

## Example Interactions

### "How to" Question
```
Ask me anything: How do I create a storage account?

Azure CLI:
az storage account create --name <account-name> --resource-group <rg-name> --location <location>

Azure PowerShell:
New-AzStorageAccount -Name <account-name> -ResourceGroupName <rg-name> -Location <location>
[DONE]
```

### "Do it for me" Request
```
Ask me anything: Create a VM for me

> What is the name of your VM?
my-test-vm

> What resource group should I use?
my-rg

> Which region/location?
eastus

> What VM size do you need?
Standard_B2s

Azure CLI:
az vm create --name my-test-vm --resource-group my-rg --location eastus --size Standard_B2s

Azure PowerShell:
New-AzVM -Name my-test-vm -ResourceGroupName my-rg -Location eastus -Size Standard_B2s
[DONE]
```

The agent continues asking questions until it has all required information, then provides complete commands. Type `cancel`, `quit`, or `exit` to end the conversation early.
