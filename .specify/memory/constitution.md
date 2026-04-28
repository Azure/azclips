<!--
  Sync Impact Report
  ===================
  Version change: 0.0.0 (template) → 1.0.0
  Modified principles: N/A (initial creation)
  Added sections:
    - Core Principles (7 principles)
    - Technology Stack & Constraints
    - Development Workflow
    - Governance
  Removed sections: None
  Templates requiring updates:
    - .specify/templates/plan-template.md ✅ aligned (Constitution Check section present)
    - .specify/templates/spec-template.md ✅ aligned (no constitution-specific changes needed)
    - .specify/templates/tasks-template.md ✅ aligned (phase structure compatible)
  Follow-up TODOs: None
-->

# Cirrus Constitution

## Core Principles

### I. Bootstrapper DI Pattern (NON-NEGOTIABLE)

All service registration MUST go through `CirrusServiceProvider` using
the static, thread-safe, double-checked locking bootstrap pattern.

- Every service MUST be registered as a Singleton.
- DI registration MUST use extension methods named
  `services.Register*Services()`.
- Manual instantiation in `Program.cs` or cmdlet entry points is
  forbidden — the bootstrapper returns `ICommandEngine` and all
  downstream dependencies are resolved through the container.
- Reverting to manual wiring is a constitutional violation regardless
  of perceived convenience.

**Rationale**: A single composition root guarantees deterministic
lifetime management, testability via DI swap, and consistent behavior
across CLI and PowerShell surfaces.

### II. Dual-Surface Shared Codebase

Cirrus serves two client surfaces — a native CLI binary and PowerShell
modules — from one shared codebase. All domain logic MUST reside in a
shared `Business/` layer.

- Service modules MUST follow the structure:
  `Business/` (shared), `Commands/` (CLI), `Cmdlets/` (PowerShell).
- CLI commands use kebab-case (e.g., `resource-group list`).
- PowerShell cmdlets use `Verb-CirrusNoun` (e.g., `Get-CirrusGroup`).
- No business logic is permitted inside `Commands/` or `Cmdlets/` —
  they are thin dispatch layers only.

**Rationale**: A shared Business/ layer prevents feature drift between
surfaces, reduces duplication, and ensures behavioral parity.

### III. Orchestrator-First Dispatch

The `OrchestratorEngine` MUST attempt structured command dispatch via
`DefaultCommandEngine` first. The AI `AgentEngine` is invoked only
when structured dispatch throws an exception.

- Structured commands are always preferred over AI interpretation.
- Agent fallback is a safety net, not a primary dispatch path.
- New commands MUST be registered in the extension index so they are
  resolved structurally before agent fallback is considered.

**Rationale**: Deterministic command resolution provides predictable
behavior, lower latency, and auditability. AI fallback enables
graceful handling of unrecognized or natural-language input.

### IV. Extension Contract: IProcessor

All new commands MUST implement `IProcessor`
(`Init(ICommandContext)` + `ProcessAsync()`). The legacy `ICommand`
interface is deprecated and MUST NOT be used for new work.

- `IExtensionIndex` maps command names to `CommandMetadata` per
  `ClientType` (CLI / PowerShell).
- `ICommandContext` provides per-invocation isolated auth, output,
  telemetry, and HTTP pipeline. Commands MUST NOT share or cache
  context instances across invocations.
- Extensions are namespaced as
  `Microsoft.Azure.Cirrus.Extensions.{Name}`.

**Rationale**: IProcessor + ICommandContext gives each invocation an
isolated scope, enabling safe concurrency and clean testability
without global state.

### V. Thread-Affinity & Isolation

CLI and PowerShell have distinct threading and assembly-loading
contracts that MUST be respected.

- **CLI**: Uses per-extension `AssemblyLoadContext` isolation. Async
  execution via `ProcessAsync()` is the norm.
- **PowerShell**: Has strict thread-affinity. `IEngine.Process()` is
  synchronous and uses a `Channel<T>` pump to marshal work onto the
  correct thread.
- Code shared between surfaces MUST NOT assume async availability —
  the Business/ layer MUST expose both sync and async paths or use
  sync-over-async only where the PowerShell pump explicitly allows it.

