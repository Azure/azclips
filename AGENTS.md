# Repository Guidelines

## Project Structure & Module Organization
- `src/` – Core libraries (e.g., `Core.Abstractions`, `Core.Authentication`). Shared props in `src/Directory.Build.props` target `net10.0`, enable nullable and implicit usings.
- `tests/` – xUnit test projects (e.g., `Core.Authentication.Tests`) with Coverlet configured in `tests/Directory.Build.props`.
- `build/` – PowerShell scripts (e.g., `build.ps1`) for solution builds.
- `docs/spec/` – Design notes and specifications.
- `global.json` – Pins the .NET SDK (use the version declared here).

## Build, Test, and Development Commands
- Build solution: `pwsh -File build/build.ps1` (runs `dotnet build src/Core/Core.slnx`).
- Build all projects: `dotnet build` (respects `global.json`).
- Run tests with coverage: `dotnet test tests --collect:"XPlat Code Coverage"`.
- List SDK/version: `dotnet --info` (verify matches `global.json`).

## Coding Style & Naming Conventions
- C# with 4-space indentation; UTF-8; one class per file when practical.
- Naming: PascalCase for types/methods/properties; camelCase for locals/params; interfaces prefixed with `I`; test projects suffixed with `.Tests`.
- Features: `<Nullable>enable</Nullable>`, `<ImplicitUsings>enable</ImplicitUsings>` are set; prefer modern language features.
- Organize namespaces to mirror folders, e.g., `Core.Authentication.*`.

## Testing Guidelines
- Framework: xUnit (`[Fact]`, `[Theory]`). Place tests under `tests/<Project>.Tests`.
- Naming: `MethodName_ShouldBehavior_WhenCondition`.
- Coverage: Collected via Coverlet; aim for strong coverage on new/changed code. Run `dotnet test --collect:"XPlat Code Coverage"`.

## Commit & Pull Request Guidelines
- Commits: Use Conventional Commits where possible, e.g., `feat(auth): add token validator` or `fix(core): null check in mapper`.
- PRs: Include a clear description, link related issues, list breaking changes, and add test coverage for new behavior. Keep PRs focused and small.

## Security & Configuration Tips
- SDK pinned via `global.json`; ensure your local `dotnet` matches. Avoid committing secrets; use environment variables or user secrets for local dev.

## Agent-Specific Instructions
- This `AGENTS.md` applies to the entire repo. If nested `AGENTS.md` files are added later, the most specific (deepest) file takes precedence for its subtree.
