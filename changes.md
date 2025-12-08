# Changes in resilv/agent-v2 Branch (Merged with main)

## 🚀 What is azv2?

`azv2` is an intelligent Azure CLI that combines **traditional command execution** with **AI-powered natural language understanding**. It accepts both structured commands and conversational queries, automatically falling back to AI interpretation when needed.

### Example Usage
```bash
# Structured command (fast path)
azv2 group list
azv2 group create --name myapp --location eastus

# Natural language (AI fallback)
azv2 "create a resource group called myapp in east us"
azv2 "how do I list my resource groups?"
azv2 "grup list"  # Typo correction via AI
```

### How It Works: The Orchestrator Pattern

When you run a command, `azv2` follows this execution pattern:

1. **Parse Attempt**: `DefaultCommandEngine` tries to parse as structured command
   - ✅ Success → Execute immediately (fast path)
   - ❌ Failure → Throw `NotImplementedException`

2. **AI Fallback**: `OrchestratorEngine` catches exception, invokes `AgentEngine`
   - Sends query to Azure OpenAI
   - AI interprets intent, generates proper command
   - Displays generated command for user confirmation
   - Executes upon approval

3. **Retry Loop**: Up to 5 attempts with context preservation
   - Handles ambiguity and clarification requests
   - Detects PowerShell vs CLI context via `AZURECLI_FLAVOR` environment variable

### PowerShell Integration
```powershell
# Same pattern works in PowerShell
Import-Module ./bin/Cirrus.psd1

# Structured cmdlets (fast path)
Get-AzV2ResourceGroup
New-AzV2ResourceGroup -Name myapp -Location eastus

# Natural language (AI fallback) via aliases
ask create a resource group
please show me all my groups
pls help with resource groups
```

The `Invoke-AzV2` function sets `AZURECLI_FLAVOR=azure powershell`, ensuring the AI generates PowerShell cmdlets instead of CLI commands.

## 📊 Current Status
- **Branch**: `resilv/agent-v2` (also pushed as `resilv/agent-handoff`)
- **Last Merge**: main branch merged on Dec 8, 2025
- **Framework**: .NET 9.0 (uses .NET 10.0.100 SDK with net9 target framework)
- **Tests**: ✅ **29 tests passing** (12 CLI + 17 PowerShell)

## 🏗️ Architecture

### Bootstrapper Pattern (Restored from main)
The original Bootstrapper pattern from main was restored and extended:
- **Original** (main branch): Simple dependency injection with `ITelemetryService` and `ICommandEngine`
- **Current** (agent-v2): Extended to support `OrchestratorEngine`, `DefaultCommandEngine`, `AgentEngine`, `CommandParser`, and `CommandBinder`
- Located in: `src/Core/Core.Engine/Bootstrapper.cs`
- All services registered as **Singletons** (appropriate for short-lived CLI process)

### CLI Entry Point
**`src/Core/Core.Cli/Program.cs`** (3 lines - matches original design):
```csharp
var commandEngine = await Bootstrapper.StartAsync();
await commandEngine.ExecuteCommandAsync(args, default);
return 0;
```

### Orchestrator Pattern
**`OrchestratorEngine`** coordinates command execution:
1. **Fast Path**: Try `DefaultCommandEngine` (structured commands: `group create --name test`)
2. **Fallback**: On `NotImplementedException`, invoke `AgentEngine` (AI natural language)
3. **Retry Loop**: Max 5 attempts with flavor detection for PowerShell vs CLI context

### Module System
Service modules follow Azure PowerShell pattern:
- **Business Layer**: Shared logic (`Group/Business/`, `Login/Business/`)
- **CLI Commands**: `ICommand` implementations (`Group/Commands/`, `Login/Commands/`)
- **PowerShell Cmdlets**: PSCmdlet classes (`Group/Cmdlets/`, `Login/Cmdlets/`)
- **Module Manifests**: `.psd1` files for PowerShell discovery

### PowerShell Module Structure
```
bin/
├── azv2                      # CLI executable
├── Cirrus.psd1               # Meta-module (imports all sub-modules)
├── Agent/
│   ├── Cirrus.Agent.psd1     # AI agent module
│   └── AgentHelper.ps1       # Invoke-AzV2, ask, please, pls
├── Group/
│   ├── Cirrus.Group.psd1     # Resource group module
│   └── Group.dll             # Cmdlets: New/Get/Set/Remove-AzV2ResourceGroup
└── Login/
    ├── Cirrus.Login.psd1     # Authentication module
    └── Login.dll             # Cmdlets: Connect/Disconnect-AzV2Account, Get-AzV2Context
```

## 🔧 Build & Deploy System

