# Core.Agent Test Architecture

## Design Principles

### 1. Environment-Aware Testing
The test suite intelligently adapts to the environment:
- **Local Development**: If `.env` exists with Azure credentials, all tests run
- **CI/CD**: Without `.env`, cloud tests automatically skip without failing
- **Hybrid**: Mixed environments work seamlessly

### 2. No Manual Test Selection
Tests self-determine whether to run or skip based on configuration availability:
```csharp
private readonly bool _hasAzureConfig;

public CloudAgentTests()
{
    // Load .env if it exists
    var envPath = Path.Combine(GetRepositoryRoot(), ".env");
    if (File.Exists(envPath))
    {
        Env.Load(envPath);
    }
    
    // Check if configuration is available
    _hasAzureConfig = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT"));
}

[Fact]
public async Task SomeTest()
{
    if (!_hasAzureConfig)
    {
        return; // Skip gracefully
    }
    // ... test logic
}
```

### 3. Test Organization

#### CloudAgentTests.cs
Tests for Azure OpenAI-based agent (no `--local-model` flag):
- Agent creation and configuration
- Error handling for missing credentials
- Streaming response functionality
- Program execution without local model

#### LocalAgentTests.cs
Tests for Phi-3.5-mini local agent (with `--local-model` flag):
- Local agent creation
- Model download and caching
- Model verification (ensures Phi-3.5-mini is used)
- Streaming response with local model
- Program execution with local model flag

#### ProgramIntegrationTests.cs
End-to-end integration tests:
- Executable verification
- Flag-based agent selection
- Model download verification
- Cache behavior validation

## Key Test Scenarios

### Scenario 1: Running Without --local-model
**Expected Behavior**:
- CloudAgent is instantiated
- Azure OpenAI is used for inference
- No local model download or initialization
- Output does NOT contain "Initializing local Phi-3.5-mini model"

**Tests**:
- `CloudAgentTests.ProgramExecution_ShouldRunWithoutLocalModelFlag`
- `ProgramIntegrationTests.Program_WithoutFlags_ShouldUseCloudAgent`

### Scenario 2: Running With --local-model
**Expected Behavior**:
- LocalAgent is instantiated
- Phi-3.5-mini model is downloaded (if not cached)
- Model is loaded from cache on subsequent runs
- Output contains "Initializing local Phi-3.5-mini model" or "Downloading Phi-3.5-mini model"

**Tests**:
- `LocalAgentTests.ProgramExecution_ShouldRunWithLocalModelFlag`
- `ProgramIntegrationTests.Program_WithLocalModelFlag_ShouldUseLocalAgent`

### Scenario 3: Model Download and Verification
**Expected Behavior**:
- Model is downloaded from HuggingFace (first time)
- Model is cached in `~/.cache/llama_models/`
- Model filename is `Phi-3.5-mini-instruct-Q4_K_M.gguf`
- Model size is approximately 2.4GB

**Tests**:
- `LocalAgentTests.CreateAsync_ShouldDownloadOrUseCachedModel`
- `LocalAgentTests.ModelManager_ShouldUsePhi35MiniModel`
- `LocalAgentTests.LocalAgent_ShouldDownloadModel_WhenNotCached`
- `ProgramIntegrationTests.Program_WithLocalModelFlag_ShouldDownloadPhi35Mini`

### Scenario 4: Environment Configuration
**Expected Behavior**:
- When `.env` is present with Azure credentials, cloud tests run
- When `.env` is missing, cloud tests skip gracefully
- Local tests always run (no external dependencies)

**Tests**:
- `CloudAgentTests.CreateAsync_ShouldCreateCloudAgent_WhenAzureConfigPresent`
- `CloudAgentTests.CreateAsync_ShouldThrow_WhenAzureConfigMissing`

## Test Execution Flow

