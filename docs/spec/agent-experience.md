# Agent Experience Design Specification

## Overview

This document describes the design and requirements for the Core.Agent, an interactive Azure CLI and PowerShell command assistant that helps users construct, validate, and execute Azure commands through conversational interactions.

## Requirements

### Functional Requirements

The agent must support three distinct interaction patterns:

#### Type 1: "How to" Questions

Users ask conceptual questions about Azure operations and receive command syntax with placeholders.

**Characteristics:**
- Single-turn interaction
- Returns both Azure CLI and Azure PowerShell syntax
- Uses placeholders for parameters (e.g., `<account-name>`, `<rg-name>`)
- Terminates with `[DONE]` marker

**Example:**
```
User: How do I create a storage account?

Agent:
Azure CLI:
az storage account create --name <account-name> --resource-group <rg-name> --location <location> --sku <sku>

Azure PowerShell:
New-AzStorageAccount -Name <account-name> -ResourceGroupName <rg-name> -Location <location> -SkuName <sku>
[DONE]
```

#### Type 2: "Do it for me" Requests

Users request specific actions and the agent interactively gathers all required parameters before providing complete, executable commands.

**Characteristics:**
- Multi-turn conversation
- Agent asks ONE question at a time
- No `[DONE]` marker during questioning phase
- Validates required parameters are collected
- Provides complete commands with actual values
- Terminates with `[DONE]` marker after final response
- Supports cancellation keywords: `cancel`, `quit`, `exit`, `stop`, `nevermind`

**Example:**
```
User: Create a storage account for me

Agent: What is the name of your storage account?

User: mystorageacct2024

Agent: What resource group should I use?

User: my-prod-rg

Agent: Which region/location?

User: eastus

Agent: What SKU tier do you need? (Standard_LRS, Standard_GRS, etc.)

User: Standard_LRS

Agent:
Azure CLI:
az storage account create --name mystorageacct2024 --resource-group my-prod-rg --location eastus --sku Standard_LRS

Azure PowerShell:
New-AzStorageAccount -Name mystorageacct2024 -ResourceGroupName my-prod-rg -Location eastus -SkuName Standard_LRS
[DONE]
```

**Cancellation Example:**
```
User: Create a VM for me

Agent: What is the name of your VM?

User: cancel

Agent: [DONE]
```

#### Type 3: Command Validation

Users submit Azure commands for validation and receive confirmation or corrections.

**Characteristics:**
- Single-turn interaction
- Validates syntax and parameter usage
- Returns concise feedback
- Provides corrected version if incorrect
- Terminates with `[DONE]` marker

**Example (Correct):**
```
User: az vm create --name my-vm --resource-group my-rg --image UbuntuLTS

Agent: ✓ Command is correct [DONE]
```

**Example (Incorrect):**
```
User: az storage account create --name my-storage

Agent: Command is missing required parameters. Here's the corrected version:

Azure CLI:
az storage account create --name my-storage --resource-group <rg-name> --location <location> --sku <sku>

Azure PowerShell:
New-AzStorageAccount -Name my-storage -ResourceGroupName <rg-name> -Location <location> -SkuName <sku>
[DONE]
```

### Non-Functional Requirements

1. **Conversation Management**
   - Maintain conversation history across multiple turns
   - Support streaming responses for real-time feedback
   - Clear session termination via `[DONE]` marker

2. **User Experience**
   - Display visual feedback (spinner) while processing
   - Stream responses incrementally as they arrive
   - Use color coding for different message types
   - Provide clear prompts for user input

3. **Accuracy**
   - Use current Azure CLI and PowerShell syntax (2025 standards)
   - Follow Azure naming conventions
   - Never guess parameter values—always ask
   - Provide production-ready commands

4. **Cross-Platform Support**
   - Support Windows, Linux, and macOS (including Apple Silicon)
   - Handle platform-specific considerations

## Solution Design

### Architecture Overview

The Core.Agent solution is designed as an experimental platform to evaluate three distinct implementation approaches. Each approach will be built and evaluated to determine the optimal solution for production use.

### Implementation Options

#### Option 1: Cloud-Based Model (Current Implementation)

**Description:** Local agent client that connects to Azure OpenAI for inference.

