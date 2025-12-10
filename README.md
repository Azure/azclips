# Project Cirrus

A .NET 9.0 extension-based CLI framework with AI-powered natural language capabilities for Azure resource management.

## Repository Structure

```
cirrus/
├── 📁 .github/                   # GH actions, code owners
├── 📁 build/                     # Build scripts
├── 📁 docs/                      # Documentation
│   └── spec/                     # Technical specifications
├── 📁 src/                       # Source code
│   ├── Directory.Build.props     # Shared build properties (net9)
│   ├── CLI/                      # az CLI entry point
│   │   ├── Core.Cli.csproj       # Main CLI project (produces 'az' binary)
│   │   ├── Program.cs            # Entry point with AI fallback
│   │   ├── Parser/               # Extension dispatcher and parsers
│   │   └── Exceptions/           # CLI-specific exceptions
│   ├── Core/                     # Core libraries
│   │   ├── Core.Abstractions/    # Base abstractions & interfaces
│   │   ├── Core.Agent/           # AI agent integration
│   │   ├── Core.Authentication/  # Authentication framework
│   │   ├── Core.Engine/          # Command engine and bootstrapper
│   │   └── Core.Implementation/  # Core service implementations
│   ├── Extensions/               # Extension modules
│   │   └── Resources/            # Resource group management extension
│   │       ├── ExtensionIndex.cs # Command registry
│   │       ├── NewResourceGroup.cs # Command implementation
│   │       ├── ResourcesClient.cs  # Azure REST API client
│   │       └── ResourcegroupModel.cs # Data models
│   ├── PS/                       # PowerShell modules
│   │   └── Resources/            # Resource group PowerShell module
│   │       ├── Resources.psd1    # Module manifest
│   │       ├── Resources.psm1    # Module loader
│   │       └── exports/          # Exported functions
│   └── SharedContracts/          # Shared contracts and interfaces
├── 📁 tests/                     # Test projects
│   ├── Cli.Tests/                # CLI integration tests
│   └── PowerShell.Tests/         # PowerShell module tests
├── 📄 global.json               # .NET SDK version (9.0)
├── 📄 cirrus.slnx              # Main solution file
├── 📄 AGENTS.md                # Repository guidelines for AI agents
├── 📄 CHANGELOG.md             # Version history
├── 📄 CONTRIBUTING.md          # Contribution guidelines
└── 📄 LICENSE                  # License information
```

## Key Features

- **Target Framework**: .NET 9.0
- **Extension Architecture**: Dynamic extension loading via `AZURE_CLI_EXTENSION_DIRECTORY`
- **AI Integration**: Natural language command processing with automatic fallback
- **Real Azure API**: HTTP client for Azure Resource Manager REST API
- **PowerShell Support**: Full PowerShell module wrapping extension functionality
- **Code Quality**: Analyzers enabled, warnings as errors

## Quick Start

### Prerequisites
- [.NET 9.0 SDK](https://dotnet.microsoft.com/en-us/download/dotnet/9.0) (version pinned in `global.json`)
- PowerShell 7.0+ (for PowerShell modules)
- Azure subscription and access token (for actual API calls)

### Build & Publish

```bash
# Build the solution
dotnet build cirrus.slnx -c Release

# Publish the CLI (outputs to artifacts/Release/Core.Cli/ due to Shared.props)
dotnet publish src/CLI/Core.Cli.csproj -c Release

# Add to PATH for convenient access
export PATH="$PWD/artifacts/Release/Core.Cli:$PATH"

# Or run directly
./artifacts/Release/Core.Cli/az --help
```

### CLI Usage

The `az` command supports both structured extension commands and natural language via AI.

**Structured Commands (Extension-Based):**
```bash
az group create --ResourceGroupName mygroup --Location eastus --SubscriptionId <sub-id> --AccessToken <token>
```

**Natural Language (AI-Powered):**
```bash
az create a resource group named test in eastus
az how do I list all my resource groups?
az show me everything in westus
```

The CLI automatically tries extension-based execution first. On parse or execution failures, it falls back to the AI agent which interprets your intent and can generate executable commands.

### PowerShell Usage

```powershell
# Import the Resources module
Import-Module ./src/PS/Resources/Resources.psd1

# Use cmdlets (requires building artifacts first with dotnet build)
New-ResourceGroup -SubscriptionId <sub-id> -Name mygroup -Location eastus -AccessToken <token>
```

**Note**: The PowerShell module references assemblies from `artifacts/Release/` so you must run `dotnet build` before importing the module.

### Running Tests

```bash
# Build first
dotnet build cirrus.slnx -c Release

# Run all tests
dotnet test

# Run individual test projects
dotnet test tests/Cli.Tests/Cli.Tests.csproj
dotnet test tests/PowerShell.Tests/PowerShell.Tests.csproj

# Run tests with coverage
dotnet test --collect:"XPlat Code Coverage"
```
