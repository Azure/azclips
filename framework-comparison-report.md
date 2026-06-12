# Comparative Evaluation: OpenSpec vs Spec-Kit for Profile Management

**Author**: Yash Patil  
**Date**: April 29, 2026  
**Feature**: Profile Management — replacing single-context AuthContext with named profiles  
**Repo**: Cirrus (next-gen Azure CLI & Azure PowerShell)  
**AI Tool**: GitHub Copilot (both experiments)  

---

## 1. Experiment Design

### Controlled Variables
- Same feature (Profile Management)
- Same input spec (Document R — PM spec + meeting notes + 10 constraints)
- Same AI tool (GitHub Copilot)
- Same repo (Cirrus)
- Same human reviewer (meta-agent in a separate chat for both)

### Independent Variable
- Framework: OpenSpec (OPSX workflow) vs Spec-Kit (SDD workflow)

### Branches
- OpenSpec: `experiment/openspec-profile-management`
- Spec-Kit: `001-profile-management`
- Common ancestor: `367df360`

---

## 2. Quantitative Comparison

### Change Scope

| Metric | OpenSpec | Spec-Kit |
|---|---|---|
| Files changed (src/tests) | 32 | 99 |
| Lines added | 1,636 | 2,900 |
| Lines removed | 62 | 749 |
| Profile-specific files | 32 (100%) | ~16 (16%) |
| Non-profile refactoring | 0 files | ~83 files |
| Framework artifact files committed | 10 | 45 |
| New test files | 5 (unit tests) | 1 (smoke test) + 1 (stub) |
| Test count (new) | 34 unit tests | 10 smoke test scenarios |

### Workflow Steps

| Phase | OpenSpec | Spec-Kit |
|---|---|---|
| Setup | `openspec init` + `config.yaml` (~30 min) | `specify init` + `/speckit.constitution` (~30 min) |
| Spec creation | `/opsx:propose` (1 step, creates all 4 artifacts) | `/speckit.specify` (creates spec.md) |
| Clarification | **None** — zero questions asked | `/speckit.clarify` — 5 targeted questions |
| Planning | Included in propose (design.md) | `/speckit.plan` (plan.md + research.md + contracts/ + quickstart.md) |
| Analysis | `/opsx:verify` (post-implementation) — 2 issues found | `/speckit.analyze` (pre-implementation) — 10 issues found |
| Task generation | Included in propose (tasks.md) | `/speckit.tasks` (tasks.md) |
| Implementation | `/opsx:apply` | `/speckit.implement` |
| Verification | `/opsx:verify` | Standalone smoke test |

---

## 3. Software Patterns Observed

### Pattern 1: Adapter vs Clean Break (Migration Strategy)

**OpenSpec** built a full `IAuthContextService` adapter that delegated to `IProfileService`, maintained `context.json` backward compatibility, and provided a 3-phase migration plan. This was because the agent assumed backward compatibility was necessary — the original spec said "IAuthContextService becomes obsolete" without explicitly saying "break it."

**Spec-Kit** asked via `/speckit.clarify`: "Should we support migration from context.json?" The human answered: "Greenfield product, no users, breaking changes are fine." So it went straight to profiles.json with no adapter overhead.

**Observed pattern**: When a spec is ambiguous on migration, AI agents default to the conservative path (backward compatibility). An explicit clarification step prevents over-engineering. OpenSpec later stripped the adapter code after human review — but that was rework that could have been avoided.

### Pattern 2: Scope Creep vs Scope Discipline

**OpenSpec** changed only files directly related to the profile feature. 32 files, all profile-specific.

**Spec-Kit** changed 99 files, of which only ~16 were profile-specific. The remaining ~83 files included:
- Renaming `ICommandAuthHandler` → `IAuthHandler`
- Removing `ICommandContext` entirely
- Creating new `IInvocationSession` interface + `InvocationSession.cs` (252 lines)
- Restructuring PowerShell from `.psm1`/`.ps1` script wrappers to compiled C# binary cmdlets (`Cmdlets.Login.csproj`, `Cmdlets.Resources.csproj`)
- Moving `PropertyParser` to new `CLI/ArgumentParsing/` folder
- Adding `Engine.Subscriptions.cs` (140 lines) for subscription selection flow
- Creating provider interfaces (`IArgumentParsingProvider`, `IAssemblyLoadingProvider`)

This scope creep was partly justified (the clarify step determined greenfield → agent felt free to "improve" existing code) but made the PR unreviewable. In a team environment, a 99-file PR for a profile feature would be rejected on principle.

**Observed pattern**: When an AI agent is told "no backward compat needed," it may interpret this as license to refactor broadly. Framework guardrails should distinguish between "don't maintain backward compat for this feature" and "refactor whatever you want."

