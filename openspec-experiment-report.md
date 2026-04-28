# OpenSpec Experiment Report — Profile Management for Cirrus

**Author**: Yash Patil  
**Date**: April 27, 2026  
**Framework**: OpenSpec (OPSX workflow)  
**Feature**: Profile Management — replacing single-context `AuthContext` with named profiles  
**Repo**: Cirrus (next-gen Azure CLI & Azure PowerShell)  
**AI Tool**: GitHub Copilot  

---

## 1. Experiment Overview

### Goal
Evaluate OpenSpec as a spec-driven development framework for AI-assisted coding. Compare against Spec-kit and other frameworks on the same feature (Profile Management) built by different squad members.

### Process
1. **Pre-analysis**: Before any OpenSpec usage, performed deep codebase analysis and identified 10 design issues in the original PM spec
2. **Setup**: `openspec init` → configured `config.yaml` with Cirrus-specific context and per-artifact rules
3. **Baseline**: Created baseline spec for current auth-context behavior
4. **Propose**: `/opsx:propose profile-management` with original spec + 10 constraints
5. **Apply**: `/opsx:apply` — agent implemented all tasks
6. **Verify**: `/opsx:verify` — caught 2 issues (1 matched manual review)
7. **Review**: Manual design review by meta-agent (separate chat)

### Roles
- **Meta-agent (this chat)**: Codebase analysis, spec critique, config authoring, code review
- **Coding agent (separate chat)**: OpenSpec workflow execution, baseline creation, implementation
- **Human (Yash)**: Orchestration between agents, decision-making, experiment evaluation

---

## 2. Input: What the AI Received

### 2.1 Original PM Spec (from colleague)
The starting spec described the "Why" (pain points of Azure CLI, AWS CLI, PowerShell context management) and "What Changes" (Profile model, IProfileService, login/logout integration, profile commands, engine-level --profile override). It was a product-level spec, not an engineering spec — it lacked concurrency strategy, storage semantics, migration path, and DI integration details.

### 2.2 Pre-Implementation Analysis: 10 Issues Found

Before any code was written, the meta-agent identified these issues in the original spec:

| # | Issue | Severity | Resolution |
|---|---|---|---|
| 1 | `--profile` proposed as per-command parameter | High | Constrained to engine-level |
| 2 | No concurrency protection on `profiles.json` | High | Required atomic writes + SemaphoreSlim |
| 3 | Profile model under-specified / potential god object | Medium | Required extensible `Dictionary<string,string>` defaults |
| 4 | `--out-profile` collision behavior undefined | Medium | Required overwrite with warning |
| 5 | AuthContext "retained as transient" but leaks via service locator | Medium | Required acknowledgment via ADR |
| 6 | IAuthContextService "becomes obsolete" with no migration path | High | Required adapter pattern |
| 7 | Profile resolution error UX gap (first-time users) | Medium | Required actionable error messages |
| 8 | `logout` doesn't support `--profile` | Low | Required `--profile` on logout |
| 9 | `profile validate` semantics undefined | Low | Deferred — not in scope |
| 10 | Storage path inconsistency (LocalApplicationData vs $HOME) | Medium | Required matching existing convention |

### 2.3 Constraints Given to Coding Agent

These were provided alongside the original spec when running `/opsx:propose`:

- `--profile` MUST be handled at engine level (CLIEngine/PSEngine), NOT per-command
- `profiles.json` writes MUST be atomic (write-to-temp + rename)
- Profile defaults MUST be extensible (dictionary-based, not fixed properties)
- `IAuthContextService` MUST be preserved as an internal adapter during migration
- Storage path MUST use `Environment.SpecialFolder.LocalApplicationData` (matching existing convention)
- Error when no profile exists MUST include actionable guidance
- `--out-profile` collision MUST overwrite with warning
- `logout --profile <name>` MUST be supported

### 2.4 OpenSpec config.yaml (Full)

This was injected into every artifact generation prompt:

