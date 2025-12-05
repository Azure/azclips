# Agent Experience Design Specification

## Overview

Core.Agent is an AI-powered natural language interface for Azure CLI and Azure PowerShell that interprets user intent and generates executable commands. It integrates through a **fallback mechanism**: when standard command parsing fails, the agent interprets the natural language input and can hand back executable commands to the execution engine.

**Note:** For comprehensive architecture details including both CLI (azv2) and PowerShell integration patterns, see [ARCHITECTURE.md](../../src-v2/ARCHITECTURE.md).

## Architecture

### Integration with Core CLI

The agent implements `ICommandEngine` and operates as a fallback to the `DefaultCommandEngine`:

```
┌──────────────────────────────────────────────────────────────┐
│                    User Input: "az ..."                      │
└────────────────────────────┬─────────────────────────────────┘
                             │
                             ▼
              ┌──────────────────────────────┐
              │   OrchestratorEngine         │
              └──────────────┬───────────────┘
                             │
                ┌────────────┴───────────┐
                │                        │
                ▼                        ▼
    ┌─────────────────────┐  ┌──────────────────────┐
    │ DefaultCommandEngine│  │    AgentEngine       │
    │  (Standard Parser)  │  │ (Natural Language)   │
    └──────────┬──────────┘  └──────────┬───────────┘
               │                        │
               │ Success                │ Parse Failed
               ▼                        │
    ┌──────────────────┐                │
    │ Execute Command  │                │
    └──────────────────┘                │
                                        ▼
                            ┌────────────────────────┐
                            │  Interpret & Generate  │
                            └────────────┬───────────┘
                                         │
                            ┌────────────┴────────────┐
                            │                         │
                            ▼                         ▼
                  ┌──────────────────┐    ┌──────────────────────┐
                  │  Provide Guidance│    │  Generate Command    │
                  │  (with examples) │    │  & Confirm with User │
                  └──────────────────┘    └──────────┬───────────┘
                                                     │
                                          User Confirms Execution
                                                     │
                                                     ▼
                                      ┌──────────────────────────┐
                                      │  Hand Back to Default    │
                                      │  CommandEngine           │
                                      └──────────────────────────┘
```

**Flow:**
1. User enters command (e.g., `az create a storage account in westus`)
2. `DefaultCommandEngine` attempts to parse with standard syntax
3. **On parse failure:** Control passes to `AgentEngine`
4. Agent interprets natural language and either:
   - Returns guidance with example commands (placeholders → not executable)
   - Generates complete command and asks user for confirmation
5. **If user confirms:** Agent sets `ExtractedCommand` property and hands control back to `DefaultCommandEngine`
6. `DefaultCommandEngine` executes the extracted command

### Deployment Options

The agent supports three AI backend implementations:

1. **Azure OpenAI** (default) - Cloud-based, production-grade responses
2. **Local Model** (Phi-3.5-mini) - Fully offline inference via LlamaSharp
3. **Azure Copilot Service** - Azure's production Copilot infrastructure

### Interface Support

The agent works with both:

1. **Azure CLI (azv2)** - Command-line interface with `az` prefix
2. **Azure PowerShell** - Cmdlet-based interface with `AzV2` prefix and natural language support via `Invoke-AzV2` (aliases: `ask`, `please`, `pls`)

See [ARCHITECTURE.md](../../src-v2/ARCHITECTURE.md) for detailed examples of both interfaces.

## Agent Capabilities

## Agent Capabilities

The agent supports three interaction patterns:

#### Type 1: "How to" Questions

Returns command syntax with placeholders (not executable).

**Example:**
```
$ az how do I create a storage account

Azure CLI:
az storage account create --name <account-name> --resource-group <rg-name> --location <location> --sku <sku>
[DONE]
```

#### Type 2: "Do it for me" Requests

Generates complete, executable commands from natural language. User confirms before execution.

**Example:**
```
$ az create a resource group called mygroup in westus

[Agent generates command]
az group create --name mygroup --location westus

Execute this command? (yes/no): yes
[Command executes via DefaultCommandEngine]
```

#### Type 3: Command Validation

Validates commands and provides corrections if needed.

**Example:**
```
$ az storage account create --name my-storage

Command is missing required parameters. Corrected version:
az storage account create --name my-storage --resource-group <rg-name> --location <location> --sku <sku>
[DONE]
```

### Response Protocol

- **`[DONE]` marker** - Indicates conversation end, command contains placeholders (not executable)
- **No `[DONE]` marker** - Command is complete and executable after user confirmation
- **Cancellation keywords** - `cancel`, `quit`, `exit`, `stop` terminate conversations early

## Running the Agent

### Production Use

The agent is automatically invoked when standard command parsing fails:

**Azure CLI (azv2):**
```bash
$ azv2 create a storage account called mystore in westus
```

**Azure PowerShell:**
```powershell
PS> Invoke-AzV2 "create a storage account called mystore in westus"
# or using aliases:
PS> ask "create a storage account called mystore in westus"
```

See [ARCHITECTURE.md](../../src-v2/ARCHITECTURE.md) for the complete flow and orchestration details.

### Testing & Evaluation (via Program.cs)

