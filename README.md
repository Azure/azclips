# Project Cirrus

A .NET 10.0 modular framework with core abstractions and authentication components.

## Repository Structure

```
cirrus/
├── 📁 .github/                   # GH actions, code owners
├── 📁 bin/                       # Published binaries (after build/deploy)
│   ├── azv2                      # CLI executable
│   ├── Cirrus.psd1               # Meta-module (imports all modules)
│   ├── Agent/                    # AI agent PowerShell module
│   ├── Group/                    # Resource group PowerShell module
│   └── Login/                    # Authentication PowerShell module
├── 📁 build/                     # Build scripts
├── 📁 docs/                      # Documentation
│   └── spec/                     # Technical specifications
├── 📁 scripts/                   # Deployment and utility scripts
│   └── deploy.sh                 # Build & deploy script
├── 📁 src/                       # Source code
│   ├── Directory.Build.props     # Shared build properties (net10.0)
│   ├── ARCHITECTURE.md           # Detailed architecture documentation
│   ├── Core/                     # Core libraries
│   │   ├── Core.Abstractions/    # Base abstractions & interfaces
│   │   ├── Core.Agent/           # AI agent integration
│   │   ├── Core.Authentication/  # Authentication framework
│   │   ├── Core.Cli/             # azv2 CLI entry point (uses Bootstrapper)
│   │   ├── Core.Engine/          # Bootstrapper, orchestration, command engines
│   │   └── Core.Implementation/  # Core service implementations
│   ├── Agent/                    # AI agent PowerShell module source
│   │   └── Agent/
│   ├── Group/                    # Resource group management module
│   │   ├── Business/             # Shared business logic
│   │   ├── Cmdlets/              # PowerShell cmdlets
│   │   ├── Commands/             # CLI command implementations
│   │   └── Group/                # Module manifest
│   └── Login/                    # Authentication module
│       ├── Business/             # Shared business logic
│       ├── Cmdlets/              # PowerShell cmdlets
│       ├── Commands/             # CLI command implementations
│       └── Login/                # Module manifest
├── 📁 tests/                     # Test projects
│   ├── Cli.Tests/                # CLI integration tests
│   └── PowerShell.Tests/         # PowerShell module tests
├── 📄 global.json               # .NET SDK version (10.0.100)
├── 📄 cirrus.slnx              # Main solution file
├── 📄 AGENTS.md                # Repository guidelines for AI agents
├── 📄 CHANGELOG.md             # Version history
├── 📄 CONTRIBUTING.md          # Contribution guidelines
└── 📄 LICENSE                  # License information
```

## Key Features

- **Target Framework**: .NET 10.0 (currently in RC, expected to be released on November 11, 2025)
- **Code Quality**: Analyzers enabled, warnings as errors (except tests)
- **Testing**: xUnit with Coverlet code coverage
- **Build System**: MSBuild, should look into Nuke or Cake in the future

## Quick Start

