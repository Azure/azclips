# Feature Specification: Profile Management

**Feature Branch**: `001-profile-management`
**Created**: 2026-04-29
**Status**: Draft
**Input**: User description: "Build Profile Management for Cirrus — named authentication profiles with CRUD, switching, login integration, and per-command override."

## Clarifications

### Session 2026-04-29

- Q: How is a subscription selected when creating a profile via login and multiple subscriptions exist under the tenant? → A: Interactive prompt when multiple exist; auto-select when only one; `--subscription` flag to skip prompt for scripting.
- Q: What is the migration path from the existing `context.json` to `profiles.json`? → A: None needed — greenfield product with no existing users. `context.json` is simply replaced by `profiles.json`. Breaking changes are acceptable.
- Q: What file permissions should `profiles.json` be created with? → A: Owner-only read/write (Unix `0600`, Windows current-user ACL). Cross-platform support required (macOS + Windows).
- Q: Should a `profile validate` command be included? → A: Deferred to a future iteration. Useful but not needed for MVP.
- Q: How should concurrent active-profile writes be handled? → A: Last-writer-wins for the active pointer. `profile switch` must print confirmation of the resulting active profile after writing.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Create and Use a Named Profile (Priority: P1)

A developer logs into Cirrus for the first time. After authenticating via browser-based login, they create a named profile that captures their tenant, subscription, and identity. Future commands automatically use this profile without requiring re-authentication or manual context selection.

**Why this priority**: Without the ability to create and persist a profile, no other profile feature (switching, overrides, listing) has meaning. This is the foundation of the entire feature.

**Independent Test**: Can be fully tested by running `login --out-profile dev`, then running any command (e.g., `group list`) and verifying it uses the `dev` profile's tenant and subscription context.

**Acceptance Scenarios**:

1. **Given** no profiles exist and the tenant has exactly one subscription, **When** the user runs `login --out-profile dev`, **Then** the subscription is auto-selected, and a profile named `dev` is created with the authenticated tenant ID, subscription ID, user principal name, auth method, and it is set as the active profile.
6. **Given** no profiles exist and the tenant has multiple subscriptions, **When** the user runs `login --out-profile dev` without `--subscription`, **Then** the system lists available subscriptions interactively and prompts the user to choose one before creating the profile.
7. **Given** no profiles exist and the tenant has multiple subscriptions, **When** the user runs `login --out-profile dev --subscription sub-B`, **Then** the profile is created using `sub-B` without an interactive prompt (enables scripting and CI scenarios).
2. **Given** no profiles exist, **When** the user runs `login` without `--out-profile`, **Then** a disposable profile named `temp` is auto-created/overwritten and set as active.
3. **Given** a profile named `dev` already exists, **When** the user runs `login --out-profile dev`, **Then** the existing `dev` profile is overwritten with a warning message and the updated profile is set as active.
4. **Given** an active profile `dev` exists, **When** the user runs any command (e.g., `group list`), **Then** the command executes using the tenant, subscription, and identity from the `dev` profile.
5. **Given** an active profile exists, **When** the user runs `profile show`, **Then** the system displays the active profile's name, tenant ID, subscription ID, auth method, user principal name, and any defaults.

---

### User Story 2 - Switch Between Named Profiles (Priority: P2)

An enterprise developer works across multiple tenants (e.g., dev and production). They have previously created named profiles for each environment. They switch between profiles to change the active identity context without re-authenticating, and the system re-authenticates only when the cached token for the target profile is expired or missing.

**Why this priority**: Multi-tenant switching is the core differentiator over Azure CLI/PowerShell and directly addresses the #1 enterprise pain point. Depends on profile creation (P1) existing first.

**Independent Test**: Can be tested by creating two profiles (`dev` and `prod`), switching between them with `profile switch`, and verifying each subsequent command uses the correct tenant context.

**Acceptance Scenarios**:

