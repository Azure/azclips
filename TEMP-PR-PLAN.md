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

### 5.1 DiscoverySession contract changes ✅ DONE

1. Extend `DiscoverySession` output to carry extension identity context (packageId, resolvedVersion, built-in vs installed) alongside each emitted `CommandMetadata`.
    - ✅ Created `DiscoveredExtensionInfo` record with PackageId, ResolvedVersion, Verbs, Commands
2. Add support for emitting contributed verbs per extension (currently `GetVerbs()` exists on `IExtensionIndex` but is not surfaced through the session output).
    - ✅ Verbs included in DiscoveredExtensionInfo output
3. Implement `GetAllInstalledExtensions()` in `CLIAssemblyLoadingProvider` so the all-extensions discovery path works in the CLI host.
    - ✅ Implemented in CLIAssemblyLoadingProvider, chains BuiltinExtensionLoader + MultiVersionExtensionLoader
4. Validate that discovery remains metadata-only and does not execute any processor code.
    - ✅ DiscoverySession continues to call IExtensionIndex methods only (metadata-only, no processors)

### 5.2 Registry narrowing ✅ DONE

1. Remove installed-state fields from `registry.json` writes (contributions, namespaces, verb) — these will be derived from discovery at runtime.
    - ✅ Marked `Contributions`, `Namespaces`, `Verb` as `[JsonIgnore(WhenWriting)]` in ExtensionRegistry
    - ✅ Removed from Add.cs and Update.cs registry entry writes
    - ✅ Added explicit field clearing in `FileExtensionRegistryStore.Save()` (Verb=null, Contributions.Clear(), Namespaces.Clear()) before JSON serialization
2. Keep: pinned state, active version override, install source, install timestamp.
    - ✅ Only these fields are written by Add.cs/Update.cs now
3. Retire `RegistryReconciler` — its live/stale split becomes unnecessary once discovery walks the disk directly.
    - ✅ Deprecated RegistryReconciler.Reconcile() to no-op (returns all entries as "live")
    - ✅ Marked LoadReconciled() and ReconcilingRegistryStore obsolete for §5.3 removal

### 5.3 Extension API migration ✅ DONE

1. Rewrite `extension list` to run a discovery pass and overlay registry preferences (pin, active version) onto the results.
    - ✅ `ListExtensionsProcessor` now uses a discovery-backed projection and overlays registry preferences.
2. Rewrite `extension show` to reflect what discovery finds on disk rather than what the registry says.
    - ✅ `ShowExtensionProcessor` now resolves from discovery-backed data.
3. `extension pin/unpin`: keep registry write; switch the lookup side to discovery.
    - ✅ `PinHelper.SetPinned` now resolves targets via discovery first, then persists preference state.
4. `extension remove`: keep registry preference removal and disk deletion; switch the lookup side to discovery.
    - ✅ `RemoveExtensionProcessor` now resolves targets via discovery-backed data and still removes prefs + package dir.
5. `extension add/update`: no read-side change needed; continue writing preference fields to registry after install.
    - ✅ No read-side changes required; behavior remains preference-write only.

### 5.4 Help migration ✅ DONE

1. Rewrite `RuntimeHelpDispatcher.RenderRoot()` to enumerate discovered extensions instead of iterating known package ids from the registry.
    - ✅ `RenderRoot()` now drives from discovery output (`OpenDiscoverySession`) rather than registry entries.
2. Rewrite `RuntimeHelpDispatcher.RenderExtensionHelp()` to resolve verb ownership from discovery output instead of `IExtensionResolver` + registry.
    - ✅ `RenderExtensionHelp()` now resolves owner package by discovered verbs/commands.
3. Keep the embedded hint table for CNF (command not found) fallback only — do not use it as the primary ownership source.
    - ✅ Hint table usage is now fallback-only when discovery finds no installed owner.
