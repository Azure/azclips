# Project Cirrus

A .NET 10.0 modular framework with core abstractions and authentication components.

## Repository Structure

```
cirrus/
├── 📁 .github/                   # GH actions, code owners
├── 📁 build/                     # Build scripts
├── 📁 docs/                      # Documentation
│   └── spec/                     # Technical specifications
├── 📁 src/                       # Source code
│   ├── Directory.Build.props     # Shared build properties (net10.0)
│   └── Core/                     # Core libraries
│       ├── Core.Abstractions/    # Base abstractions & interfaces
│       ├── Core.Agent/           # Azure CLI/PowerShell assistant
│       └── Core.Authentication/   # Authentication framework
├── 📁 tests/                     # Test projects
│   ├── Directory.Build.props     # Test-specific build properties
│   └── Core/                     # Core library tests
│       └── Core.Authentication.Tests/
├── 📄 global.json               # .NET SDK version (10.0.100-rc.2)
├── 📄 cirrus.slnx              # Main solution file, contains everything
├── 📄 AGENTS.md                # Repository guidelines
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

### Build & Publish
```bash
# Verify SDK version
dotnet --info

# Build all projects
dotnet build cirrus.slnx

# Build and publish az CLI to bin/ (makes it globally executable)
dotnet publish src/Core/Core.Cli/Core.Cli.csproj -c Release -o /workspaces/cirrus/bin --self-contained false

# Add bin/ to PATH for global access (already configured in devcontainer)
export PATH="/workspaces/cirrus/bin:$PATH"

# Now you can run az from anywhere
az --help
```

### Running Tests
```bash
# Run tests with coverage
dotnet test --collect:"XPlat Code Coverage"
```

### Build Scripts
```powershell
# (Optional) Build solution via PowerShell script
pwsh -File build/build.ps1
```

## az CLI - Azure Command Interface

The `az` command provides both traditional command-line syntax and natural language capabilities through an integrated AI agent.

### Usage

**Traditional Commands:**
```bash
az group create --name mygroup --location eastus
az login
```

**Natural Language:**
```bash
az create a storage account called mystore in westus
az I would like to login
az how do I list all VMs
```

The CLI automatically falls back to the AI agent when standard parsing fails, interprets your intent, and can execute commands after confirmation.

### Architecture

- **DefaultCommandEngine** - Parses standard Azure CLI syntax
- **AgentEngine** - Interprets natural language when parsing fails
- **Modules** - Dynamic loading of command modules (Group, Login, etc.)

See [`src/Core/Core.Agent/README.md`](src/Core/Core.Agent/README.md) for agent-specific details and [`docs/spec/agent-experience.md`](docs/spec/agent-experience.md) for architecture documentation.

## Core.Agent - Standalone Testing

For testing and evaluation, the agent can be run independently:

**1. Cloud Mode (Default) - Azure OpenAI**
```bash
dotnet run --project src/Core/Core.Agent
```
Requires `.env` file in `src/Core/Core.Agent/` with Azure OpenAI credentials.

**2. Local Mode - Phi-3.5-mini**
```bash
dotnet run --project src/Core/Core.Agent -- --local-model
```
Fully offline, automatically downloads model (~2.4GB) on first run.

**3. Azure Copilot Mode**
```bash
dotnet run --project src/Core/Core.Agent -- --use-copilot
```
Requires Azure authentication (`az login` from Azure CLI, not this az).
