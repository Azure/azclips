# Core.Agent.Standalone

A standalone console application for agent testing, debugging, and evaluation.

## Purpose

This standalone executable provides:
- **Interactive testing** of agent responses to natural language commands
- **Backend switching** between Azure OpenAI, Local Model, and Copilot Service
- **Direct AgentEngine access** for automated evaluation and benchmarking
- **Foundation for evaluators** - build automated test suites on top of this

## Usage

### Interactive Mode

Run with default backend (Azure OpenAI):
```bash
dotnet run --project eval/Core.Agent.Standalone
```

**Note:** Local model and Copilot Service backends are experimental features under development in the `dev/agent` branch. They are not available in the current branch.

```bash
# Not yet implemented - will show "not implemented" message
dotnet run --project eval/Core.Agent.Standalone -- --local-model
dotnet run --project eval/Core.Agent.Standalone -- --use-copilot
```

### Example Session

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

> show me all resource groups

⠋ Processing...

Exit Code: 0
Output:
{
  "type": "command",
  "command": "az group list --output table",
  "confidence": 0.95
}

> how do I create a VM

⠋ Processing...

Exit Code: 0
Output:
{
  "type": "howto",
  "content": "To create a virtual machine in Azure...",
  "examples": [...]
}

> quit
Goodbye!
```

## Configuration

The standalone executable reads configuration from `src/Core/Core.Agent/.env`:

```bash
AZURE_OPENAI_ENDPOINT=https://your-endpoint.openai.azure.com/
AZURE_OPENAI_API_KEY=your-api-key
AZURE_OPENAI_DEPLOYMENT_NAME=gpt-4
```

See `src/Core/Core.Agent/.env.example` for a template.

## Architecture

The standalone executable:
1. Uses **Bootstrapper pattern** to configure DI container (same as production CLI)
2. Resolves `ICommandEngine` and casts to `AgentEngine`
3. Calls `ExecuteAsync()` directly with user input
4. Returns raw agent responses for inspection/evaluation

This ensures the standalone executable uses the **exact same agent logic** as production.

## Differences from Production CLI

| Aspect | Standalone | Production CLI |
|--------|-----------|----------------|
| **Entry Point** | Direct `AgentEngine.ExecuteAsync()` | `OrchestratorEngine` → fallback to `AgentEngine` |
| **Parsing** | No parsing, raw input to agent | `ExtensionDispatcher` tries first, then agent |
| **Confirmation** | No interactive confirmation | Interactive `yes/no` prompts |
| **Execution** | Shows JSON response only | Actually executes commands |

## Building an Evaluator

You can build automated evaluators on top of this standalone executable:

### Option 1: Process Spawning
```bash
# Spawn process, feed inputs, capture outputs
echo "show me all resource groups" | dotnet run --project eval/Core.Agent.Standalone
```

### Option 2: Reference as Library
```csharp
// In your evaluator project
var services = new ServiceCollection();
Bootstrapper.ConfigureServices(services, config);
var provider = services.BuildServiceProvider();
var engine = provider.GetRequiredService<ICommandEngine>() as AgentEngine;

// Run evaluation suite
var results = await RunEvaluationSuite(engine, testCases);
```

### Example Evaluator Structure
```
eval/
├── Core.Agent.Standalone/          # This project (baseline executable)
└── Core.Agent.Evaluator/           # Your evaluator (references Standalone or Core.Agent)
    ├── TestCases/
    │   ├── typo-correction.json
    │   ├── howto-questions.json
    │   └── natural-language.json
    ├── Evaluator.cs                # Orchestrates test runs
    └── Reporter.cs                 # Generates evaluation reports
```

## Testing Different Backends

### Azure OpenAI (Current)
```bash
# Configure in .env
dotnet run --project eval/Core.Agent.Standalone
```

### Local Model (Experimental - dev/agent branch)
```bash
# Not yet available in current branch
# Implementation exists in dev/agent branch
dotnet run --project eval/Core.Agent.Standalone -- --local-model
```

### Copilot Service (Experimental - dev/agent branch)
```bash
# Not yet available in current branch
# Implementation exists in dev/agent branch
dotnet run --project eval/Core.Agent.Standalone -- --use-copilot
```

## See Also

- [Agent Experience Documentation](../../docs/spec/agent-experience.md)
- [Agent Approaches](../../docs/spec/agent-experience/agent-approaches.md)
- [Core.Agent Library](../../src/Core/Core.Agent/README.md)