**Components:**
- `CloudAgent.cs`: Manages Azure OpenAI connection and conversation state
- `Program.cs`: CLI host with streaming and conversation loop
- `prompt.txt`: System instructions loaded at runtime
- **Model:** GPT-4o-mini (deployed to Azure OpenAI)

**Technology Stack:**
- `Microsoft.Agents.AI` - Agent framework
- `Azure.AI.OpenAI` - Azure OpenAI client SDK
- `DotNetEnv` - Configuration management

**Authentication:**
- API key-based authentication via environment variables
- Configuration loaded from `.env` file or environment

**Advantages:**
- Production-grade model quality
- No local resource requirements
- Scalable to multiple users
- Easy model updates

**Considerations:**
- Requires internet connectivity
- Depends on external service availability
- API costs per request
- Latency to first token depends on network and service

#### Option 2: Local Model (Current Implementation)

**Description:** Fully local agent using LlamaSharp with Phi-3.5-mini model.

**Components:**
- `LocalAgent.cs`: Manages local model lifecycle and inference
- `LlamaModelManager.cs`: Handles model download and caching
- `Program.cs`: CLI host with streaming and conversation loop
- `prompt.txt`: System instructions loaded at runtime
- **Model:** Phi-3.5-mini-instruct (~2.4GB)

**Technology Stack:**
- `Microsoft.Agents.AI` - Agent framework
- `LLamaSharp` - .NET bindings for llama.cpp
- `LLamaSharp.Backend.Cpu` - CPU inference backend

**Model Management:**
- Automatic download on first run
- Cached in `~/.cache/llama_models/`
- Progress indication during download
- Model validation and retry logic

**Advantages:**
- No external dependencies or API costs
- Full privacy—data never leaves the machine
- Bundleable as standalone executable
- Works offline
- Consistent, predictable performance

**Considerations:**
- Model download required (~2.4GB)
- Higher local resource requirements (RAM, CPU)
- Model quality may be lower than cloud models
- Limited to model capabilities at build time

#### Option 3: Copilot Service (Planned)

**Description:** Cloud-deployed REST service using Azure's Copilot handlers for CLI/PowerShell scenarios.

