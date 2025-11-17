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

### Build Commands
```powershell
# Verify SDK version
dotnet --info

# Build all projects directly
dotnet build cirrus.slnx

# Run tests with coverage
dotnet test --collect:"XPlat Code Coverage"

# (Optional) Build solution via PowerShell script
pwsh -File build/build.ps1
```

## Core.Agent - Azure Command Assistant

An interactive Azure CLI and PowerShell command assistant with three deployment options:

### Usage Options

**1. Cloud Mode (Default) - Azure OpenAI**
```bash
dotnet run --project src/Core/Core.Agent
```
Requires `.env` file with Azure OpenAI credentials (see `src/Core/Core.Agent/README.md`).

**2. Local Mode - Phi-3.5-mini**
```bash
dotnet run --project src/Core/Core.Agent -- --local-model
```
Fully offline, automatically downloads model (~2.4GB) on first run.

**3. Azure Copilot Mode - Production Service**
```bash
dotnet run --project src/Core/Core.Agent -- --use-copilot
```
Requires Azure authentication (`az login`).

**Features:**
- Interactive command construction for Azure CLI and PowerShell
- Three interaction types: "how to" questions, "do it for me" requests, command validation
- Streaming responses with conversation history
- Full privacy in local mode

See [`src/Core/Core.Agent/README.md`](src/Core/Core.Agent/README.md) for detailed setup and examples.