**Rationale**: PowerShell's runspace model is single-threaded; violating
thread-affinity causes deadlocks and undefined behavior. ALC isolation
in CLI prevents version conflicts between extensions.

### VI. Strict Code Quality

The codebase enforces zero-tolerance for warnings and style drift.

- `TreatWarningsAsErrors` and `EnforceCodeStyleInBuild` are enabled
  and MUST NOT be disabled.
- `<Nullable>enable</Nullable>` and `<ImplicitUsings>enable</ImplicitUsings>`
  are set globally — do not override per-project.
- C# 4-space indentation, UTF-8 encoding.
- PascalCase for types, methods, and properties; camelCase for locals
  and parameters; `I` prefix for interfaces.
- Namespaces MUST mirror folder structure: `Core.Abstractions`,
  `Core.Runtime`, `Core.Engine`,
  `Microsoft.Azure.Cirrus.Extensions.{Name}`.

**Rationale**: Compiler-enforced quality eliminates entire categories
of null-reference and style-related defects at build time rather than
in review.

### VII. Test-Driven Verification

All new and changed code MUST have corresponding test coverage.

- Framework: xUnit 2.9.3 with Coverlet for coverage collection.
- Test naming: `MethodName_ShouldBehavior_WhenCondition`.
- Tests reside under `tests/<Project>.Tests`.
- CLI tests MUST validate: structured command dispatch, agent fallback,
  and typo correction.
- PowerShell tests MUST validate: cmdlet loading, module import, and
  `Invoke-Az` variants (`ask`, `please`, `pls`).
- Build command: `dotnet test tests/Cli.Tests/Cli.Tests.csproj`.
- Coverage: `dotnet test --collect:"XPlat Code Coverage"`.

**Rationale**: Automated tests are the executable specification of
Cirrus behavior; untested code is assumed broken.

## Technology Stack & Constraints

- **Runtime**: .NET 9 (SDK 9.0.308, pinned via `global.json`).
- **Build**: `dotnet build cirrus.slnx` — the solution MUST remain
  buildable at all times on the pinned SDK.
- **Deploy**: `./scripts/deploy.sh` publishes CLI to `bin/` and copies
  PowerShell modules.
- **CLI entry point**: `./bin/azv2`.
- **PowerShell entry point**: `Import-Module ./bin/Cirrus.psd1`.
- **Target framework**: `net10.0` (as declared in
  `src/Directory.Build.props`).
- **Secrets**: MUST NOT be committed. Use environment variables or
  .NET user secrets. Azure OpenAI credentials use `.env` in
  `src/Core/Core.Agent/`.

## Development Workflow

- **Conventional Commits**: Use prefixes such as `feat(auth):`,
  `fix(core):`, `docs:`, `test:`.
- **Pull Requests**: MUST include a clear description, link related
  issues, list breaking changes, and add test coverage for new
  behavior. Keep PRs focused and small.
- **Code Review**: Every PR MUST verify compliance with this
  constitution before merge.
- **Build Verification**: A successful `dotnet build cirrus.slnx` and
  `dotnet test tests/Cli.Tests/Cli.Tests.csproj` is a merge gate.

## Governance

This constitution supersedes all conflicting practices, ad-hoc
conventions, and informal agreements.

- **Precedence**: Constitution > AGENTS.md > inline code comments.
- **Amendments**: Any change to this document MUST be versioned using
  semantic versioning (MAJOR for principle removals/redefinitions,
  MINOR for additions/expansions, PATCH for clarifications/typos).
- **Compliance Review**: All PRs and code reviews MUST check
  adherence to the principles above. Violations MUST be resolved
  before merge.
- **Complexity Justification**: Any deviation from these principles
  MUST be documented with a rationale and a simpler rejected
  alternative.
- **Runtime Guidance**: See `AGENTS.md` for agent-specific
  development guidance and `docs/spec/ARCHITECTURE.md` for detailed
  system design.

**Version**: 1.0.0 | **Ratified**: 2026-04-29 | **Last Amended**: 2026-04-29
