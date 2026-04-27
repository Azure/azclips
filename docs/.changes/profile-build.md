# Profile Commands, Storage, Migration, and Tests

Date: 2026-04-27

## Summary

Implemented profile-based authentication for `azv2` end-to-end across CLI and PowerShell, including durable storage, legacy `context.json` migration, and full automated test coverage wired into CI.

Wall-clock effort from first spec file to this update: **~1 h 25 min** (`02:57:52 UTC` → `04:22:52 UTC`).

## Spec & Planning Docs

- `docs/spec/profile/profile-rjs.md` — normative spec (MUST/SHOULD/MAY, exit codes, schema, errors).
- `docs/spec/profile/history.md` — process notes.
- `docs/spec/profile/step-1-command-surface-and-parity.md`
- `docs/spec/profile/step-2-profile-schema-and-storage.md`
- `docs/spec/profile/step-3-session-semantics-and-token-boundaries.md`
- `docs/spec/profile/implementation-plan.md`
- `docs/spec/profile/stop 99 - summary.md` — running summary with results, design diagrams, and command-status matrix.

## Code Changes

### Core abstractions / authentication

- `src/Core/Core.Abstractions/Models/AuthProfile.cs` — new record with `ProfileName`, `TenantId`, `SubscriptionId`, `AccountId`, `AccountType`, `Environment`, `CreatedAt`, `LastUsedAt`, `DisplayName`, `Version`, plus `ToAuthContext()`.
- `src/Core/Core.Abstractions/IAuthContextService.cs` — added profile API:
  - `SaveProfileAsync`
  - `GetProfileAsync`
  - `ListProfilesAsync`
  - `SetActiveProfileAsync` / `GetActiveProfileAsync`
  - `DeleteProfileAsync(name, force)`
  - `RenameProfileAsync(old, new)`
- `src/Core/Core.Authentication/AuthContextService.cs`:
  - Profile dir + `active-profile` pointer file layout.
  - Atomic write (temp + rename) and tolerant read.
  - Name normalization/validation (rejects whitespace, path separators, `.` / `..`).
  - `GetCurrentContextAsync` prefers active profile, falls back to legacy context.
  - Legacy migration (Epic 4): on first read, materializes `default` profile from `context.json`, sets active pointer, renames original to `context.json.migrated.bak`. Idempotent — skips when profile state already exists.

### CLI (Login extension)

- `src/CLI/Parser/CommandGroupIndex.cs` — `Profile` group routes to `Login` extension.
- `src/Extensions/Login/ExtensionIndex.cs` — registered metadata + dispatch entries for both CLI and PowerShell.
- New command handlers under `src/Extensions/Login/Commands/`:
  - `ListProfiles.cs` (`profile list`) — emits one row per profile so PS pipelines enumerate correctly.
  - `ShowActiveProfile.cs` (`profile active`).
  - `SetActiveProfile.cs` (`profile set --name <X>`).
  - `DeleteProfile.cs` (`profile delete --name <X> [--force]`).
  - `RenameProfile.cs` (`profile rename --name <old> --new-name <new>`).
- Extended `Login.cs` (`azv2 login`) to accept `--profile <name>`; on success the named profile is created/updated and made active.

### PowerShell (Cirrus.Login module)

- `src/PS/Login/Login.psd1` — exported new functions.
- New cmdlets under `src/PS/Login/exports/`:
  - `Get-CirrusProfile.ps1` (default lists; `-Active` shows the active profile).
  - `Select-CirrusProfile.ps1` (`SupportsShouldProcess`).
  - `Remove-CirrusProfile.ps1` (`-Force`, `ConfirmImpact = High`).
  - `Rename-CirrusProfile.ps1`.
- Extended `Connect-CirrusAccount.ps1` to accept `-Profile <name>`.

### Tests / stubs

- `tests/Common/Stubs/StubAuthContextService.cs` — implements all new interface methods.
- `tests/Cli.Tests/Cli.Tests.csproj` — added project references for `Core.Authentication` and `Tests.Common`.
- `tests/Cli.Tests/Profile/ProfileFilesystemCollection.cs` — `[CollectionDefinition("ProfileFilesystem", DisableParallelization = true)]` so tests touching `~/.cirrus/` serialize.
- `tests/Cli.Tests/Profile/ProfileCommandsTests.cs` — 13 real assertions for list/active/set/delete/rename plus name-validation theory (snapshot/restore of active pointer to preserve user state).
- `tests/Cli.Tests/Profile/ProfileMigrationTests.cs` — 3 tests for legacy-context migration (materializes default, idempotent, skips when profile state already exists).
- `tests/PS.Tests/Login.Profile.Tests.ps1` — 10 Pester tests covering module surface, list/select/active, remove (with/without `-Force`), rename (success and missing-source).
- Existing `*MatrixTests.cs` scaffolds remain `[Theory(Skip=…)]` placeholders for the broader contract matrices.

### CI

- `.github/workflows/build-and-test.yml`:
  - Installs Pester (`MinimumVersion 5.0.0`).
  - Runs `Invoke-Pester -Path ./tests/PS.Tests` and fails the job on any failure.
  - Existing `dotnet test cirrus.slnx` step automatically picks up the new xUnit profile tests.

## Validation

- `dotnet build cirrus.slnx` — green, 0 warnings, 0 errors.
- `dotnet test cirrus.slnx` — **20 passed, 0 failed, 7 skipped** (matrix scaffolds + unrelated fallback tests).
- `dotnet test … --filter Cli.Tests.Profile` — **16 passed, 0 failed, 3 skipped** (matrix scaffolds).
- Pester (`./tests/PS.Tests`) — **10 passed, 0 failed**.
- Live demo against `./artifacts/Debug/Core.Cli/azv2`: `profile list`, `profile active`, `profile set`, `profile delete`, `profile rename` all execute end-to-end.

## Status against the implementation plan

- **Epic 1 — Command Surface and Parity:** complete (CLI + PS).
- **Epic 2 — Profile Schema and Storage:** complete.
- **Epic 4 — Migration and Compatibility:** complete (idempotent first-run migration, backup file).
- **Epic 5.1:** partial — real assertions for everything implemented; three matrix scaffolds still skipped.
- **Epic 1.1.4:** exit-code mapping not yet applied (any failure → generic non-zero).
- **Epic 1.2.2:** `-Profile` parameter only on `Connect-CirrusAccount` / `azv2 login` so far.
- **Epic 3 — Session Semantics:** not started (`--profile` runtime override resolver, silent-auth fallback, token-isolation enforcement).
- **Epic 5.2 integration tests:** pending Epic 3.

## Cross-cutting / known gaps

- CLI output is the .NET `ToString()` representation; no `--output table|json` formatter yet.
- No telemetry events specific to profile commands.
- Wiki and module READMEs not yet updated.