1. **Given** profiles `dev` and `prod` exist with `dev` active, **When** the user runs `profile switch prod`, **Then** the active profile changes to `prod` and subsequent commands use `prod`'s identity context.
2. **Given** profiles `dev` and `prod` exist with `dev` active, **When** the user runs `profile switch prod` and the cached token for `prod` is still valid, **Then** no re-authentication prompt appears.
3. **Given** profiles `dev` and `prod` exist, **When** the user runs `profile switch prod` and the cached token for `prod` is expired, **Then** the system initiates re-authentication using the auth method stored in the `prod` profile.
4. **Given** only one profile exists, **When** the user runs `profile switch nonexistent`, **Then** the system returns an error with the message including the list of available profiles and guidance to create the missing profile.

---

### User Story 3 - List and Inspect Profiles (Priority: P3)

A developer wants to understand which profiles are configured, which one is currently active, and the details of a specific profile. They use `profile list` and `profile show <name>` to inspect their setup.

**Why this priority**: Introspection and discoverability are critical for usability, but require profiles to already exist. Lower priority than creation and switching.

**Independent Test**: Can be tested by creating two profiles, running `profile list` and `profile show <name>`, and verifying the output includes all expected fields and correctly marks the active profile.

**Acceptance Scenarios**:

1. **Given** profiles `dev`, `prod`, and `temp` exist with `dev` active, **When** the user runs `profile list`, **Then** the system displays all three profiles with the active profile visually indicated (e.g., `*` marker or `(active)` label).
2. **Given** a profile `dev` exists, **When** the user runs `profile show dev`, **Then** the system displays all fields: name, tenant ID, subscription ID, cloud name, auth method, user principal name, client ID (if applicable), and any configured defaults.
3. **Given** no profiles exist, **When** the user runs `profile list`, **Then** the system displays a message indicating no profiles are configured and provides guidance to create one (e.g., "Run `login --out-profile <name>` to create a profile.").

---

### User Story 4 - Modify and Remove Profiles (Priority: P4)

A developer needs to update a profile's subscription or defaults (e.g., default location), or remove an obsolete profile. They use `profile modify` and `profile remove` to maintain their profile inventory.

**Why this priority**: Profile lifecycle management is important but not urgently needed for core demo scenarios. Users can work around this by re-running login with `--out-profile`.

**Independent Test**: Can be tested by creating a profile, modifying its subscription, verifying the change persists, then removing it and verifying it no longer appears in `profile list`.

**Acceptance Scenarios**:

1. **Given** a profile `dev` exists with subscription `sub-A`, **When** the user runs `profile modify dev --subscription sub-B`, **Then** the `dev` profile's subscription ID is updated to `sub-B` and all other fields remain unchanged.
2. **Given** a profile `dev` exists, **When** the user runs `profile modify dev --default location=eastus`, **Then** the `dev` profile gains a default key-value pair `location=eastus`.
3. **Given** profiles `dev` and `prod` exist with `dev` active, **When** the user runs `profile remove prod`, **Then** the `prod` profile is deleted and `dev` remains active.
4. **Given** only profile `dev` exists and is active, **When** the user runs `profile remove dev`, **Then** the system removes the profile and clears the active profile pointer. Subsequent commands that require a profile return an error with guidance.
5. **Given** a profile `dev` exists, **When** the user runs `profile remove nonexistent`, **Then** the system returns an error indicating the profile does not exist.

---

### User Story 5 - Per-Command Profile Override (Priority: P5)

A developer has `dev` as their active profile but needs to run a single command against the `prod` tenant. They use the `--profile prod` flag on that command to temporarily use the `prod` profile without changing the active pointer.

**Why this priority**: This is a power-user scenario that depends on all prior stories. The meeting notes explicitly defer this from MVP scope, but the requirements document includes it. It is included here for completeness but is a stretch goal.

**Independent Test**: Can be tested by setting `dev` as active, running `group list --profile prod`, verifying the command uses `prod`'s context, then running `group list` again and verifying it still uses `dev`.

**Acceptance Scenarios**:

1. **Given** profiles `dev` (active) and `prod` exist, **When** the user runs `group list --profile prod`, **Then** the command executes using `prod`'s identity context and the active profile remains `dev`.
2. **Given** profiles `dev` (active) and `prod` exist, **When** the user runs `group list --profile nonexistent`, **Then** the system returns an error including available profile names.
3. **Given** the `--profile` flag is provided and an active profile also exists, **When** the engine resolves the profile, **Then** the `--profile` flag takes precedence over the active profile (resolution order: `--profile` flag → active profile → error).

