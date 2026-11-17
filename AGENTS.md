# Repository Guidelines

## Essentials
- Use the .NET SDK pinned in `global.json`.
- Build with `dotnet build azclips.slnx`.
- Run targeted tests with `dotnet test <test-project>.csproj`.
- Keep changes focused and avoid unrelated refactors.

## Project Layout
- `src/` contains product code and shared build props.
- `src/Core/` contains core framework libraries.
- `src/Extensions/` contains extension projects.
- `src/PS/` contains PowerShell module projects and manifests.
- `tests/` contains xUnit test projects and fixtures.
- `tools/` contains repository tooling.
- `docs/` and `wiki/` contain documentation.

## Coding Guidelines
- Use C# with nullable enabled and implicit usings.
- Follow existing naming, namespace, and folder conventions.
- Prefer dependency injection and existing helpers over manual wiring.
- Add or update tests for behavior changes.

## PowerShell Modules
- Build the solution before importing binary-backed modules.
- Import module manifests from `src/PS/<Module>/<Module>.psd1` during local development.
- Use `build/Set-ModuleConfiguration.ps1` when staging modules under `artifacts/<Configuration>/PS`.

## DevOps & CI/CD
- For Azure DevOps pipeline YAML (`.azure-pipelines/`), see [.azure-pipelines/AGENTS.md](.azure-pipelines/AGENTS.md).
- For build/packaging/signing/publishing scripts (`devops/scripts/`), see [devops/scripts/AGENTS.md](devops/scripts/AGENTS.md).
- These scoped guides are the source of truth for pipelines and scripts; prefer them over this file for DevOps work.

## Agent
- For the AI agent under `Agent/` and its tests, see [Agent/AGENTS.md](Agent/AGENTS.md).

## Safety
- Do not commit secrets or local-only configuration.
- Preserve existing public behavior unless the task explicitly changes it.
- Keep pull requests small and include validation notes.