### Pattern 3: Phase-Gated vs Fluid Workflow

**Spec-Kit** enforces a strict phase order: constitution → specify → clarify → plan → tasks → analyze → implement. You cannot run analyze before tasks exist. You cannot skip steps.

**OpenSpec** is fluid: `/opsx:propose` creates all artifacts at once, you can edit any artifact at any time, `/opsx:apply` can be interrupted and resumed. When the agent discovered an MSAL limitation during implementation, it updated `design.md` inline and continued.

**Observed pattern**: Phase gates provide checkpoints but prevent iteration. Fluid workflows enable iteration but risk skipping important checkpoints. Spec-Kit's rigidity forced analysis before implementation (good). OpenSpec's fluidity allowed mid-implementation design changes (also good). The ideal is fluid with optional gates.

### Pattern 4: Upfront Analysis vs Post-hoc Verification

**Spec-Kit** runs `/speckit.analyze` before implementation — a cross-artifact consistency check. It found 10 issues: .NET version mismatch, misnumbered scenarios, missing error path tasks, parameter naming inconsistencies, test task omissions. These were caught before a single line of code was written.

**OpenSpec** runs `/opsx:verify` after implementation — validating code against spec. It found 2 critical issues (empty AuthContext fallback, PS dispatch table mismatch) and 3 warnings. These required code fixes after implementation.

**Observed pattern**: Pre-implementation analysis catches spec-level issues cheaply. Post-implementation verification catches code-level drift. Both are valuable at different stages. Spec-Kit's analyze was more thorough (10 vs 2 findings) because it ran earlier when artifacts were the only thing to check.

### Pattern 5: Delta Specs vs Flat Specs (Brownfield Documentation)

**OpenSpec** uses ADDED/MODIFIED/REMOVED sections in delta specs. The `auth-context/spec.md` explicitly documented how each existing requirement changed (e.g., "REQ-CTX-01: Single Context Model → MODIFIED: system now derives transient AuthContext from Profile"). This creates a clear audit trail of what existed before and what changed.

**Spec-Kit** uses a flat `spec.md` with user stories and functional requirements — no concept of "before vs after." It documents the target state but not the transition from the existing state.

**Observed pattern**: For brownfield/enterprise codebases, delta specs are superior for code review and audit. For greenfield, flat specs are simpler. Cirrus is technically greenfield but built on patterns from Azure CLI/PS — making delta specs more useful for the team's understanding.

### Pattern 6: Compiled Cmdlets vs Script Wrappers (PS Integration)

**OpenSpec** preserved the existing `.ps1` script wrapper pattern — adding new `.ps1` files in `src/PS/Login/exports/` that construct `CommandInvocation` objects and call `IEngine.Process()`. This matches the existing codebase convention.

**Spec-Kit** rewrote the entire PowerShell integration from `.psm1`/`.ps1` scripts to compiled C# binary cmdlets (`ConnectCirrusAccountCmdlet.cs` etc.) with a new `Cmdlets.Login.csproj` project. This is architecturally better (type-safe, testable, IntelliSense) but was not part of the profile feature request.

**Observed pattern**: AI agents with "greenfield freedom" will optimize beyond the feature scope. Spec-Kit's agent saw an opportunity to modernize PS integration and took it. This is valuable refactoring — but should be a separate PR, not bundled with a feature change.

### Pattern 7: Schema Versioning (Forward Compatibility)

**OpenSpec** did not include a schema version in `profiles.json`.

**Spec-Kit** included `SchemaVersion: 1` in `ProfileStore` with validation: if the file's schema version exceeds the current known version, a clear error is thrown. This prevents silent data loss when downgrading Cirrus.

**Observed pattern**: Schema versioning is a production concern that AI agents may or may not include depending on how the spec is framed. Spec-Kit's research step (R8) explicitly considered versioning strategy. OpenSpec's design step didn't raise it. The `/speckit.clarify` + `/speckit.plan` research phase naturally surfaces these cross-cutting concerns.

### Pattern 8: File Permissions (Security Posture)

**OpenSpec** did not set file permissions on `profiles.json`.

**Spec-Kit** set `UnixFileMode.UserRead | UserWrite` (0600) on Unix via `.NET 7+ API` and documented that Windows `LocalApplicationData` is user-scoped by default. This came from a clarification question about security.

**Observed pattern**: Security concerns are easily missed when the spec focuses on functional requirements. An explicit clarification step ("How should we handle file permissions?") surfaces these non-functional requirements early.

---

## 4. Artifact Comparison

### OpenSpec Artifacts (10 files in `openspec/`)