The `Program.cs` file provides a **standalone executable for testing and evaluation** independent of the Core CLI:

```bash
# Cloud mode (default)
dotnet run --project src-v2/Agent/Agent.csproj

# Local model
dotnet run --project src-v2/Agent/Agent.csproj -- --local-model

# Azure Copilot
dotnet run --project src-v2/Agent/Agent.csproj -- --use-copilot
```

This standalone mode is used for:
- Rapid prototyping and iteration
- Performance benchmarking
- User experience validation
- Model evaluation without CLI integration overhead

## Implementation Details

## Implementation Details

### Backend Options

#### Option 1: Azure OpenAI (Default)

**Technology:** Azure OpenAI Service with GPT-4o-mini

**Configuration:**
```bash
# .env file in src/Core/Core.Agent/
AZURE_OPENAI_ENDPOINT=https://<resource>.openai.azure.com
AZURE_OPENAI_API_KEY=<key>
AZURE_OPENAI_DEPLOYMENT=gpt-4o-mini
```

#### Option 2: Local Model

**Technology:** LlamaSharp with Phi-3.5-mini (~2.4GB)

**Features:**
- Automatic model download on first run
- Cached in `~/.cache/llama_models/`
- Fully offline, no external dependencies

#### Option 3: Azure Copilot Service

**Technology:** Azure Copilot infrastructure with CLI/PowerShell handlers

**Features:**
- Production Copilot infrastructure
- Built-in Azure documentation grounding
- Enterprise-grade security
- Requires Azure authentication (`az login`)

### Shared Components

All options share:
- `src-v2/Cli/AgentEngine.cs` - ICommandEngine implementation, conversation management
- `src-v2/Agent/prompt.txt` - System instructions with flavor support (azure cli/azure powershell)
- `src-v2/Agent/CloudAgent.cs` - AI integration layer
- `src-v2/Cli/OrchestratorEngine.cs` - Orchestrates fallback between DefaultCommandEngine and AgentEngine
- Streaming response handling
- `[DONE]` marker protocol

### Configuration

**Environment Variables:**
```bash
AZURE_OPENAI_ENDPOINT=https://<resource>.openai.azure.com
AZURE_OPENAI_API_KEY=<key>
AZURE_OPENAI_DEPLOYMENT=gpt-4o-mini
```

## Evaluation Criteria

## Evaluation Criteria

### Metrics

#### 1. Factual Accuracy
Correctness of generated commands against Azure CLI/PowerShell standards.

**Measurement:**
- Command syntax correctness
- Parameter accuracy (required vs. optional)
- Azure naming convention compliance

**Measurement:**
- Command syntax correctness
- Parameter accuracy (required vs. optional)
- Azure naming convention compliance

**Test Sources:**
- Azure CLI/PowerShell official documentation
- Automated validation against CLI parsers

#### 2. Latency to First Token

Time from user input to first response character.

**Targets:**
- Excellent: < 500ms
- Good: 500ms - 1500ms
- Acceptable: 1500ms - 3000ms

#### 3. Groundedness

Ability to stay focused on Azure command generation without off-topic responses.

**Measurement:**
- Task completion rate
- Proper use of three interaction patterns
- Correct `[DONE]` marker usage

## Example Interactions

### Azure CLI Examples

#### Natural Language Command Generation
```
$ az create a resource group called mygroup in westus

[Agent interprets and generates]
az group create --name mygroup --location westus

Execute this command? (yes/no): yes
[DefaultCommandEngine executes]
```

#### Command with Typo
```
$ az I would like to logni

Azure CLI:
az login
az login --service-principal --username <appId> --password <password> --tenant <tenantId>
[DONE]
```

#### "How to" Query
```
$ az how do I list all VMs

Azure CLI:
az vm list
az vm list --resource-group <rg-name>
[DONE]
```

### Azure PowerShell Examples

#### Natural Language Query
```powershell
PS> Invoke-AzV2 "show me all resource groups in westus"
# or using aliases:
PS> ask "show me all resource groups in westus"
PS> please "show me all resource groups in westus"

Azure PowerShell:
Get-AzV2ResourceGroup | Where-Object Location -eq 'westus'
[DONE]
```

#### Cmdlet Syntax Help
```powershell
PS> Invoke-AzV2 "how do I create a resource group?"

Azure PowerShell:
New-AzV2ResourceGroup -Name <name> -Location <location>
[DONE]
```

For more comprehensive scenarios including mixed CLI/PowerShell usage, see [ARCHITECTURE.md Example Scenarios](../../src-v2/ARCHITECTURE.md#example-scenarios).

## Future Considerations

## Future Considerations

- **Context Awareness**: Detect current Azure subscription/tenant, infer resource group
- **Learning & Feedback**: User feedback collection, telemetry for improvement
- **Multi-Language Support**: Localization of prompts and responses
- **Advanced Features**: Multi-step command sequences, command explanation, cost estimation

## References

- [Azure CLI Documentation](https://learn.microsoft.com/cli/azure/)
- [Microsoft.Agents.AI Framework](https://www.nuget.org/packages/Microsoft.Agents.AI)
- [LlamaSharp](https://github.com/SciSharp/LLamaSharp)
