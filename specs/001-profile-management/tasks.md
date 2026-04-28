# Tasks: Profile Management

**Input**: Design documents from `/specs/001-profile-management/`
**Prerequisites**: plan.md (required), spec.md (required for user stories), research.md, data-model.md, contracts/

**Tests**: Not explicitly requested in the feature specification. Test tasks are omitted.

**Organization**: Tasks are grouped by user story to enable independent implementation and testing of each story.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g., US1, US2, US3)
- Include exact file paths in descriptions

## Path Conventions

- Paths follow the existing Cirrus module structure per plan.md
- Core libraries: `src/Core/{Module}/`
- Login extension: `src/Extensions/Login/`
- CLI entry point: `src/CLI/`
- PS module: `src/PS/Login/`
- Tests: `tests/Cli.Tests/`

---

## Phase 1: Setup

**Purpose**: No new projects needed — all changes fit within existing `.csproj` boundaries. Setup is limited to creating new directories.

- [x] T001 Create `tests/Cli.Tests/Common/` directory for shared test utilities

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Core models, interface, service implementation, and DI wiring that ALL user stories depend on. No user story work can begin until this phase is complete.

**⚠️ CRITICAL**: This phase delivers IProfileService, Profile model, ProfileStore, atomic persistence, and DI registration.

- [x] T002 Create Profile model class in `src/Core/Core.Abstractions/Models/Profile.cs` with all fields from data-model.md (Name, TenantId, SubscriptionId, CloudName, AuthMethod, UserPrincipalName, ClientId, Defaults dictionary) and name validation regex `[a-zA-Z0-9_-]+`
- [x] T003 [P] Create ProfileStore serialization envelope class in `src/Core/Core.Abstractions/Models/ProfileStore.cs` with SchemaVersion (int, default 1), ActiveProfile (string?), and Profiles (Dictionary<string, Profile>)
- [x] T004 Create IProfileService interface in `src/Core/Core.Abstractions/IProfileService.cs` with methods: GetAllProfilesAsync, GetProfileAsync, GetActiveProfileAsync, SetActiveProfileAsync, SaveProfileAsync, UpdateProfileAsync, DeleteProfileAsync
- [x] T005 Implement ProfileService in `src/Core/Core.Runtime/ProfileService.cs` — full CRUD, atomic file writes (write-to-temp + File.Move per R1), SemaphoreSlim(1,1) concurrency (per R3), in-memory caching, owner-only file permissions on Unix via File.SetUnixFileMode (per R2), schema version validation on load (per R8), actionable error messages on corruption/version mismatch (FR-014, FR-015)
- [x] T006 Add RegisterProfileServices() extension method in `src/Core/Core.Runtime/DIRegisterExtensions.cs` registering IProfileService → ProfileService as Singleton
- [x] T007 Verify CirrusServiceProvider.Initialize() in `src/Core/Core.DependencyInjection/CirrusServiceProvider.cs` already calls RegisterProfileServices() — confirm wiring is active
- [x] T008 [P] Create StubProfileService (in-memory test double) in `tests/Cli.Tests/Common/StubProfileService.cs` implementing IProfileService with Dictionary-backed storage for unit tests

**Checkpoint**: Foundation ready — IProfileService is injectable, profiles persist atomically, DI is wired. User story implementation can now begin.

---

## Phase 3: User Story 1 — Create and Use a Named Profile (Priority: P1) 🎯 MVP

**Goal**: Users can login with `--out-profile <name>` to create a named profile, and subsequent commands automatically use the active profile's identity context.

**Independent Test**: Run `login --out-profile dev`, then run any command and verify it uses the `dev` profile's tenant and subscription.

### Implementation for User Story 1

