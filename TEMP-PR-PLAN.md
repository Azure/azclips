@TODO delete this file before merge

# TEMP PR plan: move extension home from ~/.cirrus to ~/.azclips

## Goal

Change extension storage defaults from `~/.cirrus/extensions` to `~/.azclips/extensions` with a hard cutover (no migration, no fallback, no legacy compatibility).

## Investigation summary (what currently uses ~/.cirrus/extensions)

- Runtime default extension root is hardcoded to `.cirrus/extensions` in:
    - `src/Core/Core.Configuration/Options/AssemblyLoadingOptions.cs`
- Default extension registry path is hardcoded to `.cirrus/extensions/registry.json` in:
    - `src/Core/Core.Extensions/FileExtensionRegistryStore.cs`
- Extension install path defaults to `.cirrus/extensions` in:
    - `src/Extensions/Extension/ExtensionInstaller.cs`
- Multiple call sites instantiate `new FileExtensionRegistryStore()` and rely on its default path:
    - `src/CLI/CliExtensionServiceRegistration.cs`
    - `src/CLI/Completions/CompletionGenerator.cs`
    - `src/CLI/Parser/ExtensionDispatcher.cs`
    - `src/Extensions/Extension/{Add,List,Pin,Remove,Show,Update}.cs`
- Loader/discovery reads only one configured root (`VersionedInstallationDir`) with no legacy fallback:
    - `src/CLI/AssemblyLoading/ExtensionLoader.cs`

## Investigation: configurable extension home (user-defined, default ~/.azclips)

- Existing config surface already has `assemblyLoading.versionedInstallationDir`, so we can expose a user-defined root without introducing a new key.
- Desired behavior for this branch:
    - default root is `~/.azclips/extensions`
    - user can override root via config key `assemblyLoading.versionedInstallationDir`
- Path composition should remain consistent:
    - extension payloads under `<root>/<packageId>/<version>/`
    - registry at `<root>/registry.json`
- Key implementation check:
    - `FileExtensionRegistryStore.DefaultPath()` should derive from configured `assemblyLoading.versionedInstallationDir` when available, or use default `~/.azclips/extensions` if unset.
    - If `FileExtensionRegistryStore` cannot reliably read config in its current construction path, add an injected/explicit path at call sites and avoid hidden static config lookups.
- Risk to investigate before implementation:
    - some call sites instantiate `new FileExtensionRegistryStore()` with no parameters; verify they all resolve the same effective root as extension loader/install logic.
    - test fixtures that directly assert suffixes (for example `.cirrus/extensions/registry.json`) must be updated to align with configurable/default behavior.

## Investigation: avoid duplicated string literals for home folder names

- Confirmed: folder-name literals are currently duplicated in multiple places (`.cirrus` appears in extension loading, registry path defaults, installer defaults, config/auth/agent paths, and comments/docs).
- For extension-home cutover scope, the same conceptual value is repeated in at least:
    - `src/Core/Core.Configuration/Options/AssemblyLoadingOptions.cs`
    - `src/Core/Core.Extensions/FileExtensionRegistryStore.cs`
    - `src/Extensions/Extension/ExtensionInstaller.cs`
- Proposed approach:
    - introduce one shared constant/helper for the default extension home segment (for example `DefaultExtensionHomeFolderName = ".azclips"` and a helper to resolve `<home>/<folder>/extensions`).
    - have all extension path producers call the same helper rather than repeating `Path.Combine(home, ".azclips", "extensions")`.
- Boundaries:
    - keep this dedup limited to extension-home paths in this PR.
    - auth/config/agent `.cirrus` literals can be tracked as separate follow-up renames to avoid accidental broad-scope changes.
- Validation:
    - static search should show no remaining duplicated extension-home literal construction outside the shared helper.
    - tests should still pass with default and with `assemblyLoading.versionedInstallationDir` override.

## Required implementation work

### 1) Switch defaults to ~/.azclips/extensions

- Update default versioned extension directory:
    - `src/Core/Core.Configuration/Options/AssemblyLoadingOptions.cs`
- Update default extension registry path:
    - `src/Core/Core.Extensions/FileExtensionRegistryStore.cs`
- Update extension installer default target path:
    - `src/Extensions/Extension/ExtensionInstaller.cs`
- Update comments/XML docs that still describe `~/.cirrus/extensions`:
    - `src/Core/Core.Extensions/ExtensionRegistry.cs`
    - `src/Core/Core.Extensions/ExtensionResolver.cs`
    - `src/CLI/CliExtensionServiceRegistration.cs`

### 2) Explicit non-goals for this branch

- Do not add migration from `~/.cirrus/extensions`.
- Do not add dual-read fallback logic.
- Do not preserve existing local installs/pins from legacy locations.
- Accept that existing developer machines may need to reinstall extensions after this change.

### 3) Tests to update/add

- Update existing default-path expectation:
    - `tests/Core.Extensions.Tests/FileExtensionRegistryStoreTests.cs`
- Add/adjust tests for hard-cut behavior:
    - fresh install writes and reads `~/.azclips/extensions/registry.json`
    - extension discovery/install/list/show/remove operate against the new root
- Review extension install/list/show/remove tests for path assumptions:
    - `tests/Cli.Tests/**` around extension commands

### 4) Docs to update

At minimum, replace extension-home references in:

- `docs/howto/extension-management.md`
- `docs/spec/extension-framework.md`
- `docs/spec/extension-management.md`

Also search and update any user-facing troubleshooting/help text mentioning `~/.cirrus/extensions`.

## Suggested execution order

1. Introduce shared helper/constant for extension home root to avoid scattered string literals.
2. Implement default path switch only (no migration/fallback).
3. Update tests for new defaults and hard-cut behavior.
4. Update docs.
5. Run targeted tests and then full relevant suite.

## Validation checklist

- `az extension add --name Compute` installs under `~/.azclips/extensions/...`
- `az extension list/show/remove/pin/update` all operate from `~/.azclips/extensions`
- Existing artifacts under `~/.cirrus/extensions` are ignored by default behavior
- No regression when user overrides `assemblyLoading.versionedInstallationDir`

## Open questions for PR description

- Should we include a release note callout that local developer extensions under `~/.cirrus/extensions` must be reinstalled?
- Should we include a one-liner cleanup note recommending manual deletion of stale `~/.cirrus/extensions`?
