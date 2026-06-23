# Temp PR Plan: Runtime Discovery for Extensions and Help

@TODO: delete before merge

## 1. Goal

Move Cirrus from a mostly static extension/help model to a runtime-discovery-driven model. `DiscoverySession` becomes the source of truth for what is installed and what each extension contributes. `registry.json` becomes a user-preferences layer on top (pinning, active version selection). Multi-version support is part of this design and will ship soon after the initial migration.

## 2. Guiding Principles

1. Discovery should answer what is installed and what each extension contributes now.
2. `registry.json` should track user preferences over discovered extensions (pinning, active version override) — not installed state.
3. Help should not depend on a hand-maintained verb-to-package hint table as the primary source of truth.
4. Keep the runtime path metadata-only; do not execute extension processors during discovery.
5. Multi-version support is a first-class design goal; bake it in from the start even if delivery is deferred.

## 3. Current State

1. Help currently resolves ownership through a mix of resolver, registry, and embedded hint table behavior.
2. DiscoverySession already exists and can emit command metadata without executing command processors.
3. Extension management currently persists installed state in `registry.json`, but that role should move to discovery.
4. The static hint table can drift from reality, which is what caused the stale `rest -> Resources` style mismatch. _(See §7 for deferred discussion on the root cause.)_

## 4. Proposed Direction

### 4.1 Discovery contract

Use `DiscoverySession` as the source of truth for extension capabilities. We may need to alter `DiscoverySession` in this branch to fully support this contract.

Capture at minimum:

1. packageId
2. resolvedVersion
3. built-in vs installed source
4. namespaces / contributed verbs
5. command metadata
6. per-command parameters and aliases
7. discovery issues or partial-load failures

### 4.2 Registry role

Narrow `registry.json` to user preferences over discovered extensions — not the source of installed state (discovery owns that).

Store:

1. active version override (falls back to most recently discovered version if absent)
2. pinned state
3. install source preference
4. install timestamp (metadata/audit)

> Note: installed state is determined by `DiscoverySession`, not by what is written in `registry.json`. _(See §7 for deferred discussion on why this direction is important.)_

### 4.3 Help migration

Rewrite help routing to consume discovery-backed metadata.

1. Root (top-level) help enumerates all installed extensions via discovery and emits help for each.
2. Group and sub-group help emits content for installed extensions only; unknown verbs surface structured install guidance.
3. The embedded hint table is still produced as a build artifact. Its role narrows to CNF (command not found) situations where no installed extension owns a requested verb — it tells the user which package to install.

### 4.4 Multi-version support

Prepare the model for parallel installed versions. This is a first-class goal; the design must support it even if full delivery is deferred. `DiscoverySession` is the natural home for this capability.

1. `registry.json` stores an optional active-version preference; falls back to the most recently installed version when absent.
2. `DiscoverySession` enumerates all installed versions of each extension when needed.
3. Help and dispatch use the active version only.
4. `extension list` shows all installed versions with active/pinned status.

### 4.5 Extension API surface

The `extension` command group (list, show, add, remove, update, pin, unpin) must be adapted to use `DiscoverySession` as its data source rather than reading directly from `registry.json`.

1. `extension list` currently reads installed state from `registry.json`; it should instead run a discovery pass and render the results, consulting `registry.json` only for preference overlays (pinned, active version).
2. `extension show` similarly should reflect what discovery finds on disk, not what the registry says.
3. The extension API is effectively a user-facing projection of `DiscoverySession` output, filtered and annotated by registry preferences.
4. Any command that mutates state (add, remove, update, pin, unpin) continues to write to `registry.json` for preference storage, but should not be required to write installed-state fields that discovery can derive at runtime.

### 4.6 Migration steps

1. Define the discovery DTOs and contract boundaries.
2. Wire extension management to read discovery metadata instead of static hints for ownership.
3. Switch help resolution to the discovery-backed model.
4. Narrow `registry.json` to user preferences (pin, active version override); remove it as the authoritative install state.
5. Add tests for stale hints, missing packages, multi-version selection, and partial discovery failures.

## 5. Implementation Steps

### 5.1 DiscoverySession contract changes

1. Extend `DiscoverySession` output to carry extension identity context (packageId, resolvedVersion, built-in vs installed) alongside each emitted `CommandMetadata`.
2. Add support for emitting contributed verbs per extension (currently `GetVerbs()` exists on `IExtensionIndex` but is not surfaced through the session output).
3. Implement `GetAllInstalledExtensions()` in `CLIAssemblyLoadingProvider` so the all-extensions discovery path works in the CLI host.
4. Validate that discovery remains metadata-only and does not execute any processor code.

### 5.2 Registry narrowing

1. Remove installed-state fields from `registry.json` writes (contributions, namespaces, verb) — these will be derived from discovery at runtime.
2. Keep: pinned state, active version override, install source, install timestamp.
3. Retire `RegistryReconciler` — its live/stale split becomes unnecessary once discovery walks the disk directly.

### 5.3 Extension API migration

1. Rewrite `extension list` to run a discovery pass and overlay registry preferences (pin, active version) onto the results.
2. Rewrite `extension show` to reflect what discovery finds on disk rather than what the registry says.
3. `extension pin/unpin`: keep registry write; switch the lookup side to discovery.
4. `extension remove`: keep registry preference removal and disk deletion; switch the lookup side to discovery.
5. `extension add/update`: no read-side change needed; continue writing preference fields to registry after install.

### 5.4 Help migration

1. Rewrite `RuntimeHelpDispatcher.RenderRoot()` to enumerate discovered extensions instead of iterating known package ids from the registry.
2. Rewrite `RuntimeHelpDispatcher.RenderExtensionHelp()` to resolve verb ownership from discovery output instead of `IExtensionResolver` + registry.
3. Keep the embedded hint table for CNF (command not found) fallback only — do not use it as the primary ownership source.
4. Update `ExtensionIndexHelpAdapter` if needed to consume the new discovery-backed index shape.

### 5.5 Tests

1. Unit tests for the new DiscoverySession output shape (identity context, contributed verbs).
2. Integration tests for `extension list` and `extension show` against a real on-disk install layout without registry entries.
3. Help rendering tests: root help with no registry entries, verb help for installed and uninstalled extensions.
4. Multi-version selection tests: active version override, fallback to latest.

## 6. Risks

1. The runtime path must stay metadata-only to avoid side effects.
2. We need to be careful not to conflate installed state with command ownership state.

## 6. Success Criteria

1. Help output is driven by actual discovered extension metadata.
2. No stale verb/package mapping is required for normal help routing.
3. `registry.json` manages pinned state and active version preferences cleanly.
4. The design can support parallel installed versions without another rewrite.

## 7. Deferred Discussion Items

1. **Root cause of stale hint table mismatch (§3.4):** Investigate how `rest -> Resources` got into the hint table and was not caught. Understand whether the build-time validation (`ExtensionHints.targets`) is sufficient or needs strengthening, and whether the hint table generation process needs to be tied more tightly to the actual installed registry.
2. **Registry as non-authoritative install state (§4.5.4):** The team direction is that `registry.json` should not be the authoritative source of installed state — runtime discovery should determine what is installed. Discuss the implications for the install/remove lifecycle, startup time, and how to handle edge cases where discovery and any persisted state diverge.