- [x] T009 [US1] Modify LoginProcessor in `src/Extensions/Login/Processors/LoginProcessor.cs` — add `OutProfile` (string) and `Subscription` (string) properties with ArgumentInfo attributes; after successful auth, call IProfileService.SaveProfileAsync to create/overwrite the named profile (or `temp` if no --out-profile); emit warning on overwrite (FR-008); handle subscription selection: auto-select if one, prompt if multiple, use --subscription if provided (FR-018)
- [x] T010 [US1] Add `login` CLI command metadata entry with `--out-profile` and `--subscription` parameters to CLI dispatch table in `src/Extensions/Login/ExtensionIndex.cs`
- [x] T011 [US1] Add `login` PS command metadata entry with `-OutProfile` and `-SubscriptionId` parameters to PS dispatch table in `src/Extensions/Login/ExtensionIndex.cs`
- [x] T012 [US1] Modify AuthContextService in `src/Core/Core.Authentication/AuthContextService.cs` to delegate to IProfileService internally — GetCurrentContextAsync reads active profile and derives AuthContext, SaveContextAsync creates/overwrites temp profile, ClearContextAsync clears active profile token (per R5, FR-016)
- [x] T013 [US1] Create ProfileShowProcessor in `src/Extensions/Login/Processors/ProfileShowProcessor.cs` implementing IProcessor — resolves profile by name or active profile, formats and outputs all fields including defaults (FR-004)
- [x] T014 [US1] Add `profile show` CLI command metadata entry (positional name parameter, optional) and `Get-CirrusAccount` PS entry update in `src/Extensions/Login/ExtensionIndex.cs`

**Checkpoint**: At this point, `login --out-profile <name>` creates a persisted profile, `profile show` displays it, and Engine.CreateCommandContext() resolves the active profile for command execution.

---

## Phase 4: User Story 2 — Switch Between Named Profiles (Priority: P2)

**Goal**: Users can switch the active profile with `profile switch <name>`, changing the identity context for subsequent commands without re-authenticating (unless token is expired).

**Independent Test**: Create two profiles, switch between them with `profile switch`, verify each subsequent command uses the correct tenant context.

### Implementation for User Story 2

- [x] T015 [US2] Create ProfileSwitchProcessor in `src/Extensions/Login/Processors/ProfileSwitchProcessor.cs` implementing IProcessor — validates profile exists, calls IProfileService.SetActiveProfileAsync, prints confirmation message with resulting active profile name (FR-006, FR-020), error with available profiles list if not found (FR-015)
- [x] T016 [US2] Add `profile switch` CLI command metadata entry (required name parameter) and `Switch-CirrusProfile` PS entry in `src/Extensions/Login/ExtensionIndex.cs`
- [x] T017 [P] [US2] Create SwitchCirrusProfileCmdlet PS cmdlet class in `src/PS/Login/Cmdlets/SwitchCirrusProfileCmdlet.cs` — wraps profile switch via IEngine dispatch

**Checkpoint**: Users can switch between profiles. Combined with US1, this delivers the core demo flow: login → create profile → switch → execute commands.

---

## Phase 5: User Story 3 — List and Inspect Profiles (Priority: P3)

**Goal**: Users can list all profiles and see which one is active, improving discoverability and reducing confusion.

**Independent Test**: Create two profiles, run `profile list`, verify output includes both profiles with active marker.

### Implementation for User Story 3

- [x] T018 [US3] Create ProfileListProcessor in `src/Extensions/Login/Processors/ProfileListProcessor.cs` implementing IProcessor — calls IProfileService.GetAllProfilesAsync, formats table output with `*` marker for active profile, shows guidance message when no profiles exist (FR-004, FR-015)
- [x] T019 [US3] Add `profile list` CLI command metadata entry and `Get-CirrusProfile -List` PS entry in `src/Extensions/Login/ExtensionIndex.cs`
- [x] T020 [P] [US3] Create GetCirrusProfileCmdlet PS cmdlet class in `src/PS/Login/Cmdlets/GetCirrusProfileCmdlet.cs` — supports -Name and -List parameters, outputs Profile objects to pipeline with IsActive computed property

**Checkpoint**: Full introspection available. Users can discover their profiles and active state via CLI and PowerShell.

---

## Phase 6: User Story 4 — Modify and Remove Profiles (Priority: P4)

**Goal**: Users can update profile fields (subscription, tenant, defaults) and delete profiles they no longer need.

**Independent Test**: Create a profile, modify its subscription, verify change persists, remove it, verify it no longer appears in list.

