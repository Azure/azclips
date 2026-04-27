# Stop 99 - Summary

## What we did

We took a rough profile-auth idea and turned it into concrete, buildable docs plus starter code and starter tests.

In simple terms:

1. We reviewed the original requirements and listed what was missing.
2. We wrote a full profile spec draft with clear rules.
3. We split the work into step-by-step docs:
   - Step 1: command list and command parity
   - Step 2: profile file shape and storage rules
   - Step 3: session behavior and token boundaries
4. We created an implementation checklist document so the team can build in order.
5. We added code scaffolding for profile storage and active profile switching.
6. We added xUnit test matrix files to lock in what must be tested.
7. We ran build and tests, fixed one break caused by interface changes, and got green builds.

## Commands defined (and what each does)

These are the commands now defined in the spec work.

### CLI

- `azv2 login --subscription <id> --tenant <id> --profile <name>`
  - Signs in and creates/updates the named profile.
  - Sets that profile as active.

- `azv2 profile list`
  - Shows all saved profiles.
  - Marks which one is active.

- `azv2 profile show`
  - Shows the currently active profile.

- `azv2 profile set <name>`
  - Makes a saved profile the active one.

- `azv2 profile delete <name> [--force]`
  - Deletes a saved profile.
  - If it is the active profile, force is required.

- `azv2 profile rename <oldName> <newName>`
  - Renames a profile without losing profile details.

- `--profile <name>` on normal commands
  - Uses that profile only for this one command run.
  - Does not change active profile.

### PowerShell

- `Connect-CirrusAccount -SubscriptionId <id> -TenantId <id> -Profile <name>`
  - Sign in and create/update that profile.

- `Get-CirrusProfile`
  - List all profiles.

- `Get-CirrusProfile -Active`
  - Show active profile.

- `Select-CirrusProfile -Name <name>`
  - Set active profile.

- `Remove-CirrusProfile -Name <name> [-Force]`
  - Delete profile (same active-profile safety behavior).

- `Rename-CirrusProfile -Name <oldName> -NewName <newName>`
  - Rename profile.

- `-Profile <name>` on profile-aware cmdlets
  - One-time profile override for the current command only.

## How a user would use this (simple flow)

1. Sign in and create first profile:
   - `azv2 login --subscription <id> --tenant <id> --profile work`
2. Create another profile:
   - `azv2 login --subscription <id2> --tenant <id2> --profile personal`
3. See profiles:
   - `azv2 profile list`
4. Switch default profile:
   - `azv2 profile set personal`
5. Run one command with a temporary override:
   - `azv2 group list --profile work`
6. Remove old profile:
   - `azv2 profile delete oldprofile`

PowerShell is the same idea using the matching cmdlets.

## Code changes made

### New/updated spec and planning docs

- `docs/spec/profile/profile-rjs.md`
- `docs/spec/profile/history.md`
- `docs/spec/profile/step-1-command-surface-and-parity.md`
- `docs/spec/profile/step-2-profile-schema-and-storage.md`
- `docs/spec/profile/step-3-session-semantics-and-token-boundaries.md`
- `docs/spec/profile/implementation-plan.md`
- `docs/spec/profile/stop 99 - summary.md`

### New/updated code scaffolding

- `src/Core/Core.Abstractions/Models/AuthProfile.cs`
  - New profile metadata model.

- `src/Core/Core.Abstractions/IAuthContextService.cs`
  - Added profile methods:
    - Save profile
    - Get profile
    - List profiles
    - Set active profile
    - Get active profile

- `src/Core/Core.Authentication/AuthContextService.cs`
  - Added profile folder and active-profile pointer support.
  - Added profile read/write/list helpers.
  - Added active profile set/get helpers.
  - `GetCurrentContextAsync` now prefers active profile when present.
  - Kept legacy context behavior for compatibility fallback.

- `tests/Common/Stubs/StubAuthContextService.cs`
  - Updated to implement new interface methods.

## Tests added and what they prove

### Added test files

- `tests/Cli.Tests/Profile/ProfileCommandContractMatrixTests.cs`
- `tests/Cli.Tests/Profile/ProfileStorageContractMatrixTests.cs`
- `tests/Cli.Tests/Profile/ProfileSessionSemanticsMatrixTests.cs`

### What they prove right now

Right now, these are matrix scaffolds (intentionally skipped). They prove:

- The expected test cases are now captured in code.
- The test project can discover these profile test suites.
- The repo still builds and test runs succeed with this new structure.

### What they do not prove yet

- They do not yet validate real command behavior end-to-end.
- They do not yet validate real file writes/reads for profile storage.
- They do not yet validate real runtime switching behavior.

In other words: the test structure is in place; assertion logic is the next step.

## Commands we ran to validate

- `dotnet build cirrus.slnx`
- `dotnet test tests/Cli.Tests/Cli.Tests.csproj`

Final result after fixes:

- Build: passed
- Tests: passed (with expected skipped matrix scaffolds)

## Bottom line

You now have:

- Clear command definitions
- Clear storage/session rules
- A buildable code scaffold for profile state
- A concrete implementation task plan
- A test matrix ready to be turned into active assertions

This puts the project in a "ready to implement for real" state rather than a "still discussing requirements" state.