```yaml
schema: spec-driven

context: |
  # Project
  Cirrus is the next-generation Azure CLI and Azure PowerShell, built on .NET 9 (net9, SDK 9.0.308).
  It serves two client surfaces from a shared codebase: a native CLI binary and PowerShell modules.

  # Architecture
  - Bootstrapper DI pattern: CirrusServiceProvider (static, thread-safe, double-checked lock)
  - All services registered as Singletons (CLI is short-lived, PS reuses across invocations)
  - Orchestrator pattern: tries structured command dispatch first, falls back to AI agent on any exception
  - Extension model: IExtensionIndex maps command names → CommandMetadata per ClientType (CLI/PS)
  - Commands implement IProcessor (Init(ICommandContext) + ProcessAsync()), NOT the legacy ICommand
  - ICommandContext provides per-invocation isolated auth, output, telemetry, and HTTP pipeline
  - HttpPipeline is a middleware chain: Auth → RetryError → Retry429 → HttpClient
  - PowerShell has strict thread-affinity: IEngine.Process() is synchronous, uses Channel<T> pump

  # Extension System
  - CLI: per-extension AssemblyLoadContext isolation
  - PowerShell: shared default ALC (first-loaded version wins)
  - Extensions discovered from artifacts/{Config}/ (builtin) or ~/.cirrus/extensions/ (user-installed)
  - Each extension has ExtensionIndex with dual command tables: CLI (kebab-case) + PS (Verb-CirrusNoun)

  # Persisted State
  - Auth context: {LocalApplicationData}/.cirrus/context.json (via AuthContextService)
  - MSAL token cache: ~/.cirrus/MSAL.cache.plaintext (DEBUG only)
  - User extensions: ~/.cirrus/extensions/{PackageId}/{Version}/

  # Coding Conventions
  - C# with 4-space indent, UTF-8, Nullable=enable, ImplicitUsings=enable
  - TreatWarningsAsErrors=true, EnforceCodeStyleInBuild=true
  - Namespaces mirror folders: Core.Abstractions, Core.Runtime, Core.Engine, etc.
  - Extensions use namespace: Microsoft.Azure.Cirrus.Extensions.{Name}
  - PascalCase types/methods/properties, camelCase locals, I-prefix interfaces
  - DI registration via extension methods: services.Register*Services()

  # Testing
  - xUnit 2.9.3, [Fact] and [Theory], naming: MethodName_ShouldBehavior_WhenCondition
  - No mock framework; direct instantiation for unit tests
  - Coverage via Coverlet: dotnet test --collect:"XPlat Code Coverage"

  # Key Constraints (from architecture specs)
  - Bootstrapper pattern MUST be preserved — no manual instantiation in Program.cs
  - Service modules follow: Business/ (shared logic), Commands/ (CLI), Cmdlets/ (PowerShell)
  - IProcessor is the modern contract; ICommand is legacy (proposed for removal)
  - PowerShell cmdlets use $PSCmdlet.WriteObject on runspace thread — no async output

  # Build & Run
  - Build: dotnet build cirrus.slnx
  - Test: dotnet test tests/Cli.Tests/Cli.Tests.csproj

rules:
  proposal:
    - Reference the existing Cirrus types being modified by fully-qualified namespace
    - Identify migration path from old to new (adapter pattern for backward compatibility)
    - List all affected surfaces: Core libs, CLI commands, PowerShell cmdlets, DI registrations, tests
    - Include a rollback strategy
    - Scope must distinguish between what changes in Core vs Extensions vs PS modules
  specs:
    - Use Given/When/Then (Gherkin) format for ALL scenarios
    - Use RFC 2119 keywords (MUST/SHALL/SHOULD/MAY) for requirement strength
    - Include Priority (P0-P3) and Rationale for each requirement
    - Cover both CLI and PowerShell surfaces
    - Include concurrency, error, and edge-case scenarios — not just happy path
    - Each scenario SHOULD map to a testable assertion (MethodName_ShouldBehavior_WhenCondition)
    - Reference affected interfaces by name
  design:
    - Include Architecture Decision Records (ADR) for non-obvious choices
    - Reference existing Cirrus types by fully-qualified name
    - Address both CLI and PowerShell code paths — different threading models
    - Address the service locator pattern — new services must justify usage
    - Show how new services integrate with DI registration chain
    - Include a File Changes section listing new/modified files with paths
    - Address thread safety for shared mutable state
  tasks:
    - Tasks must be small enough for a single commit
    - Group by layer: Model/Abstractions → Service/Runtime → Engine integration → CLI Commands → PS Cmdlets → Tests
    - Each task must reference the spec requirement it satisfies
    - Include explicit test tasks
    - DI registration changes must be a separate task
    - PowerShell module manifest updates must be a separate task
```

---

