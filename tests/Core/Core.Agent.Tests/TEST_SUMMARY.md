# Core.Agent Test Suite - Summary

## Overview
Comprehensive test suite for Core.Agent library covering both CloudAgent (Azure OpenAI) and LocalAgent (Phi-3.5-mini) implementations.

## Test Statistics
- **Total Tests**: 17
- **CloudAgent Tests**: 4
- **LocalAgent Tests**: 7
- **Integration Tests**: 6

## Test Coverage

### 1. CloudAgent Tests (Azure OpenAI Mode)
Tests that verify the agent runs **WITHOUT** `--local-model` parameter:

✅ `CreateAsync_ShouldCreateCloudAgent_WhenAzureConfigPresent`
   - Verifies CloudAgent creation with proper Azure configuration
   - **Depends on**: `.env` file with `AZURE_OPENAI_*` variables
   - **Behavior**: Skips automatically if `.env` not present (CI/CD safe)

✅ `CreateAsync_ShouldThrow_WhenAzureConfigMissing`
   - Validates error handling when Azure configuration is missing
   - **Behavior**: Always runs, tests exception handling

✅ `RunStreamingAsync_ShouldReturnResponse_WhenValidInput`
   - Tests streaming response functionality with CloudAgent
   - **Depends on**: `.env` file with Azure configuration
   - **Behavior**: Skips if configuration not available

✅ `ProgramExecution_ShouldRunWithoutLocalModelFlag`
   - End-to-end test: runs Core.Agent executable without `--local-model`
   - **Verifies**: CloudAgent is used (no local model initialization messages)
   - **Depends on**: `.env` file with Azure configuration
   - **Behavior**: Skips if configuration not available

### 2. LocalAgent Tests (Phi-3.5-mini Mode)
Tests that verify the agent runs **WITH** `--local-model` parameter:

✅ `CreateAsync_ShouldCreateLocalAgent`
   - Verifies LocalAgent creation and initialization
   - **Behavior**: Always runs

✅ `CreateAsync_ShouldDownloadOrUseCachedModel`
   - Tests model download/caching mechanism
   - **Verifies**: Model is downloaded or retrieved from cache
   - **Behavior**: Downloads model (~2.4GB) on first run, uses cache thereafter

✅ `ModelManager_ShouldUsePhi35MiniModel`
   - Verifies that Phi-3.5-mini model is used (not other models)
   - **Checks**: Model filename contains "Phi-3.5-mini-instruct-Q4_K_M.gguf"
   - **Behavior**: Downloads model if not cached

✅ `RunStreamingAsync_ShouldReturnResponse_WhenValidInput`
   - Tests streaming response functionality with LocalAgent
   - **Behavior**: Downloads model if not cached

✅ `ProgramExecution_ShouldRunWithLocalModelFlag`
   - End-to-end test: runs Core.Agent executable with `--local-model`
   - **Verifies**: LocalAgent is used (shows initialization messages)
   - **Checks**: Output contains "Initializing local Phi-3.5-mini model"
   - **Behavior**: Downloads model if not cached

✅ `ProgramExecution_WithLocalModelFlag_ShouldUsePhi35Mini`
   - Verifies Phi-3.5-mini model is present after running with `--local-model`
   - **Checks**: Model file exists and filename is correct
   - **Behavior**: Downloads model if not cached

✅ `LocalAgent_ShouldDownloadModel_WhenNotCached`
   - Tests model download with progress callback
   - **Verifies**: Download progress is tracked and model is cached
   - **Behavior**: Downloads model if not cached, tests progress reporting

### 3. Integration Tests
End-to-end program execution tests:

✅ `Program_ShouldExist_AndBeExecutable`
   - Verifies Core.Agent executable exists and is accessible
   - **Behavior**: Always runs, quick smoke test

✅ `Program_WithoutFlags_ShouldUseCloudAgent`
   - Tests program execution without any flags (CloudAgent mode)
   - **Depends on**: `.env` file with Azure configuration
   - **Behavior**: Skips if configuration not available

✅ `Program_WithLocalModelFlag_ShouldUseLocalAgent`
   - Tests program execution with `--local-model` flag
   - **Verifies**: Local model initialization messages appear
   - **Behavior**: Downloads model if not cached

✅ `Program_WithLocalModelFlag_ShouldDownloadPhi35Mini`
   - Verifies model download when using `--local-model`
   - **Checks**: Model file exists and size is reasonable (1-5GB)
   - **Behavior**: Downloads model if not cached

✅ `ModelManager_ShouldUseCorrectModelUrl`
   - Verifies correct model is downloaded (Phi-3.5-mini)
   - **Behavior**: Downloads model if not cached

✅ `ModelManager_ShouldCacheInUserProfile`
   - Verifies model is cached in `~/.cache/llama_models/`
   - **Behavior**: Checks cache directory structure

## Environment Configuration

### Local Development (with .env)
```bash
# Create .env in repository root
AZURE_OPENAI_ENDPOINT=https://your-resource.openai.azure.com
AZURE_OPENAI_API_KEY=your-api-key
AZURE_OPENAI_DEPLOYMENT=gpt-4o-mini
```

**Result**: All 17 tests will run (4 CloudAgent + 7 LocalAgent + 6 Integration)

### CI/CD or Missing .env
**Result**: 
- CloudAgent tests (4) will **skip automatically**
- LocalAgent tests (7) will **run normally**
- Integration tests (6) will **run** (cloud-specific tests skip)
- Total running: ~13 tests (4 skipped)

## Running Tests

### Run all tests
```bash
dotnet test tests/Core/Core.Agent.Tests
```

### Run only LocalAgent tests (no Azure config needed)
```bash
dotnet test tests/Core/Core.Agent.Tests --filter "FullyQualifiedName~LocalAgent"
```

### Run only CloudAgent tests
```bash
dotnet test tests/Core/Core.Agent.Tests --filter "FullyQualifiedName~CloudAgent"
```

### Run with coverage
```bash
dotnet test tests/Core/Core.Agent.Tests --collect:"XPlat Code Coverage"
```

### List all tests
```bash
dotnet test tests/Core/Core.Agent.Tests --list-tests
```

## Key Features

✅ **Environment-aware**: Automatically skips cloud tests when `.env` is missing
✅ **CI/CD Ready**: No manual test selection needed for different environments
✅ **Model Verification**: Ensures Phi-3.5-mini is used for local inference
✅ **Download Testing**: Verifies model download and caching behavior
✅ **End-to-End**: Tests actual program execution with different flags
✅ **No Hardcoded Secrets**: All Azure credentials from environment variables

## Model Caching
- **Location**: `~/.cache/llama_models/Phi-3.5-mini-instruct-Q4_K_M.gguf`
- **Size**: ~2.4GB
- **Behavior**: Downloaded once, reused for all subsequent test runs
- **First Run**: May take several minutes to download
- **Subsequent Runs**: Use cached model, very fast
