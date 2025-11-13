# Core.Agent

An interactive Azure CLI and PowerShell command assistant powered by Azure OpenAI or local models via LlamaSharp.

## What It Does

This agent specializes in Azure command-line operations with three modes:
1. **"How to" questions** - Get command syntax with placeholders
2. **"Do it for me" requests** - Interactive gathering of parameters with final complete commands
3. **Command validation** - Verify Azure CLI/PowerShell command correctness

The agent maintains conversation history and uses streaming responses for real-time interaction.

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
- Cross-platform support including Apple Silicon

Both modes use the Microsoft Agent Framework (`Microsoft.Agents.AI`) with conversation history and streaming responses.

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
