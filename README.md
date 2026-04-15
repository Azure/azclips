# Project Cirrus

A .NET 9.0 extension-based CLI framework with AI-powered natural language capabilities for Azure resource management.

📚 **Documentation:** [https://azure.github.io/cirrus/](https://azure.github.io/cirrus/)

Alternatively, clone the repository and run the `wiki:preview` task in VS Code to view a rendered version of the wiki locally.

## Repository Structure

```
cirrus/
├── 📁 .github/                   # GH actions, code owners
├── 📁 build/                     # Build scripts
├── 📁 docs/                      # Documentation
│   └── spec/                     # Technical specifications
├── 📁 src/                       # Source code
│   ├── Directory.Build.props     # Shared build properties (net9)
│   ├── CLI/                      # CLI entry point
│   │   ├── Core.Cli.csproj       # Main CLI project (produces 'azv2' binary)
│   │   ├── Program.cs            # Entry point with AI fallback
│   │   ├── Parser/               # Extension dispatcher and parsers
│   │   └── Exceptions/           # CLI-specific exceptions
│   ├── Core/                     # Core libraries
│   │   ├── Core.Abstractions/    # Base abstractions & interfaces
│   │   ├── Core.Authentication/  # Authentication framework
│   │   ├── Core.Engine/          # Command engine and bootstrapper
│   │   └── Core.Implementation/  # Core service implementations
│   ├── Extensions/               # Extension modules
│   │   └── Resources/            # Resource group management extension
│   │       ├── ExtensionIndex.cs # Command registry
│   │       ├── NewResourceGroup.cs # Command implementation
│   │       ├── ResourcesClient.cs  # Azure REST API client
│   │       └── ResourcegroupModel.cs # Data models
│   ├── Agent/                    # Agent module with PowerShell cmdlets
│   ├── Group/                    # Group module with Business/Cmdlets/Commands
│   ├── Login/                    # Login module with Business/Cmdlets/Commands
│   ├── PS/                       # PowerShell modules (source)
│   │   └── Resources/            # Resource group PowerShell module
│   │       ├── Resources.psd1    # Module manifest
│   │       ├── Resources.psm1    # Module loader
│   │       └── exports/          # Exported functions
├── 📁 tests/                     # Test projects
│   ├── Cli.Tests/                # CLI integration tests
│   ├── PowerShell.Tests/         # PowerShell module tests
│   └── Core/                     # Core library tests
├── 📁 artifacts/                 # Build output (generated)
│   └── Release/                  # Release builds
│       ├── Core/                 # Core libraries
│       ├── Core.Cli/             # CLI binary 'azv2' + dependencies + Resources module
│       ├── Microsoft.Azure.Cirrus.Extensions.Login/     # Login extension + PowerShell module
│       └── Microsoft.Azure.Cirrus.Extensions.Resources/  # Resources extension
├── 📄 global.json               # .NET SDK version (9.0)
├── 📄 cirrus.slnx              # Main solution file
├── 📄 Shared.props             # Shared build configuration
├── 📄 AGENTS.md                # Repository guidelines for AI agents
├── 📄 CHANGELOG.md             # Version history
├── 📄 CONTRIBUTING.md          # Contribution guidelines
└── 📄 LICENSE                  # License information
```

## Key Features

- **Target Framework**: .NET 9.0
- **Extension Architecture**: Dynamic extension loading from artifacts directory
- **AI Integration**: Natural language command processing with automatic fallback
- **Real Azure API**: HTTP client for Azure Resource Manager REST API
- **PowerShell Support**: Full PowerShell module wrapping extension functionality
- **Code Quality**: Analyzers enabled, warnings as errors

## Quick Start

### Prerequisites

- [.NET 10.0 SDK](https://dotnet.microsoft.com/en-us/download/dotnet/10.0) (version pinned in `global.json`)
- PowerShell 7.0+ (for PowerShell modules)
- Azure subscription (for actual API calls)
- [`Microsoft.PowerShell.PlatyPS`](https://www.powershellgallery.com/packages/Microsoft.PowerShell.PlatyPS/1.0.1) **1.0.1** (only for `-GenerateHelpArtifacts`; auto-installed by `build/Install-BuildDependencies.ps1`)

### Build

```bash
# Build the solution
dotnet build -c Release cirrus.slnx

# The CLI binary is produced at artifacts/Release/Core.Cli/azv2
# Add to PATH for convenient access
export PATH="$PWD/artifacts/Release/Core.Cli:$PATH"

# Or run directly
./artifacts/Release/Core.Cli/azv2 --help
```

### CLI Usage

The `azv2` CLI supports both structured extension commands and natural language via AI. It uses an extension-based architecture where commands are dynamically loaded from extension modules.

**Structured Commands (Extension-Based):**

```bash
azv2 group create --ResourceGroupName mygroup --Location eastus --SubscriptionId <sub-id> --AccessToken <token>
```

**Natural Language (AI-Powered):**

```bash
azv2 "create a resource group named test in eastus"
azv2 "how do I list all my resource groups?"
azv2 "show me everything in westus"
```

The CLI automatically tries extension-based parsing first. On parse or execution failures, it falls back to the AI agent which interprets your intent and can generate executable commands.

### PowerShell Usage

Cirrus provides PowerShell modules in [`src/PS`](src/PS) that can be used directly without building.

```powershell
# Import the Login module (binary module with cmdlets)
Import-Module ./src/PS/Login/Login.psd1

# Import the Resources module (script module)
Import-Module ./src/PS/Resources/Resources.psd1

# Use authentication cmdlets
Connect-CirrusAccount -TenantId <tenant-id>

# Use resource management functions
New-ResourceGroup -Name mygroup -Location eastus

```

**Available Modules:**

- **Login** ([`src/PS/Login/Login.psd1`](src/PS/Login/Login.psd1)) - Binary module with `Connect-CirrusAccount` cmdlet for Azure authentication
- **Resources** ([`src/PS/Resources/Resources.psd1`](src/PS/Resources/Resources.psd1)) - Script module with resource management functions

**Note**: The Login module requires the solution to be built first (`dotnet build -c Release`) so that `Login.dll` is available in [`artifacts/Release/Microsoft.Azure.Cirrus.Extensions.Login/`](artifacts/Release/Microsoft.Azure.Cirrus.Extensions.Login/). The Resources module works directly from source.

### Running Tests

```bash
# Run all tests
dotnet test

# Run individual test projects
dotnet test tests/Cli.Tests/Cli.Tests.csproj

# Run tests with coverage
dotnet test --collect:"XPlat Code Coverage"
```