---

### User Story 6 - Logout Respects Profiles (Priority: P6)

A developer wants to log out from a specific profile's identity, clearing its cached token, without destroying the profile definition itself. They can then re-authenticate later using the same profile.

**Why this priority**: Logout integration is needed for complete lifecycle support but is not required for the core demo flow.

**Independent Test**: Can be tested by logging into a profile, running `logout`, verifying the token is cleared, then running a command and verifying re-authentication is triggered.

**Acceptance Scenarios**:

1. **Given** a profile `dev` is active and has a valid cached token, **When** the user runs `logout`, **Then** the MSAL token for `dev` is cleared, the profile definition is preserved, and subsequent commands trigger re-authentication.
2. **Given** profiles `dev` (active) and `prod` exist, **When** the user runs `logout --profile prod`, **Then** only `prod`'s cached token is cleared; `dev` remains active and its token is unaffected.
3. **Given** no active profile exists, **When** the user runs `logout`, **Then** the system returns an error with guidance to select or create a profile first.

---

### Edge Cases

- What happens when `profiles.json` is corrupted or contains invalid JSON? The system MUST detect the corruption, report a clear error with the file path, and offer guidance (e.g., "Delete `~/.cirrus/profiles.json` and re-create your profiles.").
- What happens when two parallel processes attempt to write to `profiles.json` simultaneously? Writes MUST be atomic (write-to-temp-file + rename) to prevent corruption. For the active-profile pointer, last-writer-wins semantics apply — no locking is required. Each `profile switch` prints confirmation so the user sees which profile is active after the write.
- What happens when the user downgrades Cirrus and the `profiles.json` schema has changed? The system MUST include a schema version in the file and reject incompatible versions with a clear message.
- What happens when a profile references a tenant ID that no longer exists or the user no longer has access? The error message MUST include the profile name and tenant ID to aid diagnosis.
- What happens when the user has hundreds of profiles? The `profile list` command MUST remain performant and display results in a scannable format.
- What happens when `--out-profile` is used with a name that contains special characters (spaces, slashes, dots)? Profile names MUST be validated against a safe character set (alphanumeric, hyphens, underscores) with a clear rejection message for invalid names.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST provide a `Profile` data model containing: name (unique identifier), tenant ID, subscription ID, cloud name, auth method, user principal name, client ID, and an extensible dictionary of default settings.
- **FR-002**: System MUST persist profiles to a local file at the platform-appropriate application data path, using atomic writes (write-to-temp + rename) to prevent corruption from parallel access.
- **FR-003**: System MUST maintain an active profile pointer that identifies which profile is used by default for command execution.
- **FR-004**: System MUST provide profile CRUD operations: create, list, show, modify (including defaults), and remove.
- **FR-005**: System MUST enforce unique profile names. Profile names MUST match the pattern `[a-zA-Z0-9_-]+` (alphanumeric, hyphens, underscores only).
- **FR-006**: System MUST support profile switching that changes the active profile pointer persistently. Switching MUST trigger re-authentication only when the cached token for the target profile is expired or absent.
- **FR-020**: The `profile switch` command MUST print the resulting active profile name after writing, confirming the switch took effect. Concurrent switches use last-writer-wins semantics for the active pointer.
- **FR-007**: The `login` command MUST accept an `--out-profile <name>` parameter. When provided, login creates or overwrites the named profile with the authenticated context. When omitted, login creates/overwrites a disposable `temp` profile.
- **FR-018**: The `login` command MUST accept an optional `--subscription <id|name>` parameter. When provided, the specified subscription is used without interactive prompting. When omitted and multiple subscriptions exist under the tenant, the system MUST list available subscriptions and prompt the user to choose. When only one subscription exists, it MUST be auto-selected silently.
- **FR-008**: When `--out-profile` targets an existing profile, the system MUST overwrite it and emit a warning to the user.
- **FR-009**: The `logout` command MUST clear the MSAL token for the active profile only (or for a specific profile if `--profile <name>` is provided), while preserving the profile definition.
- **FR-010**: The `--profile <name>` parameter MUST be handled at the engine level for both CLI and PowerShell surfaces, not implemented per-command. It uses a named profile for a single execution without changing the active pointer.
- **FR-011**: Profile resolution order MUST be: `--profile` flag → active profile → error with actionable guidance. There MUST be exactly one resolution path with no ambiguity.
- **FR-012**: The engine's command context creation MUST resolve the active profile (or `--profile` override), derive a transient `AuthContext` from it, and inject it into `ICommandContext`. `AuthContext` is no longer persisted — the Profile is the source of truth.
- **FR-013**: Profiles MUST NOT store credentials or tokens. Authentication tokens are managed by the underlying auth library (MSAL) and are associated with profiles by tenant/user/auth-method correlation.
- **FR-014**: The `profiles.json` file MUST include a schema version field. The system MUST reject files with incompatible schema versions with a clear error message.
- **FR-019**: The `profiles.json` file MUST be created with owner-only read/write permissions (Unix: `0600`, Windows: current-user ACL). This applies to both the final file and the temporary file used during atomic writes.
- **FR-015**: All error messages related to profile operations MUST include actionable guidance (e.g., suggesting commands to fix the issue, listing available profiles).
- **FR-016**: The existing `IAuthContextService` MUST be preserved as an internal adapter during migration. `AuthContext` is retained as the transient execution record passed to commands and as the login command's output model.
- **FR-017**: Telemetry payloads MUST include the active profile's tenant ID for correlation.

