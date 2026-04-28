# Implementation Plan: Profile Management

**Branch**: `001-profile-management` | **Date**: 2026-04-29 | **Spec**: [spec.md](spec.md)
**Input**: Feature specification from `specs/001-profile-management/spec.md`

## Summary

Introduce named authentication profiles to Cirrus, replacing the single-context `AuthContext` persistence with a `Profile`-based model that supports CRUD, active-profile switching, login/logout integration, and per-command `--profile` overrides. Profiles are persisted as a single `profiles.json` file at `~/.cirrus/` with atomic writes. The `IProfileService` interface lives in `Core.Abstractions` with the implementation in `Core.Runtime`. Profile commands are added to the Login extension with dual CLI/PS dispatch tables. `Engine.CreateCommandContext()` already scaffolds profile resolution; this plan completes the implementation.

## Technical Context

**Language/Version**: C# / .NET 9 (SDK 9.0.308, pinned via `global.json`)
**Primary Dependencies**: System.Text.Json (serialization), MSAL.NET (token cache correlation), Microsoft.Extensions.DependencyInjection
**Storage**: Single `profiles.json` file at `Environment.SpecialFolder.LocalApplicationData/.cirrus/`
**Testing**: xUnit 2.9.3 with Coverlet; `StubProfileService` (in-memory) for unit tests
**Target Platform**: Windows + macOS (cross-platform CLI + PowerShell 7)
**Project Type**: CLI + PowerShell module (dual-surface shared codebase)
**Performance Goals**: Profile operations < 500ms for 100 profiles (SC-005)
**Constraints**: Atomic writes (write-to-temp + `File.Move`), `SemaphoreSlim(1,1)` for in-process concurrency, owner-only file permissions (Unix `0600` / Windows current-user ACL)
**Scale/Scope**: Up to ~100 profiles per user; 20 FRs, 6 user stories, 7 success criteria

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Status | Evidence |
|-----------|--------|----------|
| I. Bootstrapper DI (NON-NEGOTIABLE) | ✅ PASS | `IProfileService` registered via `RegisterProfileServices()` extension method in `CirrusServiceProvider.Initialize()`. Already scaffolded in codebase. |
| II. Dual-Surface Shared Codebase | ✅ PASS | Profile business logic in shared `ProfileService`. CLI commands in Login extension's `Commands/`-equivalent (Processors/). PS cmdlets in Login PS module. No business logic in dispatch layers. |
| III. Orchestrator-First Dispatch | ✅ PASS | All profile commands registered in `ExtensionIndex` with dual CLI/PS tables. Structured dispatch resolves them before agent fallback. |
| IV. Extension Contract: IProcessor | ✅ PASS | All new commands implement `IProcessor` (Init + ProcessAsync). No `ICommand` usage. |
| V. Thread-Affinity & Isolation | ✅ PASS | `ProfileService` uses `SemaphoreSlim` for async-safe concurrency. PS surface uses sync-over-async via existing `Channel<T>` pump pattern. |
| VI. Strict Code Quality | ✅ PASS | TreatWarningsAsErrors, Nullable=enable, namespaces mirror folders (`Core.Abstractions`, `Core.Runtime`, `Microsoft.Azure.Cirrus.Extensions.Login`). |
| VII. Test-Driven Verification | ✅ PASS | `StubProfileService` for unit tests. Test naming: `MethodName_ShouldBehavior_WhenCondition`. Coverage via Coverlet. |

**Gate result: PASS — no violations. Proceeding to Phase 0.**

## Project Structure

### Documentation (this feature)

```text
specs/001-profile-management/
├── plan.md              # This file
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output
├── quickstart.md        # Phase 1 output
├── contracts/           # Phase 1 output
│   ├── cli-commands.md
│   └── ps-cmdlets.md
└── tasks.md             # Phase 2 output (/speckit.tasks)
```

### Source Code (repository root)

```text
src/
├── Core/
│   ├── Core.Abstractions/
│   │   ├── IProfileService.cs          # NEW — interface
│   │   ├── IAuthContextService.cs      # EXISTING — preserved as adapter
│   │   └── Models/
│   │       ├── Profile.cs              # NEW — data model
│   │       ├── ProfileStore.cs         # NEW — serialization envelope
│   │       └── AuthContext.cs           # EXISTING — transient record
│   ├── Core.Runtime/
│   │   ├── ProfileService.cs           # NEW — implementation
│   │   └── DIRegisterExtensions.cs     # MODIFIED — add RegisterProfileServices()
│   ├── Core.Authentication/
│   │   └── AuthContextService.cs       # MODIFIED — adapter over IProfileService
│   ├── Core.Engine/
│   │   └── Engine.cs                   # EXISTING — already scaffolded
│   └── Core.DependencyInjection/
│       └── CirrusServiceProvider.cs    # EXISTING — already calls RegisterProfileServices()
├── Extensions/
│   └── Login/
│       ├── ExtensionIndex.cs           # MODIFIED — add profile command entries
│       ├── Processors/
│       │   ├── LoginProcessor.cs       # MODIFIED — add --out-profile, --subscription
│       │   ├── LogoutProcessor.cs      # MODIFIED — add --profile support
│       │   ├── ProfileListProcessor.cs     # NEW
│       │   ├── ProfileShowProcessor.cs     # NEW
│       │   ├── ProfileSwitchProcessor.cs   # NEW
│       │   ├── ProfileModifyProcessor.cs   # NEW
│       │   └── ProfileRemoveProcessor.cs   # NEW
│       └── Login.csproj                # EXISTING
├── CLI/
│   └── Program.cs                      # MODIFIED — extract --profile before dispatch
└── PS/
    └── Login/
        ├── Login.psd1                  # MODIFIED — export new cmdlets
        └── Cmdlets/                    # NEW directory for PS cmdlet classes
            ├── GetCirrusProfileCmdlet.cs
            ├── SwitchCirrusProfileCmdlet.cs
            ├── SetCirrusProfileCmdlet.cs
            └── RemoveCirrusProfileCmdlet.cs

tests/
└── Cli.Tests/
    ├── ProfileServiceTests.cs          # NEW
    ├── ProfileProcessorTests.cs        # NEW
    └── Common/
        └── StubProfileService.cs       # NEW — in-memory test double
```

**Structure Decision**: Follows the existing Cirrus module pattern. Profile business logic lives in `Core.Runtime/ProfileService.cs`. Profile commands are added to the Login extension (the natural home for authentication-related operations). No new projects are created — all changes fit within existing `.csproj` boundaries.

## Complexity Tracking

> No constitution violations — this section is empty.
