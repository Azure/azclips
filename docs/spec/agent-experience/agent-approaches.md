# Agent Implementation Approaches

| Approach | Status | Technology |
|----------|--------|------------|
| Azure OpenAI | ![Status](https://img.shields.io/badge/status-implemented-green) | GPT-4o-mini via Azure OpenAI Service |
| Local Model | ![Status](https://img.shields.io/badge/status-experimental-yellow) | LlamaSharp with Phi-3.5-mini (dev/agent branch) |
| Azure Copilot Service | ![Status](https://img.shields.io/badge/status-experimental-yellow) | Azure production infrastructure (dev/agent branch) |

> **See Also:** For general agent concepts and platform-specific implementations, see [agent-experience.md](../agent-experience.md)

> **⚠️ Experimental Features:** Local Model and Azure Copilot Service approaches are experimental implementations under active development in the `dev/agent` branch. Only Azure OpenAI is available in the current branch.

## Overview

The Cirrus agent supports three different AI backend implementations, each with distinct characteristics for deployment scenarios, performance requirements, and operational constraints. All three approaches share the same agent architecture and command generation logic, differing only in how the language model is accessed.

This document details the technical implementation, configuration, and trade-offs for each approach.

## Shared Architecture

All three approaches share common components:

### Core Components

- **AgentEngine.cs** (`src/Core/Core.Engine/AgentEngine.cs`)
  - Implements `ICommandEngine` interface
  - Manages conversation loop
  - Handles response types (`sample_command`, `executable_command`, `question`, `confirmation`, `exit`)
  - Integrates with CLI and PowerShell entry points

- **prompt.md** (`src/Core/Core.Agent/prompt.md`)
  - System instructions for the language model
  - Defines three interaction scenarios (typo correction, how-to, natural language execution)
  - Includes flavor detection (`azure cli` vs `azure powershell`) via `AZURECLI_FLAVOR` environment variable
  - Specifies JSON response format

- **CloudAgent.cs** (`src/Core/Core.Agent/CloudAgent.cs`)
  - Azure OpenAI integration layer
  - Streaming response handling
  - Token counting and cost estimation
  - Error handling and retry logic

### Integration Points

- **CLI Integration** (`src/CLI/Program.cs`)
  - Exception-driven fallback pattern
  - Try-catch wrapper around ExtensionDispatcher
  - Falls back to AgentEngine on parsing failure

- **PowerShell Integration** (`src/PS/Resources/AgentHelper.ps1`)
  - Subprocess architecture calling `azv2`
  - `Invoke-AzAI` function with aliases (ask/please/pls)
  - `__PSEXEC__` marker detection for command execution
  - Sets `AZURECLI_FLAVOR="azure powershell"`

### Conversation Flow

All approaches follow the same conversation pattern:

1. User provides input (command or question)
2. Agent receives context (previous messages, current subscription, etc.)
3. Language model generates response in JSON format
4. Agent interprets response type and takes appropriate action:
   - `sample_command`: Display help text, exit
   - `executable_command`: Prompt for confirmation, execute if confirmed
   - `question`: Display question, await user response, continue loop
   - `confirmation`: Special case for destructive operations
   - `exit`: User cancelled, terminate

## Approach 1: Azure OpenAI (Implemented)

### Overview

The default production approach uses Azure OpenAI Service with GPT-4o-mini for natural language understanding and command generation.

### Technology Stack

- **Service**: Azure OpenAI Service
- **Model**: GPT-4o-mini (default) or GPT-4o
- **API**: Azure OpenAI REST API via .NET SDK
- **Authentication**: API key or Azure AD

### Configuration

Create a `.env` file in `src/Core/Core.Agent/`:

```bash
# Required
AZURE_OPENAI_ENDPOINT=https://<your-resource>.openai.azure.com
AZURE_OPENAI_API_KEY=<your-api-key>
AZURE_OPENAI_DEPLOYMENT=gpt-4o-mini

# Optional
AZURE_OPENAI_API_VERSION=2024-02-15-preview
AZURE_OPENAI_MAX_TOKENS=2048
AZURE_OPENAI_TEMPERATURE=0.7
```

**Note:** The `.env` file is git-ignored. See `.env.example` for a template.

### Implementation Details

#### CloudAgent Class

Located in `src/Core/Core.Agent/CloudAgent.cs`:

```csharp
public class CloudAgent
{
    private readonly OpenAIClient _client;
    private readonly string _deployment;
    
    public async Task<string> GenerateResponseAsync(
        string systemPrompt,
        List<Message> conversationHistory,
        CancellationToken cancellationToken = default)
    {
        var chatOptions = new ChatCompletionsOptions
        {
            DeploymentName = _deployment,
            Messages = {
                new ChatRequestSystemMessage(systemPrompt),
                // Add conversation history
            },
            Temperature = 0.7f,
            MaxTokens = 2048,
            // Streaming enabled
        };
        
        await foreach (var update in _client.GetChatCompletionsStreamingAsync(chatOptions))
        {
            // Stream tokens to console with progress indicator
            // Accumulate full response for processing
        }
        
        return accumulatedResponse;
    }
}
```

#### Streaming Response Handling

The agent displays a progress indicator while waiting for the first token:

```
⠋ Thinking...
```

Once streaming begins, the indicator disappears and response text appears in real-time.

### Performance Characteristics

| Metric | Value | Notes |
|--------|-------|-------|
| **Latency to First Token** | 300-800ms | Network round-trip to Azure |
| **Tokens per Second** | 50-100 | Depends on Azure region and model load |
| **Cost per Request** | ~$0.0001-0.0003 | GPT-4o-mini pricing, varies by token count |
| **Availability** | 99.9% | Azure OpenAI SLA |

### Advantages

- **High Accuracy**: GPT-4o-mini provides excellent command generation quality
- **Continuous Improvement**: Model updates from Azure OpenAI
- **Scalability**: Cloud infrastructure handles any load
- **No Local Resources**: Minimal client-side requirements

### Trade-offs

- **Network Dependency**: Requires internet connectivity
- **Latency**: Network round-trip adds 300-800ms baseline
- **Cost**: Per-token pricing (though minimal for typical usage)
- **Privacy**: Commands sent to Azure (within compliance boundaries)

### Running with Azure OpenAI

Azure OpenAI is the default backend:

```bash
# CLI - automatic fallback to agent
azv2 create a resource group called mygroup in westus

# PowerShell
ask create a resource group called mygroup in westus

# Standalone testing
dotnet run --project src/Core/Core.Agent/Core.Agent.csproj
```

---

## Approach 2: Local Model (Experimental - dev/agent branch)

### Overview

Fully offline inference using LlamaSharp with Microsoft's Phi-3.5-mini model (~2.4GB). No network connectivity required after initial model download.

### Technology Stack

- **Framework**: LlamaSharp (C# wrapper for llama.cpp)
- **Model**: Phi-3.5-mini-instruct (GGUF format, 4-bit quantization)
- **Model Size**: ~2.4GB on disk
- **Runtime**: llama.cpp native binaries (CPU or GPU acceleration)

### Setup and Configuration

#### Automatic Model Download

On first run with `--local-model`, the agent automatically:

1. Checks for model in `~/.cache/llama_models/`
2. If not found, downloads from Hugging Face
3. Verifies model integrity
4. Caches for subsequent runs

No manual setup required.

#### Manual Configuration (Optional)

Environment variables for advanced configuration:

```bash
# Model cache directory (default: ~/.cache/llama_models/)
LLAMA_MODEL_CACHE=/path/to/cache

# GPU offload layers (0 = CPU only)
LLAMA_GPU_LAYERS=0

# Context size (tokens)
LLAMA_CONTEXT_SIZE=4096

# Thread count for inference
LLAMA_THREADS=8
```

### Implementation Details

#### LocalAgent Class

Located in `src/Core/Core.Agent/LocalAgent.cs`:

```csharp
public class LocalAgent
{
    private readonly LLamaWeights _model;
    private readonly LLamaContext _context;
    
    public async Task<string> GenerateResponseAsync(
        string systemPrompt,
        List<Message> conversationHistory,
        CancellationToken cancellationToken = default)
    {
        // Build prompt with system instructions + conversation history
        var fullPrompt = BuildPrompt(systemPrompt, conversationHistory);
        
        // Run inference
        var executor = new InstructExecutor(_context);
        var parameters = new InferenceParams
        {
            Temperature = 0.7f,
            MaxTokens = 2048,
            AntiPrompts = new[] { "[DONE]", "</response>" }
        };
        
        var response = "";
        await foreach (var token in executor.InferAsync(fullPrompt, parameters, cancellationToken))
        {
            response += token;
            // Stream to console
        }
        
        return response;
    }
}
```

#### Model Caching Strategy

Models are cached in `~/.cache/llama_models/` with the following structure:

```
~/.cache/llama_models/
├── phi-3.5-mini-instruct-q4.gguf (2.4GB)
├── phi-3.5-mini-instruct-q4.gguf.sha256
└── model_info.json
```

Integrity verification on every load ensures model hasn't been corrupted.

### Performance Characteristics

| Metric | Value | Notes |
|--------|-------|-------|
| **Latency to First Token** | 50-200ms | CPU-dependent, no network |
| **Tokens per Second** | 10-30 (CPU) / 50-100 (GPU) | Hardware-dependent |
| **Model Load Time** | 2-5 seconds | One-time per session |
| **Memory Usage** | ~4GB RAM | Model + context |
| **Disk Space** | 2.4GB | Cached model file |

**Hardware Recommendations:**
- **Minimum**: 4 cores, 8GB RAM, ~5GB disk space
- **Recommended**: 8+ cores, 16GB RAM, SSD storage
- **Optimal**: GPU with 4GB+ VRAM (CUDA or Metal)

### Advantages

- **Fully Offline**: No network required after initial download
- **Privacy**: All processing local, no data leaves machine
- **Zero Cost**: No per-request pricing
- **Low Latency**: No network round-trip (after model load)
- **Predictable**: No rate limits or service outages

### Trade-offs

- **Lower Accuracy**: Phi-3.5-mini less capable than GPT-4o-mini
- **Model Load Time**: 2-5 second startup delay
- **Resource Requirements**: 4GB RAM minimum
- **No Continuous Improvement**: Model updates require manual intervention
- **Hardware Dependency**: Performance varies significantly by CPU/GPU

### Running with Local Model

Enable local model with `--local-model` flag:

```bash
# CLI with local model
azv2 --local-model create a resource group called mygroup in westus

# Standalone testing
dotnet run --project src/Core/Core.Agent/Core.Agent.csproj -- --local-model

# First run automatically downloads model
# Subsequent runs use cached model
```

**PowerShell Note:** Local model support in PowerShell requires setting an environment variable before calling `azv2`:

```powershell
$env:AZURECLI_LOCAL_MODEL = "true"
ask create a resource group called mygroup in westus
```

---

## Approach 3: Azure Copilot Service (Experimental - dev/agent branch)

### Overview

Integration with Azure's production Copilot infrastructure. Provides enterprise-grade AI assistance with built-in Azure documentation grounding.

**Current Status:** Experimental implementation exists in the `dev/agent` branch. Alternative designs are being explored due to architectural considerations. Not available in the current branch.

### Technology Stack

- **Service**: Azure Copilot Service (production infrastructure)
- **Authentication**: Azure AD (via `azv2 login`)
- **Grounding**: Built-in Azure documentation knowledge base
- **API**: Internal Azure Copilot REST API

### Architecture Considerations

The Copilot Service approach has different architectural characteristics:

#### Current Implementation

- Calls Azure Copilot Service API endpoints
- Requires Azure authentication (`azv2 login`)
- Automatically grounds responses in Azure documentation
- Provides session management and conversation history

#### Under Review

Alternative designs being considered:

1. **Hybrid Approach**: Combine Copilot Service for Azure knowledge with local/OpenAI for command generation
2. **Plugin Model**: Copilot Service acts as plugin to existing agent architecture
3. **Federated Agent**: Route specific query types to different backends

### Configuration (Current Implementation)

Authentication via Azure CLI login:

```bash
# Login to Azure (enables Copilot Service)
azv2 login

# Agent automatically uses Copilot Service if available
azv2 create a resource group called mygroup in westus
```

No additional configuration required - service endpoint discovered via Azure authentication context.

### Advantages

- **Azure Documentation Grounding**: Built-in access to official Azure docs
- **Enterprise Features**: Session management, audit logging, compliance
- **Consistent with Azure Portal**: Same Copilot experience across touchpoints
- **No Local Configuration**: Works automatically after `azv2 login`

### Trade-offs

- **Azure Dependency**: Requires Azure authentication
- **Limited Flexibility**: Cannot customize model or prompt template
- **Network Dependency**: Requires connectivity to Azure
- **Under Review**: Architecture being reconsidered

### Running with Copilot Service

Enable Copilot Service with `--use-copilot` flag:

```bash
# Login first
azv2 login

# CLI with Copilot Service
azv2 --use-copilot create a resource group called mygroup in westus

# Standalone testing
dotnet run --project src/Core/Core.Agent/Core.Agent.csproj -- --use-copilot
```

**Note:** This approach is under architectural review. Alternative designs may change this interface.

---

## Comparison Matrix

| Feature | Azure OpenAI | Local Model | Copilot Service |
|---------|--------------|-------------|-----------------|
| **Network Required** | Yes | No (after download) | Yes |
| **Setup Complexity** | Medium (.env file) | Low (automatic) | Low (az login) |
| **First Token Latency** | 300-800ms | 50-200ms (+ 2-5s load) | 300-800ms |
| **Accuracy** | Excellent | Good | Excellent |
| **Cost** | Pay per token | Free | Included with Azure |
| **Privacy** | Cloud processing | Fully local | Cloud processing |
| **Resource Usage** | Minimal | 4GB RAM, 2.4GB disk | Minimal |
| **Offline Support** | No | Yes | No |
| **Azure Documentation** | Via prompt | Via prompt | Built-in grounding |
| **Status** | ✅ Production | 🧪 Experimental (dev/agent) | 🧪 Experimental (dev/agent) |

## Choosing an Approach

### Use Azure OpenAI When:

- Production deployment with internet connectivity
- Highest accuracy required
- Cost is not a constraint (~$0.0001-0.0003 per request)
- Continuous model improvements desired
- Minimal client-side resources

### Use Local Model When:

- Offline operation required (air-gapped environments)
- Privacy/security mandates local processing
- Zero operational cost desired
- Consistent performance more important than peak accuracy
- Client hardware sufficient (8GB+ RAM recommended)

### Use Copilot Service When:

- Already using Azure Portal Copilot
- Enterprise audit/compliance features required
- Consistent experience across Azure touchpoints desired
- Note: Under architectural review, may change

## Standalone Testing Mode

All three approaches support standalone testing without full CLI integration using the standalone executable:

### Purpose

The standalone executable (`eval/Core.Agent.Standalone`) enables:

- Rapid prototyping and iteration
- Performance benchmarking across approaches
- User experience validation
- Model evaluation without CLI integration overhead
- Interactive debugging
- Foundation for building automated evaluators

### Running Standalone Mode

```bash
# Azure OpenAI (default - available now)
dotnet run --project eval/Core.Agent.Standalone

# Local model (experimental - dev/agent branch only)
dotnet run --project eval/Core.Agent.Standalone -- --local-model
# Output: "local backend is not yet implemented in this branch."

# Copilot Service (experimental - dev/agent branch only)
dotnet run --project eval/Core.Agent.Standalone -- --use-copilot
# Output: "copilot backend is not yet implemented in this branch."
```

### Example Standalone Session

```bash
$ dotnet run --project eval/Core.Agent.Standalone

Azure CLI Agent - Standalone Mode
==================================

Backend: Azure OpenAI (from .env configuration)

Enter natural language commands (or 'quit' to exit):
Examples:
  - show me all resource groups
  - how do I create a VM
  - grup list (typo correction)

> how do I create a resource group

⠋ Processing...

Exit Code: 0
Output:
{
  "type": "sample_command",
  "explanation": "To create a resource group in Azure:",
  "command": "azv2 group create --name <resource-group-name> --location <location>"
}

> quit
Goodbye!
```

## Implementation Files

### Shared Components

- `src/Core/Core.Engine/AgentEngine.cs` - Agent orchestration and conversation loop
- `src/Core/Core.Abstractions/ICommandEngine.cs` - Command engine interface
- `src/Core/Core.Agent/prompt.md` - System instructions and response format

### Azure OpenAI

- `src/Core/Core.Agent/CloudAgent.cs` - Azure OpenAI integration
- `src/Core/Core.Agent/.env` - Configuration (git-ignored)
- `src/Core/Core.Agent/.env.example` - Configuration template

### Local Model

- `src/Core/Core.Agent/LocalAgent.cs` - LlamaSharp integration
- `src/Core/Core.Agent/ModelDownloader.cs` - Automatic model download
- `~/.cache/llama_models/` - Model cache directory

### Copilot Service

- `src/Core/Core.Agent/CopilotAgent.cs` - Azure Copilot Service integration
- Note: Under architectural review

### CLI Integration

- `src/CLI/Program.cs` - Exception-driven fallback entry point
- `src/CLI/Parser/ExtensionDispatcher.cs` - Standard command parser

### PowerShell Integration

- `src/PS/Resources/AgentHelper.ps1` - `Invoke-AzAI` function and aliases
- Sets `AZURECLI_FLAVOR="azure powershell"` for subprocess

### Evaluation and Testing

- `eval/Core.Agent.Standalone/` - Standalone executable for testing and evaluation
- See [Core.Agent.Standalone README](../../../eval/Core.Agent.Standalone/README.md)

## Future Considerations

### Hybrid Approaches

Combine multiple backends for different query types:
- Use Copilot Service for Azure documentation questions
- Use Azure OpenAI for command generation
- Use Local Model as fallback when offline

### Caching and Optimization

- Cache common queries and responses
- Pre-warm local model on CLI startup
- Batch multiple queries for efficiency

### Model Updates

- Automatic update notifications for local models
- A/B testing between model versions
- Performance regression detection

### Additional Models

- Support for other local models (Llama 3, Mistral)
- Fine-tuned models for Azure CLI/PowerShell
- Specialized models for specific Azure services

## References

- [Azure OpenAI Service Documentation](https://learn.microsoft.com/azure/ai-services/openai/)
- [LlamaSharp GitHub Repository](https://github.com/SciSharp/LLamaSharp)
- [Phi-3 Model Information](https://huggingface.co/microsoft/Phi-3-mini-4k-instruct)
- [Main Agent Experience Document](../agent-experience.md)
- [Azure CLI Agent Experience](agent-experience-azcli.md)
- [Azure PowerShell Agent Experience](agent-experience-azps.md)
