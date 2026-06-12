# Spec-Driven Development for Cirrus

## Overview

This document captures the experiment, findings, and recommendations from evaluating spec-driven development (SDD) frameworks for the Cirrus project — the next-generation Azure CLI and Azure PowerShell.

The goal is to determine whether SDD frameworks can improve the quality, predictability, and reviewability of AI-assisted code generation for a production engineering team at Microsoft.

---

## Team & Stakeholders

### Stakeholders
- **Teresa Ritorto**
- **Damien Caro**
- **Ben**

### Identity Squad (Experiment Runners)
- **Yash Patil** — OpenSpec framework evaluation
- **Yeming Liu** — Spec-Kit framework evaluation (collaboration)
- **Ren Silva** — Additional framework evaluation

### Experiment Structure
Each squad member independently implemented the same feature (Profile Management) using a different SDD framework, then compared results in a brown-bag session. A meta-agent (separate AI chat) was used for codebase analysis, spec review, and code critique across both experiments.

---

## What is Spec-Driven Development?

Spec-Driven Development is a structured approach to AI-assisted coding where:

1. **Requirements are captured in structured spec artifacts** before any code is written
2. **AI agents generate implementation plans** from those specs, not from ad-hoc prompts
3. **Verification checks** ensure the code matches the spec
4. **Spec artifacts are committed alongside code** — creating a living record of intent, design decisions, and behavioral contracts

The hypothesis: structured specs produce more predictable, reviewable, and correct AI-generated code than prompt-based "vibe coding."

---

## The Experiment

### Feature: Profile Management

Cirrus currently uses a single-context model — one `AuthContext` with a tenant ID and subscription ID persisted to `context.json`. This works for basic workflows but breaks down for enterprise users managing multiple tenants, subscriptions, and environments.

The Profile Management feature replaces this with named profiles stored in `profiles.json`, supporting:
- Profile CRUD (create, list, show, modify, remove)
- Profile switching (persistent and per-command override)
- Login/logout integration (`--out-profile` on login, `--profile` on logout)
- Engine-level profile resolution with unambiguous precedence

### Input to Both Frameworks