```
openspec/
├── config.yaml                                     ← project context + per-artifact rules
├── specs/auth-context/spec.md                      ← baseline (current behavior)
└── changes/profile-management/
    ├── proposal.md                                 ← why + what + impact + rollback
    ├── design.md                                   ← 8 ADRs, migration plan, file changes
    ├── tasks.md                                    ← 42 tasks, 11 groups by layer
    └── specs/
        ├── profile-crud/spec.md                    ← ADDED: model, service, commands
        ├── profile-switching/spec.md               ← ADDED: switch, override, resolution
        ├── login-profile-integration/spec.md       ← ADDED: --out-profile, --profile
        └── auth-context/spec.md                    ← MODIFIED: AuthContext → transient
```

**Strengths**: Delta format, per-domain spec separation, compact  
**Weaknesses**: No contracts document, no quickstart guide, no research document

### Spec-Kit Artifacts (9 files in `specs/` + 45 files in `.specify/`)

```
specs/001-profile-management/
├── spec.md                                         ← 6 user stories, 20 FRs, 7 criteria
├── plan.md                                         ← technical context, constitution check
├── research.md                                     ← 8 technical decisions with alternatives
├── data-model.md                                   ← Profile, ProfileStore, AuthContext
├── tasks.md                                        ← 37 tasks, 9 phases by user story
├── quickstart.md                                   ← CLI + PS step-by-step workflows
├── checklists/requirements.md                      ← requirements validation
└── contracts/
    ├── cli-commands.md                             ← 7 CLI commands with parameter tables
    └── ps-cmdlets.md                               ← 6 PS cmdlets with parameter tables

.specify/
├── memory/constitution.md                          ← 7 project principles
├── feature.json, integration.json, etc.            ← framework config
├── extensions/git/                                 ← auto-commit, branching scripts
├── scripts/                                        ← prerequisite checks, setup
└── templates/                                      ← spec/plan/task templates
```

**Strengths**: Explicit contracts, quickstart guide, research with alternatives, constitution  
**Weaknesses**: Heavy framework footprint (45 files in `.specify/`), rigid structure

---

## 5. Core Implementation Comparison

### Identical Decisions (both agents converged)

Both implementations made the same core design choices:
- `Profile` class with `Dictionary<string, string> Defaults` for extensibility
- `IProfileService` interface in `Core.Abstractions`
- `ProfileService` in `Core.Runtime` with `SemaphoreSlim(1,1)` concurrency
- Atomic writes via temp file + `File.Move(overwrite: true)`
- `--profile` stripping in `Program.cs`, injected as `__profile` reserved key
- Profile resolution: flag → active → empty fallback (both arrived here after iteration)
- Profile commands in Login extension (not a separate extension)
- `AuthContextService` delegates to `IProfileService`
- Same CLI commands: `profile list/show/switch/modify/remove`
- Same PS cmdlets: `Get-CirrusProfile`, `Switch-CirrusProfile`, `Set-CirrusProfile`, `Remove-CirrusProfile`

### Key Differences

| Aspect | OpenSpec | Spec-Kit |
|---|---|---|
| `ProfileStore` | Inline in ProfileService | Separate class with `SchemaVersion` |
| File permissions | Not implemented | `UnixFileMode.UserRead \| UserWrite` on Unix |
| Temp file naming | `profiles.json.tmp.{GUID}` | `profiles.json.tmp` (fixed) |
| `IProfileService` API | 6 methods (Save = upsert) | 7 methods (+UpdateProfileAsync, Save has setActive param) |
| Profile name validation | Setter throws `ArgumentException` | Static `IsValidName()` with `[GeneratedRegex]` |
| Auto-profile name | "default" | "temp" (on adapter path) |
| `context.json` migration | Built then stripped (wasted work) | Never built (clarify resolved upfront) |
| PS integration | `.ps1` script wrappers (existing pattern) | Compiled C# binary cmdlets (new pattern) |
| Subscription selection | Not addressed | Interactive prompt design (from clarify) |
| Error messages | Lists available profiles | Lists available profiles + suggests command |

---

## 6. What Each Framework Solves

### OpenSpec Solves
1. **"What changed?"** — Delta specs make it clear what was added/modified/removed against a baseline. Essential for brownfield code review.
2. **"I need to iterate"** — No phase gates. Edit design mid-implementation. Update specs after discovering constraints.
3. **"I want minimal overhead"** — 10 artifact files, `npm install`, one `config.yaml`. Low ceremony.
4. **"Stay in scope"** — The agent only changed what the spec described. Zero scope creep.