### Implementation for User Story 4

- [x] T021 [US4] Create ProfileModifyProcessor in `src/Extensions/Login/Processors/ProfileModifyProcessor.cs` implementing IProcessor — calls IProfileService.UpdateProfileAsync with changed fields, supports --subscription, --tenant-id, --default key=value, --remove-default key; error if profile not found or no flags provided (FR-004, FR-015)
- [x] T022 [P] [US4] Create ProfileRemoveProcessor in `src/Extensions/Login/Processors/ProfileRemoveProcessor.cs` implementing IProcessor — calls IProfileService.DeleteProfileAsync, clears active pointer if removing active profile with warning, error if profile not found (FR-004, FR-015)
- [x] T023 [US4] Add `profile modify` and `profile remove` CLI command metadata entries in `src/Extensions/Login/ExtensionIndex.cs`
- [x] T024 [P] [US4] Add `Set-CirrusProfile` and `Remove-CirrusProfile` PS command metadata entries in `src/Extensions/Login/ExtensionIndex.cs`
- [x] T025 [P] [US4] Create SetCirrusProfileCmdlet PS cmdlet class in `src/PS/Login/Cmdlets/SetCirrusProfileCmdlet.cs` — supports -Name, -SubscriptionId, -TenantId, -Default hashtable, -RemoveDefault string array
- [x] T026 [P] [US4] Create RemoveCirrusProfileCmdlet PS cmdlet class in `src/PS/Login/Cmdlets/RemoveCirrusProfileCmdlet.cs` — supports -Name, -Force (skip ShouldProcess confirmation)

**Checkpoint**: Complete profile lifecycle management. Users can create, read, update, and delete profiles.

---

## Phase 7: User Story 5 — Per-Command Profile Override (Priority: P5)

**Goal**: Users can use `--profile <name>` on any command to temporarily override the active profile for a single execution.

**Independent Test**: Set `dev` active, run `group list --profile prod`, verify command uses `prod` context, verify active profile remains `dev`.

### Implementation for User Story 5

- [x] T027 [US5] Modify Program.cs in `src/CLI/Program.cs` — before ExtensionDispatcher.Parse(args), scan args for `--profile <name>`, strip it, and inject value as `__profile` reserved key into CommandInvocation parameters (per R6, FR-010)
- [x] T028 [US5] Verify Engine.CreateCommandContext() in `src/Core/Core.Engine/Engine.cs` correctly reads `__profile` from invocation parameters and resolves profile override → active profile → error (FR-011, FR-012) — this is already scaffolded, confirm behavior matches spec
- [x] T029 [P] [US5] Implement `-Profile` common parameter extraction in the PS engine path — extract `-Profile` parameter before IEngine.Process() dispatch and inject as `__profile` reserved key

**Checkpoint**: Per-command profile override works across both CLI and PowerShell surfaces.

---

## Phase 8: User Story 6 — Logout Respects Profiles (Priority: P6)

**Goal**: Users can logout from the active profile (or a specific profile) to clear cached tokens while preserving the profile definition.

**Independent Test**: Login to a profile, run `logout`, verify token is cleared, run a command and verify re-auth is triggered.

### Implementation for User Story 6

- [x] T030 [US6] Modify LogoutProcessor in `src/Extensions/Login/Processors/LogoutProcessor.cs` — resolve target profile (active or --profile override), call IAuthService to clear MSAL token for that profile's tenant/user/auth-method, preserve profile definition, error with guidance if no active profile (FR-009, FR-015)
- [x] T031 [US6] Update `logout` CLI command metadata entry with `--profile` parameter in `src/Extensions/Login/ExtensionIndex.cs`
- [x] T032 [US6] Update `Disconnect-CirrusAccount` PS command metadata entry with `-Profile` parameter in `src/Extensions/Login/ExtensionIndex.cs`

**Checkpoint**: Complete login/logout lifecycle with profile awareness.

---

## Phase 9: Polish & Cross-Cutting Concerns

**Purpose**: Improvements that affect multiple user stories