Both frameworks received identical input:
- **PM spec** describing pain points (Azure CLI, AWS CLI, PowerShell context management) and proposed changes
- **Meeting notes** with MVP scope, domain model decisions, functional requirements, auth/token constraints, UX requirements, and demo scenarios
- **10 engineering constraints** identified by the meta-agent after deep codebase analysis:
  1. `--profile` must be engine-level, not per-command
  2. `profiles.json` writes must be atomic (temp file + rename)
  3. Profile defaults must be extensible (dictionary-based)
  4. `IAuthContextService` must have a clear migration/adaptation path
  5. Storage path must use `Environment.SpecialFolder.LocalApplicationData`
  6. Error when no profile exists must include actionable guidance
  7. `--out-profile` collision must overwrite with warning
  8. `logout --profile <name>` must be supported
  9. Profile model must not become a rigid struct (AWS's 10-year mistake)
  10. Storage path must be consistent with existing conventions

### Frameworks Evaluated

| Framework | Type | Philosophy |
|---|---|---|
| **OpenSpec** (Fission AI) | Lightweight, fluid | Actions not phases — edit anything anytime, delta specs for brownfield |
| **Spec-Kit** (GitHub) | Structured, phase-gated | Constitution → specify → clarify → plan → tasks → analyze → implement |
| **Human-Guided** (GitHub) | Fluid | Human interaction with AI |

### Branches

| Branch | Framework | Status |
|---|---|---|
| `experiment/openspec-profile-management` | OpenSpec | Complete |
| `001-profile-management` | Spec-Kit | Complete |
| Common ancestor | `367df360` | Base for diffs |

---

## Process Comparison

### OpenSpec Workflow

1. **Install & Configure** — `openspec init`, created `config.yaml` with Cirrus architecture context and per-artifact rules (Gherkin scenarios for specs, ADRs for design, layer-ordered tasks)
2. **Baseline Spec** — Documented current auth-context behavior in `openspec/specs/auth-context/spec.md` using Given/When/Then scenarios
3. **Propose** — `/opsx:propose profile-management` with PM spec + constraints → generated proposal.md, 4 delta spec files, design.md (8 ADRs), tasks.md (42 tasks)
4. **Implement** — `/opsx:apply` → agent implemented all tasks in one session
5. **Verify** — `/opsx:verify` → found 2 critical issues, 3 warnings
6. **Fix & Iterate** — Fixed issues, stripped unnecessary backward-compat code after human review
7. **Build & Test** — 0 errors, 0 warnings, 45 tests passing

### Spec-Kit Workflow

1. **Install & Configure** — `specify init --here --integration copilot`
2. **Constitution** — `/speckit.constitution` → 7 project principles for Cirrus development
3. **Specify** — `/speckit.specify` with PM spec + constraints → spec.md with 6 user stories, 20 functional requirements
4. **Clarify** — `/speckit.clarify` → 5 targeted questions (migration strategy, file permissions, subscription UX, validate command, concurrency)
5. **Plan** — `/speckit.plan` → plan.md, research.md (8 technical decisions), data-model.md, contracts/ (CLI + PS), quickstart.md
6. **Analyze** — `/speckit.analyze` → found 10 cross-artifact issues before implementation
7. **Tasks** — `/speckit.tasks` → 37 tasks across 9 phases
8. **Implement** — `/speckit.implement` → all tasks executed
9. **Smoke Test** — Standalone test verified all operations against real filesystem

---

## Quantitative Results

### Change Scope

| Metric | OpenSpec | Spec-Kit |
|---|---|---|
| Files changed (src/tests) | 32 | 99 |
| Lines added | 1,636 | 2,900 |
| Lines removed | 62 | 749 |
| Profile-specific files | 32 (100%) | ~16 (16%) |
| Non-profile refactoring | 0 files | ~83 files |
| Framework artifact files | 10 | 45 |

### Test Output

| Metric | OpenSpec | Spec-Kit |
|---|---|---|
| New test files | 5 | 2 |
| New test methods | 34 unit tests | 10 smoke test scenarios |
| Test type | In-memory stubs (StubProfileService) | Real filesystem (ProfileSmokeTest) |
| Build result | 0 errors, 0 warnings | 5 pre-existing build errors (unrelated to profile work) |

### Artifact Output

| Artifact | OpenSpec | Spec-Kit |
|---|---|---|
| Spec format | 4 delta spec files (ADDED/MODIFIED) with Gherkin scenarios | 1 spec.md with user stories and FRs |
| Design decisions | 8 ADRs in design.md | 8 research items in research.md |
| CLI/PS contracts | Implicit in ExtensionIndex code | Explicit contracts/cli-commands.md and contracts/ps-cmdlets.md |
| Quickstart guide | Not produced | quickstart.md with step-by-step workflows |
| Migration plan | 3-phase plan with rollback | Not needed (clarify resolved as greenfield) |
| Cross-artifact check | /opsx:verify found 2 issues post-implementation | /speckit.analyze found 10 issues pre-implementation |

---

## Software Patterns Observed

### 1. Adapter vs Clean Break (Migration Strategy)

OpenSpec's agent assumed backward compatibility was needed — built a full `IAuthContextService` adapter with `context.json` fallback and a 3-phase migration plan. This was later stripped after human review determined it was unnecessary (greenfield product).

Spec-Kit's agent asked upfront via `/speckit.clarify`: "Should we support migration from context.json?" Human answered: "No users, breaking changes are fine." Result: cleaner implementation, no wasted work.

**Takeaway**: Ambiguous specs cause AI agents to default to the conservative (over-engineered) path. Explicit clarification prevents rework.

### 2. Scope Creep vs Scope Discipline

OpenSpec changed only profile-related files (32 files, 100% on-feature).

Spec-Kit changed 99 files, including ~83 unrelated refactoring changes: renamed core interfaces (`ICommandAuthHandler` → `IAuthHandler`), removed `ICommandContext`, created new `InvocationSession` class, restructured PowerShell from script wrappers to compiled C# cmdlets, reorganized folder structure.

**Takeaway**: When told "greenfield, break things," the AI may interpret this as license to refactor broadly. Explicit scope boundaries are essential: "Do NOT modify files outside the feature scope without approval."

### 3. Phase-Gated vs Fluid Workflow

Spec-Kit enforces strict ordering: constitution → specify → clarify → plan → tasks → analyze → implement. Cannot run analyze before tasks. Cannot skip steps.

OpenSpec allows any action at any time. When the agent discovered an MSAL limitation during implementation, it updated design.md inline and continued.

**Takeaway**: Phase gates provide quality checkpoints but prevent iteration. Fluid workflows enable iteration but risk skipping checkpoints. The ideal is fluid with optional gates.

### 4. Pre-Implementation Analysis vs Post-Implementation Verification

Spec-Kit's `/speckit.analyze` ran before implementation — found 10 issues (version mismatch, missing error paths, parameter naming inconsistencies, test omissions). These were fixed in the spec before code existed.

OpenSpec's `/opsx:verify` ran after implementation — found 2 critical issues and 3 warnings. These required code fixes.

**Takeaway**: Pre-implementation analysis catches more and catches cheaper. Post-implementation verification catches different things (actual code behavior vs spec intent). Both are valuable.

### 5. Delta Specs vs Flat Specs

OpenSpec uses ADDED/MODIFIED/REMOVED sections that document what changed relative to existing behavior. Clear audit trail for brownfield development.

Spec-Kit uses flat spec.md with user stories and functional requirements — documents the target state but not the transition.

**Takeaway**: For brownfield/enterprise codebases, delta specs are superior for code review. For greenfield, flat specs are simpler.

### 6. Schema Versioning

Spec-Kit included `SchemaVersion: 1` in `ProfileStore` with forward-compatibility validation. OpenSpec did not include schema versioning.

**Takeaway**: Production concerns like schema versioning surface naturally from research-oriented planning (Spec-Kit's research.md) but are easily missed in implementation-focused workflows.

### 7. File Permissions (Security)

Spec-Kit set `UnixFileMode.UserRead | UserWrite` (0600) on profiles.json. OpenSpec did not address file permissions.

**Takeaway**: Non-functional requirements (security, permissions) are easily missed when specs focus on functional requirements. An explicit clarification step surfaces these.

### 8. Silent UX Decisions

Both agents silently decided command names, parameter names, and edge-case behavior without asking the human. OpenSpec's agent chose `azv2 profile list` and `Get-CirrusProfile` by inferring from repo conventions. Spec-Kit's agent similarly inferred naming.

**Takeaway**: Neither framework enforces human checkpoints for UX decisions. This must be addressed via explicit rules, not framework features.

---

## Core Design — Both Agents Converged

Despite different frameworks, both implementations arrived at the same fundamental architecture:

- `Profile` class with `Dictionary<string, string> Defaults` for extensibility
- `IProfileService` interface in `Core.Abstractions`
- `ProfileService` in `Core.Runtime` with `SemaphoreSlim(1,1)` and atomic writes
- `--profile` stripping in `Program.cs`, injected as `__profile` reserved parameter
- Profile resolution: flag → active → empty fallback
- Profile commands in Login extension
- `AuthContextService` delegates to `IProfileService`
- Same CLI commands: `profile list/show/switch/modify/remove`
- Same PS cmdlets: `Get-CirrusProfile`, `Switch-CirrusProfile`, `Set-CirrusProfile`, `Remove-CirrusProfile`

The framework influenced the process and artifacts, not the architecture.

---

## Recommendations

### For the Cirrus Team

**Use a hybrid approach — take the best of both frameworks:**

| Phase | Recommended Approach | Why |
|---|---|---|
| Project setup | Spec-Kit constitution | Establishes principles once, reused across features |
| Pre-analysis | Human + AI meta-agent | The 10 constraints identified before either framework ran were the highest-value input |
| Spec creation | Either framework | Both produce structured specs; OpenSpec's delta format is better for brownfield |
| **Clarification** | **Spec-Kit's /speckit.clarify** | **Prevents over-engineering and wrong assumptions — this is the single most impactful step** |
| Planning & research | Spec-Kit | Richer artifacts (contracts, quickstart, research with alternatives) |
| Cross-artifact check | Spec-Kit's /speckit.analyze | Catches more issues pre-implementation |
| Implementation | OpenSpec's fluid workflow | No phase gates; can iterate design mid-build |
| Scope control | OpenSpec | Must add rule: "Do NOT modify files outside feature scope" |
| Verification | Both | Pre-implementation (analyze) + post-implementation (verify) |
| Testing | Spec-Kit's approach | Smoke tests against real filesystem, not just in-memory stubs |

### Critical Rules (Regardless of Framework)

1. **Scope guardrail**: "Do NOT modify, rename, or refactor files outside the feature scope without explicit human approval."
2. **Clarify before building**: Resolve migration, security, naming, and edge cases before implementation.
3. **Human reviews UX decisions**: Command names, parameter names, and user-facing messages must be approved — not inferred.
4. **Build incrementally**: Don't implement all tasks in one batch. Group into deployable slices, smoke test after each group.
5. **Schema version from day one**: Include a version field in persisted formats.
6. **Pre-analysis is highest value**: A human or meta-agent reviewing the spec before framework execution catches the most expensive class of issues.

---

## Key Findings

1. **Clarification is the single most impactful step.** It prevented over-engineering, caught security gaps, and resolved UX ambiguity. Only Spec-Kit has this built in.

2. **Scope discipline is non-negotiable.** OpenSpec's 32-file PR is reviewable. Spec-Kit's 99-file PR is not. Scope creep is the biggest risk of AI-assisted development.

3. **Pre-implementation analysis catches more than post-implementation verification.** Spec-Kit found 10 issues before code. OpenSpec found 2 after. Fixing spec-level issues is cheaper.

4. **Both agents converged on the same architecture.** The framework influences process and artifacts, not the core design. The engineering constraints given as input drove both to the correct solution.

5. **Human pre-analysis was the highest-value input.** The 10 constraints identified before either framework ran prevented the most expensive class of bugs in both implementations.

6. **AI agents interpret "greenfield" as license to refactor.** Explicit scope boundaries are essential regardless of framework.

7. **Neither framework enforces UX checkpoints.** Both agents silently decided command names and behavior. This is a gap in all current SDD frameworks.

---

## Appendix: Repository Structure

### OpenSpec Artifacts
```
openspec/
├── config.yaml
├── specs/auth-context/spec.md
└── changes/profile-management/
    ├── proposal.md
    ├── design.md
    ├── tasks.md
    └── specs/ (4 delta spec files)
```

### Spec-Kit Artifacts
```
.specify/
├── memory/constitution.md
├── feature.json, integration.json
├── extensions/git/
├── scripts/
└── templates/

specs/001-profile-management/
├── spec.md
├── plan.md
├── research.md
├── data-model.md
├── tasks.md
├── quickstart.md
├── checklists/requirements.md
└── contracts/
    ├── cli-commands.md
    └── ps-cmdlets.md
```

### New Profile Commands (Both Implementations)

**CLI**:
- `azv2 profile list` — List all profiles, mark active
- `azv2 profile show [--name <name>]` — Show profile details
- `azv2 profile switch --name <name>` — Change active profile
- `azv2 profile modify --name <name> [--set key=value]` — Update profile
- `azv2 profile remove --name <name>` — Delete profile
- `azv2 login --subscription-id <id> [--out-profile <name>]` — Login + create profile
- `azv2 login logout [--profile <name>]` — Logout specific profile
- `azv2 <any-command> --profile <name>` — Per-command profile override

**PowerShell**:
- `Get-CirrusProfile [-Name <name>]`
- `Switch-CirrusProfile -Name <name>`
- `Set-CirrusProfile -Name <name> [-Set key=value]`
- `Remove-CirrusProfile -Name <name>`
- `Connect-CirrusAccount -SubscriptionId <id> [-OutProfile <name>]`
- `Disconnect-CirrusAccount [-Profile <name>]`