### Build Configuration
- **Target Framework**: `net9` (from main branch's `src/Directory.Build.props`)
- **SDK Version**: 10.0.100 (from `global.json`)
- **Solution**: `cirrus.slnx` includes 12 projects:
  - `src/CLI/Core.Cli.csproj` (old, from main - not used)
  - `src/Core/Core.Cli/Core.Cli.csproj` (current - produces `azv2`)
  - `src/Core/Core.Agent/Core.Agent.csproj` (AI integration)
  - `src/Core/Core.Engine/Core.Engine.csproj` (Bootstrapper, Orchestrator)
  - Other core and module projects

### Deployment Script: `scripts/deploy.sh`
```bash
# 1. Verify .NET SDK
# 2. Build solution in Release mode
dotnet build cirrus.slnx -c Release

# 3. Publish azv2 CLI to bin/
dotnet publish src/Core/Core.Cli/Core.Cli.csproj -c Release -o bin/

# 4. Copy PowerShell modules to bin/
# - Agent/, Group/, Login/ modules
# - Cirrus.psd1 meta-module
```

**Output Location**: `bin/` (cleaned before each deploy)
**Binary Name**: `azv2` (set via `<AssemblyName>azv2</AssemblyName>`)

### Testing Script: `scripts/test.sh`
```bash
# 1. Run deployment (ensures binaries are up-to-date)
./scripts/deploy.sh > /dev/null 2>&1

# 2. Run all tests
dotnet test  # Builds Debug mode tests, uses Release binaries in bin/
```

**Why this approach?**
- Tests run against **deployed binaries** in `bin/`, not build artifacts in `artifacts/`
- Validates the actual runtime configuration users will experience
- CLI tests spawn `bin/azv2` as subprocess
- PowerShell tests import modules from `bin/`

## ✅ Test Coverage (29 tests)

### CLI Integration Tests (`tests/Cli.Tests/`) - 12 tests
**Tests validate**: Structured commands, natural language, agent fallback
- ✅ `AzV2_ShouldExist` - Binary exists at `bin/azv2`
- ✅ `GroupList_ShouldSucceed` - `azv2 group list`
- ✅ `GroupShow_ShouldSucceed` - `azv2 group show --name X`
- ✅ `GroupCreate_ShouldSucceed` - `azv2 group create --name X --location Y`
- ✅ `GroupDelete_ShouldSucceed` - `azv2 group delete --name X`
- ✅ `Agent_NaturalLanguage_ListGroups_ShouldInvokeAgent` - Natural language query
- ✅ `Agent_NaturalLanguage_CreateGroup_ShouldInvokeAgent` - NL command generation
- ✅ `Agent_MistypedCommand_ShouldInvokeAgent` - Typo correction
- ✅ `Login_ShouldSucceed` - Authentication
- ✅ `Agent_NaturalLanguage_LoginHelp_ShouldInvokeAgent` - Help via AI
- ✅ `Agent_NaturalLanguage_ShowContextHelp_ShouldInvokeAgent` - Context help
- ✅ `Agent_MistypedCommand_LoginTypo_ShouldInvokeAgent` - Login typo handling

### PowerShell Tests (`tests/PowerShell.Tests/`) - 17 tests

**Agent Module Tests** (`AgentCmdletTests`) - 6 tests:
- ✅ `Module_ShouldExist` - `bin/Agent/Cirrus.Agent.psd1` exists
- ✅ `InvokeAzV2Function_ShouldBeAvailable` - `Get-Command Invoke-AzV2`
- ✅ `AskAlias_ShouldBeAvailable` - `Get-Alias ask`
- ✅ `PleaseAlias_ShouldBeAvailable` - `Get-Alias please`
- ✅ `PlsAlias_ShouldBeAvailable` - `Get-Alias pls`
- ✅ `Agent_NaturalLanguage_ShouldInvokeAgent` - Natural language via alias

**Group Module Tests** (`GroupCmdletTests`) - 6 tests:
- ✅ `Module_ShouldExist` - `bin/Group/Cirrus.Group.psd1` exists
- ✅ `GetAzV2ResourceGroup_ShouldBeAvailable` - Cmdlet discovery
- ✅ `GetAzV2ResourceGroup_ShouldReturnGroups` - List groups
- ✅ `NewAzV2ResourceGroup_ShouldCreateGroup` - Create group
- ✅ `SetAzV2ResourceGroup_ShouldUpdateGroup` - Update group
- ✅ `RemoveAzV2ResourceGroup_ShouldDeleteGroup` - Delete group

**Login Module Tests** (`LoginCmdletTests`) - 5 tests:
- ✅ `Module_ShouldExist` - `bin/Login/Cirrus.Login.psd1` exists
- ✅ `ConnectAzV2Account_ShouldBeAvailable` - Cmdlet discovery
- ✅ `ConnectAzV2Account_ShouldAuthenticate` - Login
- ✅ `GetAzV2Context_ShouldReturnContext` - Get context
- ✅ `DisconnectAzV2Account_ShouldSucceed` - Logout

## 📝 Key Files Modified/Added

### Added Files (AI Agent Integration)
- `src/Core/Core.Agent/` - AI agent implementation
  - `AgentEngine.cs` - Natural language command engine
  - `CloudAgent.cs` - Azure OpenAI integration
- `src/Core/Core.Engine/Bootstrapper.cs` - DI bootstrapper (restored & extended)
- `src/Core/Core.Engine/OrchestratorEngine.cs` - Command orchestration
- `src/Agent/Agent/` - PowerShell Agent module
  - `Cirrus.Agent.psd1` - Module manifest
  - `AgentHelper.ps1` - Invoke-AzV2 function and aliases
- `tests/PowerShell.Tests/AgentCmdletTests.cs` - Agent module tests
- `scripts/deploy.sh` - Build and deployment automation
- `scripts/test.sh` - Test execution wrapper

### Modified Files (Merge with main)
- `cirrus.slnx` - Merged project structure (removed duplicates, uses Core.Cli)
- `src/Core/Core.Cli/Program.cs` - Simplified to 3-line Bootstrapper pattern
- `src/Directory.Build.props` - Target framework net9 (from main)
- `src/Group/Group/Cirrus.Group.psd1` - Removed agent functionality (now in Agent module)
- `src/Login/Login/Cirrus.Login.psd1` - Removed agent functionality (now in Agent module)
- `src/Core/Core.Engine/Cirrus.Core.psd1` - Converted to meta-module
- All `.csproj` files - Downgraded from net10.0 to net9, DependencyInjection 9.0.0

### Preserved from main
- `src/CLI/` - Old CLI implementation (not used, but kept for reference)
- `src/Extensions/Resources/` - Resource extensions
- `src/SharedContracts/` - Shared contracts
- Original module structure and business logic

## 🎯 Usage Examples

### CLI (azv2)
```bash
# Deploy binaries
./scripts/deploy.sh

# Structured commands
./bin/azv2 group list
./bin/azv2 group create --name mygroup --location eastus
./bin/azv2 login

# Natural language (AI agent)
./bin/azv2 "create a resource group called test"
./bin/azv2 "how do I list my groups?"
./bin/azv2 "grup list"  # Typo - agent suggests correction
```

### PowerShell
```powershell
# Import all modules via meta-module
Import-Module ./bin/Cirrus.psd1

# Or import individual modules
Import-Module ./bin/Group/Cirrus.Group.psd1
Import-Module ./bin/Login/Cirrus.Login.psd1
Import-Module ./bin/Agent/Cirrus.Agent.psd1

# Structured cmdlets
Connect-AzV2Account
Get-AzV2ResourceGroup
New-AzV2ResourceGroup -Name test -Location eastus

# Natural language (AI agent)
ask create a resource group
please show me all my groups
pls help with resource groups
```

### Running Tests
```bash
# Run all tests (auto-deploys first)
./scripts/test.sh

# Or run tests directly (must deploy first)
./scripts/deploy.sh
dotnet test

# Run specific test project
dotnet test tests/Cli.Tests/Cli.Tests.csproj
dotnet test tests/PowerShell.Tests/PowerShell.Tests.csproj
```

## 🔄 Merge Impact

### What Changed from main Merge
1. **Target Framework**: Reverted from net10.0 to net9 (main's standard)
2. **Package Versions**: Downgraded Microsoft.Extensions.DependencyInjection from 10.0.0 to 9.0.0
3. **Solution Structure**: Merged both `src/CLI/` (old, from main) and `src/Core/Core.Cli/` (current)
4. **Build Process**: Deploy script uses `src/Core/Core.Cli/` (Bootstrapper-based) not `src/CLI/`

### What Remains from agent-v2
1. **Bootstrapper Pattern**: Restored original design, extended for AI
2. **Orchestrator Engine**: DefaultEngine → AgentEngine fallback
3. **PowerShell Modules**: Cirrus.Agent, Cirrus.Group, Cirrus.Login, Cirrus (meta)
4. **Test Suite**: 29 integration tests validating full system
5. **Deployment Automation**: scripts/deploy.sh and scripts/test.sh

## 📚 Documentation
- `README.md` - Updated with azv2 CLI usage, build instructions, test guidance
- `src/ARCHITECTURE.md` - Comprehensive architecture documentation
- `AGENTS.md` - Repository guidelines for AI agents (build, test, conventions)
- `docs/spec/agent-experience.md` - AI agent design specification
- Inline XML comments in key files explaining architectural evolution

## 🚦 Current State
- ✅ **Build**: Successful (net9 with .NET 10 SDK)
- ✅ **Deploy**: Functional (azv2 + PowerShell modules to `bin/`)
- ✅ **Tests**: All passing (29/29)
- ✅ **CLI**: Working (`bin/azv2` with structured + NL support)
- ✅ **PowerShell**: Working (all 3 modules + meta-module)
- ✅ **Merge**: Clean (main merged, conflicts resolved)