### Key Entities

- **Profile**: Represents a complete connection context. Attributes: name (unique, validated), tenant ID, subscription ID, cloud name, auth method (browser, device-code, managed-identity, service-principal), user principal name, client ID (optional, for SP scenarios), and a dictionary of extensible defaults (e.g., location, resource group). A profile does not store credentials or tokens.
- **Active Profile Pointer**: A single string value (profile name) stored alongside the profile collection, identifying which profile is the current default for command execution.
- **AuthContext (transient)**: A per-invocation record derived from the active or overridden profile. Contains the same identity fields but is created fresh for each command execution and never persisted. Serves as the login command output model and the execution context injected into `ICommandContext`.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Users can create a named profile and execute a command using it within 60 seconds of first login.
- **SC-002**: Users can switch between two profiles and execute commands in each context within 10 seconds (excluding any re-authentication prompt).
- **SC-003**: Parallel command executions (at least 5 concurrent processes) against the same `profiles.json` complete without file corruption or data loss.
- **SC-004**: All profile error messages include the specific profile name, the operation attempted, and at least one actionable suggestion — verified by review of every error path.
- **SC-005**: Profile operations (list, show, switch) complete in under 500 milliseconds for up to 100 configured profiles.
- **SC-006**: A user new to Cirrus can discover how to create, list, and switch profiles using only `--help` output and error message guidance, without consulting external documentation.
- **SC-007**: 100% of profile commands produce identical functional behavior via CLI (`azv2 profile ...`) and PowerShell (`*-CirrusProfile` cmdlets).

## Assumptions

- Users have local filesystem access for profile storage at the platform-appropriate application data path (matching existing Cirrus convention using `Environment.SpecialFolder.LocalApplicationData`).
- The MSAL token cache is already functional and profiles reference identities within it via tenant/user/auth-method — no new token storage is introduced.
- This is a greenfield product with no existing users. The `context.json` file is replaced outright by `profiles.json` — no migration or backward-compatibility shim is needed. Breaking changes are acceptable at this stage.
- Profile defaults (e.g., default location, resource group) are informational hints that extensions can optionally consume — they do not enforce behavior globally.
- The `--profile` per-command override is a stretch goal for MVP and may be deferred; core CRUD and switching are the MVP deliverables per meeting consensus.
- A `profile validate` command (verifying tenant reachability, subscription existence, token validity) is deferred to a future iteration. The requirements doc mentions it under introspection tools but it is not needed for MVP.
- Security hardening beyond file permissions (encryption, credential-store integration) is out of scope for v1 — owner-only file permissions (Unix `0600`, Windows current-user ACL) are the initial protection model, with security team engagement planned for a future iteration.
- The `config` concept (system-level settings) remains separate from `profile` (authentication context) and is not addressed by this feature.