```
┌─────────────────────────────────────────┐
│ Test Suite Initialization               │
├─────────────────────────────────────────┤
│ 1. Locate repository root               │
│ 2. Attempt to load .env file            │
│ 3. Check Azure configuration            │
│ 4. Set _hasAzureConfig flag              │
└─────────────────────────────────────────┘
                    │
                    ▼
┌─────────────────────────────────────────┐
│ CloudAgent Tests                        │
├─────────────────────────────────────────┤
│ if (!_hasAzureConfig) return; // Skip   │
│ else: Run cloud tests                   │
└─────────────────────────────────────────┘
                    │
                    ▼
┌─────────────────────────────────────────┐
│ LocalAgent Tests                        │
├─────────────────────────────────────────┤
│ Always run (no dependencies)            │
│ - Download model if needed              │
│ - Use cached model if available         │
└─────────────────────────────────────────┘
                    │
                    ▼
┌─────────────────────────────────────────┐
│ Integration Tests                       │
├─────────────────────────────────────────┤
│ - Executable verification               │
│ - Flag-based tests (local/cloud)        │
│ - Skip cloud tests if no .env           │
└─────────────────────────────────────────┘
```

## Running Tests in Different Environments

### Local Development (Full Coverage)
```bash
# Ensure .env exists with Azure credentials
cat > .env << EOF
AZURE_OPENAI_ENDPOINT=https://your-resource.openai.azure.com
AZURE_OPENAI_API_KEY=your-api-key
AZURE_OPENAI_DEPLOYMENT=gpt-4o-mini
EOF

# Run all tests (cloud + local)
dotnet test tests/Core/Core.Agent.Tests

# Expected: All 17 tests run and pass
```

### CI/CD (No Azure Credentials)
```bash
# No .env file or Azure environment variables
# Run all tests
dotnet test tests/Core/Core.Agent.Tests

# Expected: ~13 tests run, 4 cloud tests skip gracefully
```

### Local Only (Skip Cloud)
```bash
# Run only local agent tests
dotnet test tests/Core/Core.Agent.Tests --filter "FullyQualifiedName~LocalAgent"

# Expected: 7 local tests run
```

### Cloud Only
```bash
# Run only cloud agent tests (requires .env)
dotnet test tests/Core/Core.Agent.Tests --filter "FullyQualifiedName~CloudAgent"

# Expected: 4 cloud tests run or skip
```

## Model Caching Strategy

### First Run (No Cache)
1. Test calls `LocalAgent.CreateAsync()`
2. `LlamaModelManager.EnsureModelAsync()` checks cache
3. Model not found, downloads from HuggingFace (~2.4GB)
4. Model saved to `~/.cache/llama_models/Phi-3.5-mini-instruct-Q4_K_M.gguf`
5. Test completes (may take 2-10 minutes depending on bandwidth)

### Subsequent Runs (With Cache)
1. Test calls `LocalAgent.CreateAsync()`
2. `LlamaModelManager.EnsureModelAsync()` checks cache
3. Model found, returns path immediately
4. Test completes in seconds

## Debugging Tests

### Enable Verbose Output
```bash
dotnet test tests/Core/Core.Agent.Tests --logger "console;verbosity=detailed"
```

### Run Single Test
```bash
dotnet test tests/Core/Core.Agent.Tests \
  --filter "FullyQualifiedName~CreateAsync_ShouldThrow_WhenAzureConfigMissing"
```

### Check Test Discovery
```bash
dotnet test tests/Core/Core.Agent.Tests --list-tests
```

### View Test Coverage
```bash
dotnet test tests/Core/Core.Agent.Tests --collect:"XPlat Code Coverage"
```

## Troubleshooting

### Cloud Tests Always Skip
**Cause**: Missing `.env` file or incorrect Azure credentials
**Solution**: Create `.env` in repository root with valid credentials

### Model Download Fails
**Cause**: Network connectivity or insufficient disk space
**Solution**: 
- Check internet connection
- Ensure ~3GB free space in `~/.cache/`
- Retry: model download is atomic, partial downloads are cleaned up

### Tests Timeout
**Cause**: First-time model download takes several minutes
**Solution**: Increase test timeout or pre-download model:
```bash
# Pre-download model
dotnet run --project src/Core/Core.Agent -- --local-model
# Then run tests
dotnet test tests/Core/Core.Agent.Tests
```

### Permission Errors
**Cause**: Cannot write to cache directory
**Solution**: Check permissions on `~/.cache/llama_models/`

## Best Practices

1. **Pre-download Model**: For CI/CD, pre-download and cache the model in a setup step
2. **Separate Test Runs**: Run cloud and local tests separately in CI/CD pipelines
3. **Cache Model**: Cache `~/.cache/llama_models/` in CI/CD to speed up builds
4. **Environment Variables**: Use environment variables instead of `.env` in CI/CD
5. **Test Isolation**: Each test is independent and can run in any order