### Spec-Kit Solves
1. **"The spec is ambiguous"** — `/speckit.clarify` forces 5 targeted questions before any code is written. Prevents wrong assumptions.
2. **"Are we consistent?"** — `/speckit.analyze` cross-checks spec, plan, and tasks for contradictions. Catches 5x more issues than OpenSpec's verify.
3. **"How do I use this?"** — Produces `quickstart.md`, `contracts/`, and `data-model.md`. Better onboarding artifacts.
4. **"What did we research?"** — `research.md` documents 8 technical decisions with alternatives considered. More rigorous than OpenSpec's inline ADRs.
5. **"What are our principles?"** — Constitution creates project-wide guardrails that persist across features.

---

## 7. Recommendations for the Azure CLI/PS Team

### Don't Pick One — Use a Hybrid

| Phase | Recommended Tool | Why |
|---|---|---|
| Project setup | Spec-Kit constitution | Establishes principles once, reused across all features |
| Pre-analysis | Human + meta-agent | The 10 issues we found before either framework ran were the highest-value input |
| Spec creation | Either | Both produce structured specs; OpenSpec's delta format is better for brownfield |
| **Clarification** | **Spec-Kit's /speckit.clarify** | **Prevents over-engineering and wrong assumptions** |
| Planning & research | Spec-Kit | Produces richer artifacts (contracts, quickstart, research with alternatives) |
| Cross-artifact check | Spec-Kit's /speckit.analyze | Catches more issues pre-implementation |
| Implementation | OpenSpec's fluid workflow | No phase gates; can iterate design mid-build |
| Scope control | OpenSpec | Must add explicit rule: "Do NOT modify files outside feature scope" |
| Verification | Both | Pre-implementation (analyze) + post-implementation (verify) |
| Testing | Spec-Kit's approach | Smoke tests against real filesystem, not just in-memory stubs |

### Critical Rules for Any Framework

1. **Scope guardrail**: "Do NOT modify, rename, or refactor files outside the feature scope without explicit human approval." This prevents the 83-file scope creep observed with Spec-Kit.
2. **Clarify before building**: Whether via `/speckit.clarify` or a manual checklist, resolve migration, security, naming, and edge case decisions before implementation starts.
3. **Human reviews UX decisions**: Command names, parameter names, and user-facing messages must be explicitly approved — not inferred by the AI from codebase patterns.
4. **Build incrementally**: Don't implement 37 tasks in one batch. Group into deployable slices, build-test after each group, smoke test with the real binary early and often.
5. **Schema version from day one**: Even for MVP, include a version field in persisted formats. It's nearly free and prevents painful migrations later.

### Team Considerations

- **Developers who prefer structure**: Spec-Kit's phase-gated workflow with constitution, clarify, analyze gives clear checkpoints and a "what do I do next" experience.
- **Developers who prefer autonomy**: OpenSpec's fluid workflow lets experienced developers skip what they don't need and iterate freely.
- **Code reviewers**: OpenSpec's smaller, scoped PRs are dramatically easier to review. A 32-file PR focused on one feature vs. a 99-file PR mixing feature + refactoring is not a close call.
- **PMs and stakeholders**: Spec-Kit's quickstart.md and contracts/ folder are better for non-developer communication.
- **Long-term maintenance**: OpenSpec's delta-based archive system creates a living history of spec evolution. Spec-Kit's specs are point-in-time snapshots.

---

## 8. Key Findings Summary

1. **Clarification is the single most impactful step.** It prevented over-engineering (no adapter), caught security gaps (file permissions), and resolved UX ambiguity (subscription selection). Both frameworks should have this; only Spec-Kit does.

2. **Scope discipline is non-negotiable for enterprise teams.** OpenSpec's agent stayed in scope (32 files). Spec-Kit's agent refactored 83 unrelated files. This is the difference between a reviewable PR and a rejected PR.

3. **Pre-implementation analysis catches more than post-implementation verification.** Spec-Kit found 10 issues before code was written. OpenSpec found 2 after. Fixing spec-level issues is cheaper than fixing code-level issues.

4. **Both agents converged on the same core design.** Despite different frameworks, the fundamental architecture (Profile model, IProfileService, atomic writes, SemaphoreSlim, engine-level --profile, adapter pattern) was identical. The framework influences artifacts and process, not architecture.

5. **The original human pre-analysis was the highest-value input.** The 10 constraints identified before either framework ran drove both agents to the correct design. Without those constraints, both would have made the same mistakes the original spec contained.

6. **AI agents interpret "greenfield" as "license to refactor."** When told "no backward compat needed," Spec-Kit's agent refactored core infrastructure beyond the feature scope. Explicit scope boundaries are essential regardless of framework.

7. **Neither framework enforces human checkpoints for UX decisions.** Both agents silently chose command names, parameter names, and behavior without asking. This must be addressed via rules/config, not framework features.
