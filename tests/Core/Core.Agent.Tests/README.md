# Core.Agent Tests

This test project contains comprehensive tests for the Core.Agent library, including CloudAgent (Azure OpenAI), LocalAgent (Phi-3.5-mini), and AzureCopilotAgent implementations.

## Test Categories

### CloudAgentTests
Tests for the cloud-based agent using Azure OpenAI:
- Agent creation with Azure configuration
- Error handling for missing configuration
- Streaming response functionality
- Program execution without flags (default mode)

**Note:** These tests require a `.env` file in the repository root with Azure OpenAI credentials. Tests will automatically skip if configuration is missing (e.g., in CI/CD environments).

### LocalAgentTests
Tests for the local agent using Phi-3.5-mini:
- Agent creation with local model
- Model download and caching
- Streaming response functionality
- Program execution with `--local-model` flag
- Model verification (ensures Phi-3.5-mini is used)

### AzureCopilotAgentTests
Tests for the Azure Copilot service integration:
- Agent creation with Azure authentication
- Connection to Copilot service
- Streaming response functionality
- Program execution with `--use-copilot` flag

**Note:** These tests require Azure authentication (e.g., `az login`) and appropriate permissions to access the Copilot service.

### ProgramIntegrationTests
End-to-end integration tests for the Core.Agent program:
- Executable verification
- Cloud (default), Local, and Copilot agent selection based on flags
- Command-line argument parsing (`--local-model`, `--use-copilot`)
- Phi-3.5-mini model download verification
- Model caching behavior

## Running Tests

### Run all tests:
```bash
dotnet test tests/Core/Core.Agent.Tests
```

### Run with coverage:
```bash
dotnet test tests/Core/Core.Agent.Tests --collect:"XPlat Code Coverage"
```

### Run only local tests (no Azure config needed):
```bash
dotnet test tests/Core/Core.Agent.Tests --filter "FullyQualifiedName~LocalAgent"
```

### Run only cloud tests:
```bash
dotnet test tests/Core/Core.Agent.Tests --filter "FullyQualifiedName~CloudAgent"
```

### Run only Copilot tests:
```bash
dotnet test tests/Core/Core.Agent.Tests --filter "FullyQualifiedName~CopilotAgent"
```

## Environment Configuration

### For Cloud Tests (Local Development)
Create a `.env` file in the repository root:
```
AZURE_OPENAI_ENDPOINT=https://your-resource.openai.azure.com
AZURE_OPENAI_API_KEY=your-api-key
AZURE_OPENAI_DEPLOYMENT=gpt-4o-mini
```

### For Copilot Tests (Local Development)
Ensure you're authenticated with Azure:
```bash
az login
```

### For CI/CD
Cloud and Copilot tests will automatically skip when the required configuration or authentication is not available. Local model tests will run normally.

## Test Behavior

- **Cloud tests** require `.env` configuration and will skip if not available
- **Copilot tests** require Azure authentication and will skip if not authenticated
- **Local tests** will download the Phi-3.5-mini model (~2.4GB) on first run
- Model is cached in `~/.cache/llama_models/` for subsequent test runs
- Integration tests may take several minutes on first run due to model download