- [x] T033 [P] Update PS module manifest `src/PS/Login/Login.psd1` — add new cmdlets to CmdletsToExport: Get-CirrusProfile, Switch-CirrusProfile, Set-CirrusProfile, Remove-CirrusProfile
- [x] T034 [P] Add telemetry enrichment — ensure active profile's tenant ID is included in telemetry payloads via existing TelemetryService integration (FR-017)
- [x] T035 Verify all profile error messages include profile name, operation, and actionable guidance (FR-015) — review every error path across all processors
- [x] T036 Run quickstart.md validation — execute the CLI workflow steps end-to-end and verify outputs match documented expectations
- [x] T037 Verify `dotnet build cirrus.slnx` succeeds with zero warnings and `dotnet test tests/Cli.Tests/Cli.Tests.csproj` passes

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — can start immediately
- **Foundational (Phase 2)**: Depends on Setup — BLOCKS all user stories
- **User Stories (Phase 3-8)**: All depend on Foundational (Phase 2) completion
  - US1 (Phase 3): Can start after Phase 2
  - US2 (Phase 4): Can start after Phase 2 (independent of US1 at code level, but logically follows)
  - US3 (Phase 5): Can start after Phase 2
  - US4 (Phase 6): Can start after Phase 2
  - US5 (Phase 7): Can start after Phase 2
  - US6 (Phase 8): Can start after Phase 2
- **Polish (Phase 9)**: Depends on all user story phases being complete

### User Story Dependencies

- **US1 (P1)**: Depends on Foundational only — no dependencies on other stories
- **US2 (P2)**: Depends on Foundational only — can run parallel to US1
- **US3 (P3)**: Depends on Foundational only — can run parallel to US1, US2
- **US4 (P4)**: Depends on Foundational only — can run parallel to US1-US3
- **US5 (P5)**: Depends on Foundational only — can run parallel to US1-US4
- **US6 (P6)**: Depends on Foundational only — can run parallel to US1-US5

### Within Each User Story

- Models before services (already in Foundational)
- Processors before ExtensionIndex entries (processor type referenced in metadata)
- CLI entries before PS cmdlet classes (PS cmdlets dispatch via engine)
- Core implementation before integration

### Parallel Opportunities

- All Foundational tasks marked [P] can run in parallel (T003, T008 target different files)
- Once Foundational completes, all 6 user stories can start in parallel
- Within US4: T021, T022, T025, T026 target different files and are parallelizable
- Within US5: T027, T029 target different files (CLI vs PS) and are parallelizable
- Polish phase tasks marked [P] can run in parallel (T033, T034)

---

## Parallel Example: Foundational Phase

```bash
# Sequential (must complete in order)
T002 → T004 → T005 → T006 → T007

# Parallel with T002
T003 (different file: ProfileStore.cs)
T008 (different file: StubProfileService.cs)
```

## Parallel Example: After Foundational

```bash
# All user story phases can start simultaneously
Phase 3 (US1): T009 → T010 → T011 → T012 → T013 → T014
Phase 4 (US2): T015 → T016, T017 (parallel)
Phase 5 (US3): T018 → T019, T020 (parallel)
Phase 6 (US4): T021 + T022 (parallel) → T023 + T024 + T025 + T026 (parallel)
Phase 7 (US5): T027 + T029 (parallel) → T028
Phase 8 (US6): T030 → T031 + T032 (parallel)
```

---

## Implementation Strategy

### MVP Scope (Recommended)

**Phase 1 + Phase 2 + Phase 3 (US1) + Phase 4 (US2) + Phase 5 (US3)** — delivers profile creation via login, switching, and listing/inspection. This is sufficient for the demo scenarios identified in the meeting notes.

### Incremental Delivery

1. **Increment 1** (MVP): Phases 1-5 (Setup + Foundation + US1 + US2 + US3)
2. **Increment 2**: Phase 6 (US4 — modify/remove)
3. **Increment 3**: Phase 7 (US5 — per-command override, stretch goal)
4. **Increment 4**: Phase 8 (US6 — logout integration)
5. **Increment 5**: Phase 9 (Polish)
