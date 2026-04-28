# Research: Profile Management

**Feature**: 001-profile-management
**Date**: 2026-04-29
**Status**: Complete — all Technical Context items resolved

## R1: Atomic File Writes on .NET (Cross-Platform)

**Decision**: `File.WriteAllTextAsync` to a temp file in the same directory, then `File.Move(temp, target, overwrite: true)` (requires .NET 5+).

**Rationale**: On both Unix and NTFS, renaming a file within the same directory is atomic at the filesystem level. Writing directly to the target file risks partial writes on crash. The temp file must be in the same directory (not `/tmp`) to guarantee same-filesystem rename.

**Alternatives considered**:
- `FileStream` with `FileOptions.WriteThrough` — prevents OS buffering but doesn't prevent partial content on crash.
- SQLite — overkill for a single JSON file with < 100 entries; adds a native dependency.

## R2: File Permissions on .NET (Cross-Platform)

**Decision**: On Unix, use `File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite)` (.NET 7+ API). On Windows, rely on the default user-profile ACL — files created in `LocalApplicationData` are already scoped to the current user.

**Rationale**: The .NET 7+ `UnixFileMode` API is the idiomatic cross-platform approach. Windows `LocalApplicationData` (`%LOCALAPPDATA%`) is per-user by OS design; adding explicit ACL manipulation via `System.Security.AccessControl` is unnecessary for MVP.

**Alternatives considered**:
- P/Invoke to `chmod` — unnecessary now that .NET has a managed API.
- `System.Security.AccessControl` on Windows — adds complexity for negligible benefit since `LocalApplicationData` is already user-scoped.

## R3: In-Process Concurrency for Profile Writes

**Decision**: `SemaphoreSlim(1, 1)` wrapping all read-modify-write sequences in `ProfileService`. Released in `finally` blocks.

**Rationale**: Cirrus is a CLI tool — there's at most one process per terminal session. In-process concurrency (e.g., background telemetry + foreground command) is the only realistic concurrent-write scenario. Cross-process concurrency is handled by atomic file writes (R1); no file locking needed.

**Alternatives considered**:
- `Mutex` (named, cross-process) — adds OS-level locking complexity for a scenario (parallel `profile switch` from two terminals) where last-writer-wins is the accepted behavior per spec.
- `ReaderWriterLockSlim` — reads don't contend in practice since profile data is cached in memory after first load; a simple semaphore is sufficient.

## R4: Profile Model Extensibility (Defaults Dictionary)

**Decision**: `Dictionary<string, string> Defaults` property on the `Profile` class. Serialized as a flat JSON object. No schema validation on keys — extensions consume keys they recognize and ignore others.

**Rationale**: The spec requires extensible defaults (location, resource group, etc.) that can grow over time without Profile model changes. A `Dictionary<string, string>` is the simplest extensible representation. String-typed values avoid deserialization ambiguity.

**Alternatives considered**:
- `Dictionary<string, object>` — introduces JSON deserialization ambiguity (numbers vs strings, nested objects).
- Typed properties (`DefaultLocation`, `DefaultResourceGroup`) — requires model changes for every new default; violates extensibility requirement.

## R5: IAuthContextService Adapter Strategy

**Decision**: Modify `AuthContextService` to delegate to `IProfileService` internally. `GetCurrentContextAsync` reads the active profile and derives an `AuthContext`. `SaveContextAsync` creates/overwrites the `temp` profile. `ClearContextAsync` clears the active profile's token. The interface signature is unchanged.

**Rationale**: This preserves backward compatibility with any existing code that depends on `IAuthContextService` while making `Profile` the source of truth. The adapter pattern allows incremental migration — callers don't need to know about profiles.

**Alternatives considered**:
- Remove `IAuthContextService` entirely — breaks existing Login extension code and any other consumers. Constitution principle I (Bootstrapper DI) means all callers resolve via interface; we can swap the implementation without touching callers.
- Dual-write (save to both `context.json` and `profiles.json`) — unnecessary since this is a greenfield product with no migration concerns.

## R6: `--profile` Parameter Interception at Engine Level

**Decision**: In `Program.cs`, before `ExtensionDispatcher.Parse(args)`, scan `args` for `--profile <name>`. If found, strip it from args and pass the value as a reserved `__profile` key on the `CommandInvocation`. `Engine.CreateCommandContext()` reads `__profile` to resolve the profile override. The `ExtensionDispatcher` and per-command processors never see `--profile`.

**Rationale**: Handling `--profile` at the engine level (FR-010) ensures every command gets it for free without per-extension parameter registration. The reserved `__profile` key avoids collisions with command-specific parameters. This pattern already exists in the codebase — `Engine.cs` already extracts `__profile` from invocation parameters.

**Alternatives considered**:
- Register `--profile` on every `CommandMetadata` — violates FR-010 (engine-level handling) and creates maintenance burden.
- Environment variable (`CIRRUS_PROFILE`) — adds a second resolution path, creating the kind of ambiguity the spec explicitly prohibits (FR-011).

## R7: Profile Commands in Login Extension vs. New Extension

**Decision**: Add profile commands to the existing Login extension (`Microsoft.Azure.Cirrus.Extensions.Login`).

**Rationale**: Profile management is intrinsically tied to authentication (login creates profiles, logout clears tokens, profile switch may trigger re-auth). Creating a separate `Microsoft.Azure.Cirrus.Extensions.Profile` extension would split related functionality across two assemblies and two `ExtensionIndex` implementations, increasing complexity without benefit.

**Alternatives considered**:
- New `Profile` extension — adds a new `.csproj`, assembly, and `ExtensionIndex`. More surface area to maintain for tightly coupled functionality.

## R8: Schema Versioning Strategy

**Decision**: `ProfileStore` envelope includes a `SchemaVersion` integer field (starting at `1`). On deserialization, if `SchemaVersion` exceeds the current known version, throw a clear error. Forward-compatible reads (unknown keys ignored via `JsonSerializerOptions`) handle minor additions without version bumps. Version is incremented only for breaking structural changes.

**Rationale**: Integer versioning is simpler than SemVer for a single file format. Forward-compatible reads reduce the frequency of version bumps. Since this is greenfield, there are no legacy versions to support.

**Alternatives considered**:
- SemVer string — overkill for a local file format; parsing adds unnecessary complexity.
- No versioning — the spec explicitly requires it (FR-014) for downgrade detection.