## 3. What OpenSpec Produced

### 3.1 Artifacts Generated

| Artifact | Description | Size |
|---|---|---|
| `proposal.md` | Why, what changes, capabilities, impact, rollback strategy | ~150 lines |
| `specs/profile-crud/spec.md` | Profile model, IProfileService, persistence requirements | ~120 lines, 6 scenarios |
| `specs/profile-switching/spec.md` | Switch command, per-command override, resolution order | ~120 lines, 10 scenarios |
| `specs/login-profile-integration/spec.md` | Login --out-profile, logout --profile, extension registration | ~120 lines, 10 scenarios |
| `specs/auth-context/spec.md` | MODIFIED requirements for AuthContext → Profile transition | ~120 lines, 8 scenarios |
| `design.md` | 8 ADRs, migration plan, risks, file changes table | ~200 lines |
| `tasks.md` | 42 tasks across 11 groups, all checked off | ~100 lines |

### 3.2 Architecture Decision Records (from design.md)

| # | Decision | Rationale |
|---|---|---|
| ADR-1 | Profile as class with `Dictionary<string,string> Defaults` | Avoids AWS's 10-year `endpoint_url` problem — extensible without model changes |
| ADR-2 | Single `profiles.json` with active pointer | Simplifies atomic writes (one file rename vs directory coordination) |
| ADR-3 | `--profile` at engine level, not per-command | DRY — engine owns context creation, hundreds of commands shouldn't duplicate |
| ADR-4 | `IAuthContextService` preserved as adapter | Incremental migration — old code keeps working |
| ADR-5 | Resolution: `--profile` flag → active profile → error | No silent overrides (AWS CLI's #1 pain point was confusing precedence) |
| ADR-6 | Bare login creates "default" profile | Backward compatible — `azv2 login` works identically to before |
| ADR-7 | ProfileService in Core.Runtime, not new project | One service doesn't justify a new csproj |
| ADR-8 | Service locator for new commands | Matches existing pattern — fix globally later, not per-feature |

### 3.3 Key Design: Profile Resolution Flow

```
User runs: azv2 group list --profile prod

Program.cs: StripProfileFlag(args) → extracts "prod", removes --profile from args
    ↓
ExtensionDispatcher.Parse(strippedArgs) → normal command parsing
    ↓
invocation.Parameters["__profile"] = "prod"   (reserved key)
    ↓
Engine.Process() → reads __profile, removes from params
    ↓
Engine.CreateCommandContext(profileOverride: "prod")
    ↓
IProfileService.GetProfileAsync("prod") → Profile object
    ↓
Derive transient AuthContext(TenantId, SubscriptionId)
    ↓
CommandAuthHandler.AuthContext = derived context
    ↓
Command executes with prod's tenant/subscription — active profile unchanged
```

### 3.4 Key Design: Atomic Write Pattern

```csharp
// ProfileService.SaveStoreAsync()
var tempFile = Path.Combine(dir, $"profiles.json.tmp.{Guid.NewGuid():N}");
await File.WriteAllTextAsync(tempFile, json, ct);
File.Move(tempFile, ProfileFilePath, overwrite: true);  // atomic on all platforms
```

- `SemaphoreSlim(1,1)` serializes in-process writes
- Temp file uses GUID to avoid collisions
- If crash during write, `profiles.json` remains in previous valid state
- Cleanup of temp file on exception

### 3.5 Key Design: AuthContextService Adapter

```
Before (single-context):
  LoginCommand → IAuthContextService.SaveContextAsync → context.json
  Engine → IAuthContextService.GetCurrentContextAsync → context.json → AuthContext

After (profile-based):
  LoginCommand → IProfileService.SaveProfileAsync → profiles.json
                → IAuthContextService.SaveContextAsync → profiles.json + context.json (compat)
  Engine → IProfileService.GetActiveProfileAsync → profiles.json → derive AuthContext

  Old code using IAuthContextService still works:
    GetCurrentContextAsync → delegates to IProfileService → derives AuthContext
    SaveContextAsync → updates active profile + writes context.json
    ClearContextAsync → deletes context.json only, preserves profile definitions
```

---

## 4. Implementation Output

### 4.1 Code Statistics

| Metric | Value |
|---|---|
| New files | 16 |
| Modified files | 10 |
| Total new code | ~1000 lines |
| Tests before | 11 |
| Tests after | 45 |
| New test files | 5 |

### 4.2 New Files Created

| File | Purpose |
|---|---|
| `src/Core/Core.Abstractions/Models/Profile.cs` | Profile class with validation, extensible Defaults |
| `src/Core/Core.Abstractions/IProfileService.cs` | 6-method service interface |
| `src/Core/Core.Runtime/ProfileService.cs` | Implementation: atomic writes, SemaphoreSlim, migration |
| `src/Extensions/Login/Commands/ListProfiles.cs` | List all profiles with active marker |
| `src/Extensions/Login/Commands/ShowProfile.cs` | Show profile details (defaults to active) |
| `src/Extensions/Login/Commands/SwitchProfile.cs` | Change active profile |
| `src/Extensions/Login/Commands/ModifyProfile.cs` | Update properties, --set for defaults |
| `src/Extensions/Login/Commands/RemoveProfile.cs` | Delete profile with confirmation |
| `src/PS/Login/exports/Get-CirrusProfile.ps1` | PS list/show wrapper |
| `src/PS/Login/exports/Switch-CirrusProfile.ps1` | PS switch wrapper |
| `src/PS/Login/exports/Set-CirrusProfile.ps1` | PS modify wrapper |
| `src/PS/Login/exports/Remove-CirrusProfile.ps1` | PS remove wrapper |
| `tests/Cli.Tests/ProfileServiceTests.cs` | 9 unit tests |
| `tests/Cli.Tests/ProfileModelTests.cs` | 5 unit tests |
| `tests/Cli.Tests/ProfileCommandTests.cs` | 10 unit tests |
| `tests/Cli.Tests/LoginProfileIntegrationTests.cs` | 4 integration tests |
| `tests/Cli.Tests/EngineProfileResolutionTests.cs` | 5 unit tests |

### 4.3 Modified Files

| File | Change |
|---|---|
| `src/Core/Core.Authentication/AuthContextService.cs` | Refactored to adapter over IProfileService |
| `src/Core/Core.Engine/Engine.cs` | CreateCommandContext resolves profile → AuthContext |
| `src/CLI/Program.cs` | StripProfileFlag extracts --profile before parsing |
| `src/Extensions/Login/Commands/Login.cs` | Added --out-profile parameter |
| `src/Extensions/Login/Commands/Logout.cs` | Added --profile parameter |
| `src/Extensions/Login/ExtensionIndex.cs` | Added all profile command entries (CLI + PS) |
| `src/CLI/Parser/CommandGroupIndex.cs` | Added Profile → Login routing |
| `src/PS/Login/Login.psd1` | Added 4 new cmdlets to FunctionsToExport |
| `src/PS/Login/exports/Connect-CirrusAccount.ps1` | Added -OutProfile parameter |
| `src/PS/Login/exports/Disconnect-CirrusAccount.ps1` | Added -Profile parameter |

### 4.4 New Commands

**CLI:**
| Command | Example |
|---|---|
| `azv2 profile list` | Lists all profiles, marks active with `*` |
| `azv2 profile show` | Shows active profile details |
| `azv2 profile show --name prod` | Shows specific profile |
| `azv2 profile switch --name prod` | Changes active profile |
| `azv2 profile modify --name prod --set location=eastus2` | Sets a default |
| `azv2 profile remove --name staging` | Deletes a profile |
| `azv2 login --subscription-id s1 --out-profile dev` | Login + create named profile |
| `azv2 login logout --profile prod` | Logout specific profile |
| `azv2 group list --profile prod` | Run command as different profile |

**PowerShell:**
| Cmdlet | Example |
|---|---|
| `Get-CirrusProfile` | Lists all profiles |
| `Get-CirrusProfile -Name prod` | Shows specific profile |
| `Switch-CirrusProfile -Name prod` | Changes active profile |
| `Set-CirrusProfile -Name prod -Set location=eastus2` | Sets a default |
| `Remove-CirrusProfile -Name staging` | Deletes a profile |
| `Connect-CirrusAccount -SubscriptionId s1 -OutProfile dev` | Login + named profile |
| `Disconnect-CirrusAccount -Profile prod` | Logout specific profile |

### 4.5 Storage Format

`{LocalApplicationData}/.cirrus/profiles.json`:
```json
{
  "activeProfile": "dev",
  "profiles": {
    "dev": {
      "name": "dev",
      "tenantId": "12345-...",
      "subscriptionId": "67890-...",
      "cloudName": "AzureCloud",
      "authMethod": "interactive",
      "userPrincipalName": "user@example.com",
      "clientId": null,
      "defaults": {
        "location": "eastus2",
        "resource-group": "dev-rg"
      }
    },
    "prod": {
      "name": "prod",
      "tenantId": "abcde-...",
      "subscriptionId": "fghij-...",
      "cloudName": "AzureCloud",
      "authMethod": "service-principal",
      "defaults": {}
    }
  }
}
```

---

## 5. Quality Assessment

### 5.1 Spec-Code Alignment (10 pre-identified issues)

| # | Issue | Addressed in Spec? | Addressed in Code? | Notes |
|---|---|---|---|---|
| 1 | `--profile` per-command | Yes (ADR-3) | Yes | StripProfileFlag in Program.cs + __profile reserved param |
| 2 | Concurrency on profiles.json | Yes (spec scenario) | Yes | SemaphoreSlim(1,1) + atomic temp+move |
| 3 | Profile model inflexible | Yes (ADR-1) | Yes | Dictionary<string,string> Defaults |
| 4 | --out-profile collision | Yes (spec scenario) | Yes | Overwrites with warning message |
| 5 | Service locator leak | Yes (ADR-8) | Yes | Justified, deferred to global fix |
| 6 | No migration path | Yes (ADR-4) | Yes | AuthContextService as adapter |
| 7 | Error UX gap | Yes (spec scenario) | Partial | Engine fallback uses empty AuthContext instead of throwing |
| 8 | logout --profile | Yes (spec scenario) | Yes | Implemented |
| 9 | validate semantics | N/A | N/A | Correctly deferred — not in scope |
| 10 | Storage path inconsistency | Yes (design) | Yes | Uses LocalApplicationData consistently |

### 5.2 Spec-Code Drift

**Overall drift: Low (1 behavioral mismatch out of 30+ scenarios)**

**Issue found**: In `Engine.CreateCommandContext()`, when no profile exists and no `--profile` flag is provided, the code falls back to empty `AuthContext(TenantId: "", SubscriptionId: "")` instead of throwing `InvalidOperationException` with actionable guidance as the spec requires. This means commands proceed with blank credentials and fail later with confusing ARM errors.

**Caught by**: Both manual review AND `/opsx:verify`.

### 5.3 Silent Decisions (not in spec, decided by AI)

1. **Command names**: `azv2 profile list/show/switch/modify/remove` — inferred from repo conventions, never asked human
2. **PowerShell verb choices**: `Get-`, `Switch-`, `Set-`, `Remove-` — followed PowerShell conventions
3. **`Get-CirrusProfile` dual-mode**: Single cmdlet does both list (no args) and show (-Name), rather than two separate cmdlets
4. **`RemoveProfile` has `Yes` parameter**: But no confirmation prompt is implemented (dead code)
5. **Auto-profile name is "default"**: Original spec said "temp" — agent changed it (with ADR-6 justification)

### 5.4 Test Quality

| Category | Tests | Coverage |
|---|---|---|
| Profile model validation | 5 | Constructor, invalid names, serialization, ToString |
| ProfileService CRUD | 9 | Save, update, remove, active, case-insensitive, empty state |
| Profile commands | 10 | List, show, switch, modify, remove — happy + error paths |
| Login/logout integration | 4 | --out-profile, default profile, collision, logout preserves profile |
| Engine resolution | 5 | Override, active fallback, no-profile error, persistence check |

**Limitation**: All tests use `StubProfileService` (in-memory). No integration tests verify real filesystem operations (atomic writes, migration from context.json, SemaphoreSlim under I/O).

---

## 6. OpenSpec Framework Evaluation

### 6.1 Strengths

1. **Fluid workflow**: No phase gates. When MSAL limitation was discovered during `/opsx:apply`, agent updated `design.md` and continued. No ceremony.
2. **Delta specs**: Forced clear ADDED/MODIFIED/REMOVED documentation for the AuthContext → Profile transition. Excellent for brownfield.
3. **Scenario → test traceability**: Every spec scenario has a `Test mapping:` line that matches actual test names. Verifiable mapping.
4. **config.yaml injection**: Project context and per-artifact rules were injected into every prompt. The rules for "ADRs in design" and "Gherkin in specs" were followed consistently.
5. **`/opsx:verify`**: Caught the same Issue #7 that manual review found. Real validation.
6. **ADR generation**: 8 architecture decisions, each with rationale and alternatives considered. This was driven by the design rules in config.yaml.
7. **Migration plan**: The design doc included a 3-phase migration plan with rollback strategy — directly from the proposal rules.

### 6.2 Weaknesses

1. **No human checkpoints for UX decisions**: Agent made all command naming, parameter naming, and behavioral decisions silently. No mechanism to force "ask before deciding."
2. **Verify only in expanded mode**: Default `core` profile only has propose/apply/archive. Verify requires `openspec config profile` + `openspec update`. This should be default.
3. **No spec completeness gate**: Nothing prevents `/opsx:apply` from running when specs are vague. The agent fills gaps silently during implementation.
4. **Tests are only unit-level**: The framework's scenario format maps well to test cases, but it didn't push the agent to write integration tests.
5. **Context window management**: With 4 spec files + design + tasks + implementation, context can get large. No explicit guidance on context hygiene.

### 6.3 Key Finding

**The spec's precision determines where the AI makes silent decisions vs. follows instructions.**

- Precise constraint ("atomic writes with temp+move") → code matches exactly
- Vague requirement ("add profile commands") → AI decides naming, parameters, behavior
- Missing requirement (no mention of `validate` command) → correctly skipped

The framework amplifies spec quality — it doesn't compensate for spec gaps.

---

## 7. Comparison Criteria

For comparing OpenSpec vs Spec-kit vs other frameworks on the same feature:

| Metric | How to Measure | Our Result |
|---|---|---|
| **Spec-code drift** | Count silent decisions not reflected in spec artifacts | 5 silent decisions, 1 behavioral mismatch |
| **Issue detection rate** | How many of the 10 pre-identified issues were addressed? | 9/10 (validate deferred by design) |
| **Artifact quality** | Do artifacts explain why + what + how? Are ADRs present? | 8 ADRs, 30+ Gherkin scenarios, migration plan |
| **Iteration cost** | How much ceremony when design changed mid-implementation? | Zero — fluid, just edited the artifact |
| **Traceability** | Can a new person understand the change from artifacts alone? | Yes — proposal → specs → design → tasks chain is clear |
| **Test generation** | Did spec scenarios map to tests? | Direct mapping via `Test mapping:` annotations |
| **Time to passing tests** | From spec input to `dotnet test` green | Single session (~2 hours wall clock) |
| **Human interventions** | How many times did human correct the AI? | 1 (the Issue #7 fix) |
| **Framework setup cost** | Time to install + configure + learn | ~30 minutes (npm install + config.yaml + reading docs) |
| **Code quality** | Correct patterns? Thread-safe? Follows conventions? | High — matched all repo conventions |

---

## 8. Recommendations

### For the Team
1. **Adopt spec-driven development for non-trivial changes** — the artifact trail is valuable
2. **Invest in config.yaml quality** — it's the highest-leverage input to AI code generation
3. **Add a "human checkpoint" rule** — force agent to list UX decisions and wait for approval before implementation
4. **Always do pre-analysis** — the 10 constraints we identified before coding prevented the most expensive class of bugs

### For OpenSpec
1. Make `/opsx:verify` available in the default `core` profile
2. Add a "decision checkpoint" mechanism — agent lists decisions it made and asks for confirmation
3. Support integration test generation from scenarios, not just unit tests
4. Add a spec completeness score before allowing `/opsx:apply`

---

## Appendix: File Tree (OpenSpec Artifacts)

```
openspec/
├── config.yaml                                    ← project context + rules
├── specs/                                         ← baseline (empty — we didn't archive yet)
└── changes/
    └── profile-management/
        ├── .openspec.yaml                         ← change metadata
        ├── proposal.md                            ← why + what + impact + rollback
        ├── design.md                              ← 8 ADRs + migration plan + file changes
        ├── tasks.md                               ← 42 tasks, all checked
        └── specs/
            ├── profile-crud/spec.md               ← ADDED: Profile model, IProfileService, persistence
            ├── profile-switching/spec.md           ← ADDED: switch, --profile override, resolution order
            ├── login-profile-integration/spec.md   ← ADDED: --out-profile, --profile on logout
            └── auth-context/spec.md               ← MODIFIED: AuthContext → transient, adapter pattern
```
