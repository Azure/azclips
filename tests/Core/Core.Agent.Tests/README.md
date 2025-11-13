# Core.Agent Tests

This test project contains comprehensive tests for the Core.Agent library, including both CloudAgent (Azure OpenAI) and LocalAgent (Phi-3.5-mini) implementations.

## Test Categories

### CloudAgentTests
Tests for the cloud-based agent using Azure OpenAI:
- Agent creation with Azure configuration
- Error handling for missing configuration
- Streaming response functionality
- Program execution without `--local-model` flag

**Note:** These tests require a `.env` file in the repository root with Azure OpenAI credentials. Tests will automatically skip if configuration is missing (e.g., in CI/CD environments).

### LocalAgentTests
Tests for the local agent using Phi-3.5-mini:
- Agent creation with local model
- Model download and caching
- Streaming response functionality
- Program execution with `--local-model` flag
- Model verification (ensures Phi-3.5-mini is used)

### ProgramIntegrationTests
End-to-end integration tests for the Core.Agent program:
- Executable verification
- Cloud vs. Local agent selection based on flags
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

## Environment Configuration

### For Cloud Tests (Local Development)
Create a `.env` file in the repository root:
```
AZURE_OPENAI_ENDPOINT=https://your-resource.openai.azure.com
AZURE_OPENAI_API_KEY=your-api-key
AZURE_OPENAI_DEPLOYMENT=gpt-4o-mini
```

### For CI/CD
Cloud tests will automatically skip when the `.env` file or required environment variables are not present. Local model tests will run normally.

## Test Behavior

- **Cloud tests** require `.env` configuration and will skip if not available
- **Local tests** will download the Phi-3.5-mini model (~2.4GB) on first run
- Model is cached in `~/.cache/llama_models/` for subsequent test runs
- Integration tests may take several minutes on first run due to model download