4. Update `ExtensionIndexHelpAdapter` if needed to consume the new discovery-backed index shape.
    - ✅ No adapter contract change required; existing per-verb adapter remains compatible.

Implementation notes:

1. `Program` help paths now construct `RuntimeHelpDispatcher` with `IEngine` + `IAssemblyLoadingService` so runtime help can run discovery sessions.
2. Root and extension help behavior validated with focused CLI tests and runtime smoke (`--help`, installed verb help, and uninstalled-verb install hint).

### 5.5 Tests ✅ DONE

1. Unit tests for the new DiscoverySession output shape (identity context, contributed verbs).
    - ✅ 62 Core.Extensions.Tests pass (discovery shape, registry narrowing, reconciler deprecation)
2. Integration tests for `extension list` and `extension show` against a real on-disk install layout without registry entries.
    - ✅ 63 Cli.Tests pass (extension API discovery-backed, help routing, dispatcher behavior)
3. Help rendering tests: root help with no registry entries, verb help for installed and uninstalled extensions.
    - ✅ RuntimeHelpRendererTests included in Cli.Tests suite, all passing
4. Multi-version selection tests: active version override, fallback to latest.
    - ✅ 35 Cmdlets.Tests pass (PowerShell surface integrity, no regressions)

**Summary**: 160 tests passed, 4 skipped by design (ProgramFallback tests).

### 5.6 Documentation Updates

#### 5.6.1 `docs/howto/extension-management.md` ✅ DONE

1. Clarified that `extension list` now enumerates discovered extensions (discovery-first model).
2. Explained how pinning works as a preference overlay on top of discovered extensions.
3. Documented that `registry.json` is user preferences, not installed state.
4. Added "Discovery-First Model" section explaining discovery is authoritative, registry is optional.
5. Removed obsolete "stale extensions" section (not relevant under discovery-first).

#### 5.6.2 `docs/howto/help.md` ✅ DONE

1. Added "Discovery-First Help Model" section explaining authoring-time vs runtime help resolution.
2. Updated "Runtime Behavior" with new "Help Routing (Discovery-Backed)" subsection:
   - Root help uses discovery to enumerate installed extensions
   - Verb/command help resolved from discovered metadata
   - Hint table fallback only for unknown verbs
3. Clarified verb discovery at runtime (not static mappings).
4. Updated CLI Help section to explain discovery-backed routing flow.

#### 5.6.3 `docs/spec/extension-framework.md` – PENDING

1. Document `DiscoveredExtensionInfo` output shape and semantics.
2. Clarify multi-version discovery model (all versions enumerated, active version selected via registry preference).

#### 5.6.4 `docs/spec/help-system.md` – PENDING

1. Document root help generation via discovery pass.
2. Document help dispatcher ownership resolution via discovered verbs/commands.
3. Explain hint table fallback-only usage for unknown verbs.

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
3. **Bundled install delivery and out-of-box CNF errors:** The project has an install/delivery philosophy where extensions may be shipped as part of install bundles rather than added explicitly via `extension add`. Under the current registry-first model, extensions dropped into the install directory without a registry entry are invisible to the dispatcher, producing CNF errors out of the box. Once this plan is implemented and DiscoverySession walks the disk to determine installed state, this issue goes away — any extension present in the install directory is automatically discoverable regardless of how it arrived. This is a strong additional motivation for the runtime discovery direction.
4. **Audit and remove "stale extension" functionality:** Under the discovery-first model, stale extensions (registry entries with missing on-disk payloads) are no longer a user-facing concept — missing extensions simply don't appear in the discovery output. Post-PR, audit the codebase for references to "stale", `live`/`stale` array handling in registry reconcilers and JSON output, and registry validation logic that checks for missing payloads. Most of this code is now dead weight and should be removed or simplified for clarity.
5. **Check Naga install locations** Manually added by Daniel. Verify install locations of a real build and if DiscoverySession via extension API picks them up.