### Prerequisites
- [.NET 10.0 SDK](https://dotnet.microsoft.com/en-us/download/dotnet/10.0) (version pinned in `global.json`)
- PowerShell 7.0+ (for PowerShell modules)

### Build & Deploy

**Important**: The tests rely on binaries being deployed to `bin/`, so you must run `./scripts/deploy.sh` before testing. The standard `dotnet build` only compiles to `artifacts/` but doesn't set up the runtime structure that tests expect.

```bash
# One-command build and deploy (required before testing)
./scripts/deploy.sh

# This will:
# 1. Build the solution in Release mode
# 2. Publish azv2 CLI to bin/
# 3. Copy PowerShell modules to bin/
# 4. Set up the correct structure for both CLI and PowerShell to work

# Now you can use the CLI
./bin/azv2 --help
./bin/azv2 group list
./bin/azv2 create a resource group called test
```

### PowerShell Usage
```powershell
# Import all modules via meta-module
Import-Module ./bin/Cirrus.psd1

# Or import individual modules
Import-Module ./bin/Group/Cirrus.Group.psd1
Import-Module ./bin/Login/Cirrus.Login.psd1
Import-Module ./bin/Agent/Cirrus.Agent.psd1

# Use cmdlets
Connect-AzV2Account
Get-AzV2ResourceGroup
New-AzV2ResourceGroup -Name test -Location eastus

# Use natural language with AI agent
ask "create a resource group"
please "show me all my groups"
```

### Running Tests

**Why `test.sh` is needed**: Both CLI and PowerShell integration tests run against the deployed binaries in `bin/`, not the build artifacts in `artifacts/`. This ensures tests validate the actual runtime configuration that users will experience. The `test.sh` script automatically runs `deploy.sh` first to ensure binaries are up-to-date.

```bash
# Recommended: Use test script (auto-deploys, then tests)
./scripts/test.sh

# Manual approach (must deploy first)
./scripts/deploy.sh
dotnet test --no-build

# Run individual test projects
dotnet test tests/Cli.Tests/Cli.Tests.csproj
dotnet test tests/PowerShell.Tests/PowerShell.Tests.csproj

# Run tests with coverage
dotnet test --collect:"XPlat Code Coverage"
```

**Test Structure:**
- **Cli.Tests** (12 tests) - Integration tests that spawn `bin/azv2` process and validate structured commands, natural language, and agent fallback
- **PowerShell.Tests** (17 tests) - Integration tests that import modules from `bin/` and validate cmdlets, module loading, and aliases
- **AgentCmdletTests** (6 tests) - Tests for the `Cirrus.Agent` module (`Invoke-AzV2`, `ask`, `please`, `pls`)


## azv2 CLI - Next Generation Azure Command Interface

The `azv2` command provides both traditional command-line syntax and natural language capabilities through an integrated AI agent.

### Usage

**Structured Commands:**
```bash
azv2 group create --name mygroup --location eastus
azv2 group list
azv2 group delete --name mygroup
```

**Natural Language:**
```bash
azv2 create a resource group called test
azv2 how do I list all my resource groups?
azv2 show me everything in eastus
```

The CLI automatically falls back to the AI agent when standard parsing fails, interprets your intent, and can execute commands after confirmation.

### Architecture

**Bootstrapper Pattern**: The CLI uses a dependency injection bootstrapper (`Core.Engine.Bootstrapper`) that registers all services as singletons and returns a configured `ICommandEngine`.

**Orchestration**: The `OrchestratorEngine` coordinates between:
- **DefaultCommandEngine** - Fast path for structured commands (e.g., `group create --name test`)
- **AgentEngine** - AI-powered natural language processing (e.g., "create a resource group")
- **CommandBinder** - Dynamic module loading via assembly reflection
- **CommandParser** - Argument parsing for structured commands

**Module Architecture**: Service modules (Group, Login, etc.) provide:
- **Business Layer** - Shared logic used by both CLI and PowerShell
- **Commands** - CLI command implementations (`ICommand`)
- **Cmdlets** - PowerShell cmdlet implementations
- **Module Manifest** - PowerShell module definition (`.psd1`)

See [`src/ARCHITECTURE.md`](src/ARCHITECTURE.md) for detailed architecture documentation and [`docs/spec/agent-experience.md`](docs/spec/agent-experience.md) for AI agent details.

## PowerShell Modules

Cirrus provides separate PowerShell modules following the Azure PowerShell pattern:

- **Cirrus** - Meta-module (imports all sub-modules)
- **Cirrus.Login** - Authentication cmdlets (`Connect-AzV2Account`, `Disconnect-AzV2Account`, `Get-AzV2Context`)
- **Cirrus.Group** - Resource group cmdlets (`Get-AzV2ResourceGroup`, `New-AzV2ResourceGroup`, etc.)
- **Cirrus.Agent** - AI natural language interface (`Invoke-AzV2`, aliases: `ask`, `please`, `pls`)

### Example
```powershell
# Import all modules
Import-Module ./bin/Cirrus.psd1

# Authenticate
Connect-AzV2Account

# Use structured cmdlets
New-AzV2ResourceGroup -Name test -Location eastus
Get-AzV2ResourceGroup

# Use natural language
ask "create a resource group in westus"
please "show me all my groups"
pls "help me with resource groups"
```