**Components:**
- Local thin client with REST API integration
- Cloud-hosted service using:
  - [Copilot CLI Handler](https://eng.ms/docs/cloud-ai-platform/azure-core/azure-experiences-and-ecosystems/azure-portal-and-client-tools-ruhim/azure-cli-tools-azure-cli-powershell-and-terraform/azure-client-intelligent-experiences/copilot/copilot_cli_handler)
  - [Copilot PowerShell Handler](https://eng.ms/docs/cloud-ai-platform/azure-core/azure-experiences-and-ecosystems/azure-portal-and-client-tools-ruhim/azure-cli-tools-azure-cli-powershell-and-terraform/azure-client-intelligent-experiences/copilot/copilot_ps_handler)

**Architecture:**
```
┌──────────────┐         ┌────────────────────┐         ┌──────────────────┐
│ Local Client │ ─REST─> │ Copilot Service    │ ──────> │ Azure OpenAI     │
│ (Core.Agent) │ <─────> │ (CLI/PS Handlers)  │ <────── │                  │
└──────────────┘         └────────────────────┘         └──────────────────┘
                                    │
                                    ├──> Telemetry
                                    ├──> Grounding Data
                                    └──> Context Management
```

**Advantages:**
- Leverages Azure's production Copilot infrastructure
- Built-in grounding to Azure documentation and schemas
- Telemetry and monitoring included
- Consistent experience with Azure Portal Copilot
- Service-side updates without client changes
- Enterprise-grade security and compliance

**Considerations:**
- Requires network connectivity
- Depends on Copilot service availability
- Authentication and authorization requirements
- Client must handle REST communication
- May have different latency characteristics

### Evaluation Criteria

All three implementation options will be evaluated during the MVP phase using the following metrics:

#### 1. Groundedness to Task

**Definition:** The ability to stay focused on Azure CLI/PowerShell command generation without deviating into unrelated topics.

**Measurement:**
- Task completion rate (successful command generation)
- Rate of off-topic responses
- Adherence to the three interaction patterns
- Proper use of `[DONE]` marker

**Test Scenarios:**
- Clear Azure commands (expected: high success)
- Ambiguous requests (expected: clarifying questions)
- Off-topic questions (expected: polite refusal, redirect to Azure)
- Edge cases (unusual parameters, deprecated commands)

#### 2. Factual Accuracy

**Definition:** Correctness of generated commands against ground truth test cases.

**Measurement:**
- Command syntax correctness (parseable by Azure CLI/PowerShell)
- Parameter correctness (required vs. optional)
- Flag/option accuracy (correct names and values)
- Azure naming convention compliance

**Test Approach:**
- Curated test suite of common Azure operations
- Automated validation against Azure CLI/PowerShell parsers
- Manual review by Azure CLI/PowerShell experts
- Comparison against official documentation

**Ground Truth Sources:**
- Azure CLI reference documentation
- Azure PowerShell module documentation
- Azure CLI/PowerShell command help output
- Official Azure samples and quickstarts

#### 3. Latency to First Token

**Definition:** Time from user input submission until the first response character appears.

**Measurement:**
- Mean, median, p95, p99 latency
- Measured separately for each option
- Network latency isolated where applicable

**Target Thresholds:**
- Excellent: < 500ms
- Good: 500ms - 1500ms
- Acceptable: 1500ms - 3000ms
- Poor: > 3000ms

#### 4. Streaming Capability

**Definition:** Ability to provide incremental responses as they are generated.

**Measurement:**
- Binary: supports streaming or doesn't
- Inter-token latency (time between tokens)
- Perceived responsiveness

**Requirements:**
- Must support streaming for Types 1 and 2
- Type 3 (validation) may be single-shot
- Visual feedback (spinner) until first token

### Decision Framework

At the end of the MVP evaluation period, one implementation will be selected for production based on:

**Weighted Scoring:**
- Factual Accuracy: 40%
- Groundedness: 30%
- Latency to First Token: 20%
- Streaming Capability: 10%

**Mandatory Requirements:**
- Factual accuracy must be > 90%
- All three interaction patterns must work correctly
- Cross-platform support required

**Strategic Considerations:**
- Long-term maintenance cost
- Dependency on external services
- Offline usage scenarios
- Enterprise customer requirements
- Consistency with broader Azure tooling strategy

## Implementation Strategy

### Current vs. Planned Architecture

#### Current: Stand-Alone Evaluation Implementation

The existing `Core.Agent` implementation is a **stand-alone CLI application** designed for evaluation and experimentation purposes. It provides a direct interface for users to interact with the agent through a terminal session.

**Characteristics:**
- Self-contained executable (`dotnet run`)
- Direct user interaction via console I/O
- Independent testing and benchmarking
- Isolated evaluation of each implementation option

**Purpose:**
- Rapid prototyping and iteration
- Performance benchmarking without integration overhead
- Side-by-side comparison of Options 1, 2, and 3
- User experience validation

#### Planned: Integrated Agent Service

The production implementation will be architected as a **shared agent service** that can be invoked by both Azure CLI and Azure PowerShell wrappers.

**Architecture:**

```
┌─────────────────────────────────────────────────────────────┐
│                     User Environment                        │
├─────────────────────────────────────────────────────────────┤
│                                                             │
│  ┌─────────────────┐              ┌────────────────────┐    │
│  │  Azure CLI      │              │ Azure PowerShell   │    │
│  │  (az copilot)   │              │ (Get-AzCopilot)    │    │
│  └────────┬────────┘              └─────────┬──────────┘    │
│           │                                 │               │
│           │                                 │               │
│           └──────────────┬──────────────────┘               │
│                          │                                  │
│                          ▼                                  │
│           ┌──────────────────────────────┐                  │
│           │   Core.Agent Service         │                  │
│           │   (Shared Agent Runtime)     │                  │
│           └──────────────┬───────────────┘                  │
│                          │                                  │
│           ┌──────────────┴───────────────┐                  │
│           │                              │                  │
│           ▼                              ▼                  │
│  ┌─────────────────┐          ┌──────────────────┐          │
│  │ CloudAgent      │          │ LocalAgent       │          │
│  │ (Azure OpenAI)  │          │ (LlamaSharp)     │          │
│  └─────────────────┘          └──────────────────┘          │
│           │                              │                  │
│           ▼                              ▼                  │
│  ┌─────────────────┐          ┌──────────────────┐          │
│  │ Option 3:       │          │ Phi-3.5 Model    │          │
│  │ Copilot Service │          │ (Local Inference)│          │
│  └─────────────────┘          └──────────────────┘          │
│                                                             │
└─────────────────────────────────────────────────────────────┘
```

**Integration Points:**

1. **Azure CLI Integration** (`az copilot`)
   - New CLI command group for agent interactions
   - Invokes Core.Agent service with context (subscription, resource group, etc.)
   - Returns structured responses for CLI formatting
   - Example: `az copilot ask "How do I create a storage account?"`
   - Example: `az copilot do "Create a VM for me"`

2. **Azure PowerShell Integration** (`Get-AzCopilot`, `Invoke-AzCopilot`)
   - New cmdlets for agent interactions
   - Integrates with PowerShell pipeline and object model
   - Returns PowerShell-friendly objects
   - Example: `Get-AzCopilot -Query "How do I create a storage account?"`
   - Example: `Invoke-AzCopilot -Task "Create a VM for me"`

**Service Interface:**

The Core.Agent service will expose a programmatic API that both CLI wrappers can consume:

```csharp
public interface IAgentService
{
    // Streaming interaction for conversational mode
    IAsyncEnumerable<AgentResponse> StreamAsync(
        AgentRequest request, 
        CancellationToken cancellationToken = default);
    
    // Single-shot for simple queries
    Task<AgentResponse> QueryAsync(
        AgentRequest request, 
        CancellationToken cancellationToken = default);
    
    // Session management for multi-turn conversations
    Task<string> CreateSessionAsync();
    Task<bool> EndSessionAsync(string sessionId);
}

public class AgentRequest
{
    public string SessionId { get; set; }
    public string UserInput { get; set; }
    public AgentContext Context { get; set; }
    public AgentOptions Options { get; set; }
}

public class AgentContext
{
    public string? SubscriptionId { get; set; }
    public string? ResourceGroup { get; set; }
    public string? Location { get; set; }
    public string? TenantId { get; set; }
    public Dictionary<string, string>? AdditionalContext { get; set; }
}

public class AgentResponse
{
    public string Content { get; set; }
    public bool IsComplete { get; set; }
    public ResponseType Type { get; set; }
    public Dictionary<string, object>? Metadata { get; set; }
}

public enum ResponseType
{
    Command,           // Contains Azure CLI/PowerShell commands
    Question,          // Agent asking for more information
    Validation,        // Command validation result
    Done               // Conversation complete
}
```

**Benefits of Integrated Approach:**

1. **Unified Experience**
   - Consistent agent behavior across CLI and PowerShell
   - Single codebase to maintain and improve
   - Shared prompt engineering and model fine-tuning

2. **Context Awareness**
   - Agent can leverage current Azure CLI/PowerShell context
   - Automatic injection of subscription, resource group, location
   - Reduced questions needed from users

3. **Native Tool Integration**
   - Results formatted appropriately for each tool
   - CLI: Text output with proper formatting
   - PowerShell: Structured objects for pipeline use

4. **Flexible Deployment**
   - Service can run locally (embedded in CLI/PowerShell process)
   - Service can run as shared daemon (multiple sessions)
   - Service can be remote (Option 3: Copilot Service)

**Migration Path:**

1. **Phase 1 (Current):** Stand-alone evaluation
   - Independent Core.Agent CLI application
   - Benchmark all three options
   - Finalize prompt engineering and UX patterns

2. **Phase 2:** Service extraction
   - Refactor core logic into `Core.Agent.Service` library
   - Define `IAgentService` interface
   - Maintain stand-alone CLI for testing

3. **Phase 3:** CLI/PowerShell integration
   - Implement `az copilot` command group
   - Implement PowerShell cmdlets
   - Deploy selected agent option (based on evaluation)

4. **Phase 4:** Production deployment
   - Public preview with selected implementation
   - Telemetry and feedback collection
   - Iterative improvements based on usage data

## Implementation Notes

### Current State

As of this specification:
- ✅ Option 1 (Cloud-Based Model) is implemented as **stand-alone evaluation CLI**
- ✅ Option 2 (Local Model) is implemented as **stand-alone evaluation CLI**
- ⏳ Option 3 (Copilot Service) is planned
- ⏳ Azure CLI integration is planned (Phase 3)
- ⏳ Azure PowerShell integration is planned (Phase 3)

### Shared Components

All three options share:
- `Program.cs`: CLI host, conversation loop, streaming UI (evaluation only)
- `prompt.txt`: System instructions (may vary slightly per option)
- Conversation history management
- `[DONE]` marker protocol
- Streaming response handling

### Configuration

**Option 1 (Cloud) - Environment Variables:**
```bash
AZURE_OPENAI_ENDPOINT=https://<resource>.openai.azure.com
AZURE_OPENAI_API_KEY=<key>
AZURE_OPENAI_DEPLOYMENT=gpt-4o-mini
```

**Option 2 (Local) - Command Line:**
```bash
dotnet run -- --local-model
```

**Option 3 (Copilot Service) - Planned:**
```bash
COPILOT_SERVICE_ENDPOINT=https://<service>.azure.com
# Authentication via Azure SDK DefaultAzureCredential
```

## Future Enhancements

### Post-MVP Considerations

1. **Command Execution** (if selected option supports it)
   - Optional auto-execution of generated commands
   - Confirmation prompts for destructive operations
   - Output capture and display

2. **Context Awareness**
   - Detection of current Azure subscription/tenant
   - Inference of resource group from current directory
   - Integration with `az account` context

3. **Learning and Feedback**
   - User feedback collection (thumbs up/down)
   - Telemetry for continuous improvement
   - A/B testing of prompt variations

4. **Multi-Language Support**
   - Localization of questions and responses
   - Support for Azure CLI in different languages
   - Region-specific examples

5. **Advanced Features**
   - Multi-step command sequences (pipelines)
   - Command explanation (inverse: command → explanation)
   - Best practices suggestions
   - Cost estimation for commands

## Appendix

### Example Conversation Flow

**Multi-Turn "Do it for me" with Streaming:**

```
$ dotnet run

Ask me anything: Create a web app for me

⠋ Thinking...

What is the name of your web app?

> my-webapp-2024

⠙ Thinking...

What resource group should I use?

> my-webapps-rg

⠹ Thinking...

Which region/location?

> westus2

⠸ Thinking...

What runtime stack? (e.g., "NODE:18-lts", "DOTNET:8.0", "PYTHON:3.11")

> DOTNET:8.0

⠼ Thinking...

Azure CLI:
az webapp create --name my-webapp-2024 --resource-group my-webapps-rg --plan <app-service-plan> --runtime "DOTNET:8.0"

Note: This command requires an App Service Plan. Would you like me to help you create one first?

> yes

⠴ Thinking...

What should we name the App Service Plan?

> my-webapp-plan

⠦ Thinking...

What SKU/pricing tier? (e.g., F1, B1, S1, P1V2)

> B1

⠧ Thinking...

Azure CLI:
# Create App Service Plan
az appservice plan create --name my-webapp-plan --resource-group my-webapps-rg --location westus2 --sku B1

# Create Web App
az webapp create --name my-webapp-2024 --resource-group my-webapps-rg --plan my-webapp-plan --runtime "DOTNET:8.0"

Azure PowerShell:
# Create App Service Plan
New-AzAppServicePlan -Name my-webapp-plan -ResourceGroupName my-webapps-rg -Location westus2 -Tier Basic -NumberofWorkers 1 -WorkerSize Small

# Create Web App
New-AzWebApp -Name my-webapp-2024 -ResourceGroupName my-webapps-rg -AppServicePlan my-webapp-plan -Runtime "DOTNET:8.0"
[DONE]
```

### References

- [Azure CLI Documentation](https://learn.microsoft.com/cli/azure/)
- [Azure PowerShell Documentation](https://learn.microsoft.com/powershell/azure/)
- [Microsoft.Agents.AI Framework](https://www.nuget.org/packages/Microsoft.Agents.AI)
- [LlamaSharp](https://github.com/SciSharp/LLamaSharp)
- [Copilot CLI Handler](https://eng.ms/docs/cloud-ai-platform/azure-core/azure-experiences-and-ecosystems/azure-portal-and-client-tools-ruhim/azure-cli-tools-azure-cli-powershell-and-terraform/azure-client-intelligent-experiences/copilot/copilot_cli_handler)
- [Copilot PowerShell Handler](https://eng.ms/docs/cloud-ai-platform/azure-core/azure-experiences-and-ecosystems/azure-portal-and-client-tools-ruhim/azure-cli-tools-azure-cli-powershell-and-terraform/azure-client-intelligent-experiences/copilot/copilot_ps_handler)
