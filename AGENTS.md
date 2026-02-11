# Repository Guidelines

## Project Structure & Module Organization
- `src/` – Core libraries and service modules. Shared props in `src/Directory.Build.props` target `net10.0`, enable nullable and implicit usings.
- `src/Core/` – Core framework (Abstractions, Engine with Bootstrapper, Implementation, Agent, CLI)
- `src/Agent/`, `src/Group/`, `src/Login/` – Service modules with Business/, Cmdlets/, Commands/ structure
- `tests/` – xUnit test projects (`Cli.Tests`) with Coverlet configured in `tests/Directory.Build.props`.
- `bin/` – Published binaries after running `./scripts/deploy.sh` (az CLI + PowerShell modules)
- `build/` – PowerShell build scripts
- `scripts/` – Deployment scripts (`deploy.sh`)
- `docs/spec/` – Design notes and specifications
- `global.json` – Pins the .NET SDK (use the version declared here)

## Architecture Overview
- **Bootstrapper Pattern**: `Core.Engine.Bootstrapper` registers all services via DI container, returns `ICommandEngine`
- **Orchestrator**: `OrchestratorEngine` delegates to `AgentEngine` (AI) for all command processing
- **Module System**: Service modules (Group, Login, etc.) provide Business/ logic, CLI Commands, and PowerShell Cmdlets
- **PowerShell**: Separate modules (Cirrus.Login, Cirrus.Group, Cirrus.Agent) with meta-module (Cirrus.psd1)

## Build, Test, and Development Commands
- **Build and deploy**: `./scripts/deploy.sh` (builds solution, publishes CLI to bin/, copies PowerShell modules)
- **Build only**: `dotnet build cirrus.slnx`
- **Run CLI**: `./bin/azv2 group list` or `./bin/azv2 "create a resource group"`
- **PowerShell**: `Import-Module ./bin/Cirrus.psd1` (imports all modules) or individual modules
- **Run tests**: `dotnet test tests/Cli.Tests/Cli.Tests.csproj`
- **Run tests with coverage**: `dotnet test --collect:"XPlat Code Coverage"`
- **Verify SDK**: `dotnet --info` (must match `global.json` version)

## Coding Style & Naming Conventions
- C# with 4-space indentation; UTF-8; one class per file when practical
- Naming: PascalCase for types/methods/properties; camelCase for locals/params; interfaces prefixed with `I`; test projects suffixed with `.Tests`
- Features: `<Nullable>enable</Nullable>`, `<ImplicitUsings>enable</ImplicitUsings>` are set; prefer modern language features
- Organize namespaces to mirror folders, e.g., `Core.Authentication.*`, `Group.Business.*`
- PowerShell: Module manifests (.psd1) in `<Module>/<Module>/` folder, e.g., `src/Group/Group/Cirrus.Group.psd1`

## Testing Guidelines
- Framework: xUnit (`[Fact]`, `[Theory]`). Place tests under `tests/<Project>.Tests`
- Naming: `MethodName_ShouldBehavior_WhenCondition`
- Coverage: Collected via Coverlet; aim for strong coverage on new/changed code
- CLI tests validate: structured commands work, agent fallback works, typo correction works
- PowerShell tests validate: cmdlets load, module imports work, Invoke-Az (ask/please/pls) works

## Commit & Pull Request Guidelines
- Commits: Use Conventional Commits where possible, e.g., `feat(auth): add token validator` or `fix(core): null check in mapper`
- PRs: Include a clear description, link related issues, list breaking changes, and add test coverage for new behavior. Keep PRs focused and small

## Security & Configuration Tips
- SDK pinned via `global.json`; ensure your local `dotnet` matches
- Avoid committing secrets; use environment variables or user secrets for local dev
- Azure OpenAI credentials: use `.env` file in `src/Core/Core.Agent/` (see `.env.example`)

## Documentation
- **Architecture**: See `src/ARCHITECTURE.md` for detailed system design
- **Agent Design**: See `docs/spec/agent-experience.md` for AI agent details
- **Module Structure**: Each service module has its own README in `src/<Module>/README.md`
- **README.md**: Main repository overview and quick start guide

## Agent-Specific Instructions
- This `AGENTS.md` applies to the entire repo
- If nested `AGENTS.md` files are added later, the most specific (deepest) file takes precedence for its subtree
- When making architectural changes, update inline documentation in code (XML comments explaining design decisions)
- Bootstrapper pattern must be preserved - do not revert to manual instantiation in Program.cs
- Service modules follow the pattern: Business/ (shared), Commands/ (CLI), Cmdlets/ (PowerShell)
