# TEMP-PR-PLAN — CLIPS#522 / azclips#473

Working notes for the PR that fixes local Debug/Release extension dispatch.

Related issues:
- [Azure/CLIPS#522 — Extensions not discoverable in local `debug` or `release` builds](https://github.com/Azure/CLIPS/issues/522) (primary)
- [Azure/azclips#473 — Verify "Built-in" extension discovery](https://github.com/Azure/azclips/issues/473) (duplicate; also references Yabo's original repro)

Branch: `clips-522-ext-local-discovery`

---

## 1. TL;DR

Discovery walks the local build root (`artifacts/<Config>/`) and correctly enumerates every extension it finds (`az extension list` proves this). Dispatch then calls `LoadExtension(name, discoveredVersion)` — and the built-in loader **hard-refuses any versioned request**, forcing the call over to the multi-version loader, which only looks under `~/.azclips/extensions/`. If the same extension is not also installed there, the load fails with:

```
[ERROR] Extension 'Resources' could not be found by any extension loader.
```

Help works by accident: [DiscoverySession.LoadAssemblyWithFallback](src/Core/Core.Engine/DiscoverySession.cs#L183) retries with an empty version when the versioned load misses. `ExecutionSession` has no such fallback, so command dispatch dies.

Fix: teach `BuiltinExtensionLoader.LoadExtension` to honor a versioned request when the on-disk package matches — the same shape `MultiVersionExtensionLoader.HasInstalledVersion` already uses.

---

## 2. Repro (local, Windows)

Set-up (any workstation that has run `az extension add` at least once):

- `dotnet build azclips.slnx` (Debug)
- `~/.azclips/extensions/` contains a registry entry for **only** one of the built extensions (e.g. Compute) — Resources is *not* installed here
- Verify:
  ```powershell
  Get-Content $env:USERPROFILE\.azclips\extensions\registry.json
  # -> only Compute
  Get-ChildItem $env:USERPROFILE\.azclips\extensions
  # -> only Compute
  ```

Then, from the Debug output dir:

```powershell
cd artifacts\Debug\Core.Cli
.\az.exe extension list                # both Compute + Resources appear
.\az.exe vm list --subscription-id X   # works (Compute is user-installed)
.\az.exe group list                    # FAILS: "Extension 'Resources' could not be found by any extension loader."
.\az.exe group list --help             # works — help discovery has a fallback
```

Same behavior on the Release output dir. Delete `~/.azclips/extensions/registry.json` (or purge the folder) and *both* verbs from local build become undispatchable while `az extension list` and `--help` keep working.

This matches Yabo's original description: help works, extensions listed, but dispatch fails. Daniel could not reproduce because his `~/.azclips/extensions/` happened to shadow every locally built extension.

---

## 3. Why help works and dispatch doesn't

Two code paths, one fallback.

**Help path** — [`RuntimeHelpDispatcher.DiscoverInstalledExtensions()`](src/CLI/Help/RuntimeHelpDispatcher.cs#L242) opens a transient `DiscoverySession`, which enumerates via `LoadAllAssemblies()` and loads each index through [`LoadAssemblyWithFallback`](src/Core/Core.Engine/DiscoverySession.cs#L183):

```csharp
try
{
    return new LoadedAssembly(LoadAssembly.Invoke(extensionName, versionToLoad), versionToLoad);
}
catch (InvalidOperationException ex) when (
    !string.IsNullOrWhiteSpace(versionToLoad) &&
    IsVersionedLoadMiss(ex))
{
    // Retry unversioned so the built-in loader (which refuses versioned loads) can serve it.
    return new LoadedAssembly(LoadAssembly.Invoke(extensionName, string.Empty), string.Empty);
}
```

The comment on that catch is a direct acknowledgment that the built-in loader refuses versioned loads. Help swallows the miss and retries with `""`, at which point [`BuiltinExtensionLoader.LoadExtension`](src/CLI/AssemblyLoading/ExtensionLoader.cs#L123) succeeds against the Debug root.

**Dispatch path** — [`ExtensionDispatcher.Parse`](src/CLI/Parser/ExtensionDispatcher.cs#L44) resolves the verb through the [`LeafIndex` built with discovery](src/CLI/CliExtensionServiceRegistration.cs#L60), which stamps the version from `DiscoveredExtensionInfo.ResolvedVersion` (parsed from the .nupkg by [`BuiltinExtensionLoader.ResolvePackageVersionFromNupkg`](src/CLI/AssemblyLoading/ExtensionLoader.cs#L191)). The resulting `CommandInvocation.ModuleVersion = "0.1.0"` flows into [`ExecutionSession.LoadIndex`](src/Core/Core.Engine/ExecutionSession.cs#L52) which calls `LoadAssembly(name, "0.1.0")` — no fallback, no retry. The load chain is:

1. `BuiltinExtensionLoader.LoadExtension("Resources", "0.1.0")` — early-returns `null` on the version check:
   ```csharp
   if (!string.IsNullOrEmpty(version))
   {
       return null;
   }
   ```
2. `MultiVersionExtensionLoader.LoadExtension("Resources", "0.1.0")` — walks `~/.azclips/extensions/(Microsoft.Azure.Azclips.Extensions.)Resources/0.1.0/`, nothing there, returns `null`.
3. `CLIAssemblyLoadingProvider.LoadExtension` throws `"Extension '{name}' could not be found by any extension loader."`

So the built-in root that *just* satisfied discovery is deliberately excluded from dispatch loads.

### Git archaeology

The early-return dates back to [`b8466776d6` — "Combine CLIPS Engines (#195)"](https://github.com/Azure/CLIPS/pull/195). At that time `BuiltinExtensionLoader` had no version-awareness at all, so refusing versioned loads was the only correct behavior — versioned installs belonged exclusively to the multi-version loader. That premise stopped being true once `GetInstalledExtensions()` grew nupkg parsing (later commit trail visible on the file). We just never updated `LoadExtension` to match.

### Why the "true core" verbs (login/config/extension/agent) don't hit this

They're seeded in [`BuiltInExtensions.Map`](src/Core/Core.Extensions/BuiltInExtensions.cs#L45) with hard-coded leaves — dispatch resolves them through the static map with `Version = null`, so `LoadExtension` is called with an empty version, and the built-in loader's early-return does not fire. Compute and Resources are pure discovery-driven contributions (no seed entry), so they are the only ones that get versioned dispatch and thus the only ones that break.

---

## 4. Fix

### Chosen fix (minimal, in scope)

Make `BuiltinExtensionLoader.LoadExtension` accept a versioned request when the on-disk package advertises that exact version. Mirror the shape of [`MultiVersionExtensionLoader.HasInstalledVersion`](src/CLI/AssemblyLoading/ExtensionLoader.cs#L363).

Sketch (new logic replacing lines 123-149 of [ExtensionLoader.cs](src/CLI/AssemblyLoading/ExtensionLoader.cs)):

```csharp
public Assembly? LoadExtension(string extensionName, string? version = null)
{
    foreach (var root in GetCandidateRoots(_options.BuiltinInstallationDir))
    {
        foreach (var directoryName in GetCandidateDirectoryNames(extensionName, _options.ExtensionFolderPrefix))
        {
            var packageDir = Path.Combine(root, directoryName);
            if (!Directory.Exists(packageDir))
            {
                continue;
            }

            // Versioned request: only serve if the on-disk .nupkg matches. Trust the caller for unversioned.
            if (!string.IsNullOrEmpty(version))
            {
                var discovered = ResolvePackageVersionFromNupkg(packageDir, directoryName, _loggingService);
                if (!string.Equals(discovered, version, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
            }

            foreach (var assemblyName in GetCandidateAssemblyNames(extensionName))
            {
                var extensionPath = Path.Combine(packageDir, $"{assemblyName}.dll");
                if (!File.Exists(extensionPath))
                {
                    continue;
                }

                var loadContext = new ExtensionLoadContext(extensionPath, _loggingService);
                return loadContext.LoadManagedAssembly(extensionPath);
            }
        }
    }

    _loggingService?.Debug($"Extension '{extensionName}' (version '{version}') not found in builtin extensions directory. Checked roots: '{string.Join(";", GetCandidateRoots(_options.BuiltinInstallationDir))}'");
    return null;
}
```

Properties this preserves:

- **User-installed shadowing still works.** If a user does `az extension add Resources --version 2.0.0`, the leaf index gets `Version = "2.0.0"`, the built-in loader's nupkg version check fails (it has `0.1.0`), and the multi-version loader takes over — same as today.
- **Unversioned loads keep going to the built-in loader.** The `az login`-style path where `Version = null` still returns on the first `File.Exists` hit, no nupkg parse needed.
- **No shadow risk for released bits.** In released packages, `AssemblyLoadingOptions.BuiltinInstallationDir` is `AppContext.BaseDirectory\Extensions`. That directory only exists on installs that ship built-ins alongside the host, and the versions there are baked at packaging time — matches what discovery reports.

### Alternatives considered

1. **Add the `LoadAssemblyWithFallback` retry to `ExecutionSession`.** Would fix dispatch, but it hides the real design flaw and duplicates the transient/help fallback logic. Also: the retry silently downgrades the requested version, which is fine for local dev but hostile in production when we actually care about serving a pinned version.
2. **Drop the version stamp for built-in-sourced discovered entries** (make `BuildLeafIndexWithDiscovery` write `Version = null` when the source loader is `BuiltinExtensionLoader`). Fixes dispatch but breaks `az extension list` version reporting, and requires plumbing "which loader yielded this" up through `IAssemblyLoadingProvider`.
3. **Order the loaders multi-version first.** Doesn't help — multi-version still returns null for `~/.azclips/extensions/Resources/0.1.0/` when nothing is there.
4. **Have `MultiVersionExtensionLoader` scan the build output roots too.** Blurs the two loaders' responsibilities and would let a stale build dir shadow user pins in production. Rejected.

### Follow-ups (out of scope, worth filing)

- The nested `foreach (candidateName) foreach (candidatePath) foreach (assemblyName)` in `BuiltinExtensionLoader.LoadExtension` re-computes candidates already known from `GetInstalledExtensions()`. A single enumeration cached at construction time would let both methods share truth. Not this PR.
- `AssemblyLoadingOptions.GetDefaultBuiltinInstallationDir` differs by `#if DEBUG` (`AppContext.BaseDirectory\..`) vs release (`AppContext.BaseDirectory\Extensions`). Worth documenting the `GetCandidateRoots` fallback list ([ExtensionLoader.cs#L108](src/CLI/AssemblyLoading/ExtensionLoader.cs#L108)) — currently the code silently probes `..`, `..\..\Debug`, `..\..\Release` on all builds.

---

## 5. Tests

New unit tests in [tests/Cli.Tests/AssemblyLoading/BuiltinExtensionLoaderTests.cs](tests/Cli.Tests/AssemblyLoading/BuiltinExtensionLoaderTests.cs):

1. `LoadExtension_ShouldReturnAssembly_WhenVersionMatchesNupkg` — arrange a fake built-in root with `Microsoft.Azure.Azclips.Extensions.Foo/Foo.dll` + `Microsoft.Azure.Azclips.Extensions.Foo.1.2.3.nupkg`, call `LoadExtension("Foo", "1.2.3")`, expect non-null.
2. `LoadExtension_ShouldReturnNull_WhenVersionDoesNotMatchNupkg` — same layout, call `LoadExtension("Foo", "9.9.9")`, expect null (so multi-version loader can take over).
3. `LoadExtension_ShouldReturnAssembly_WhenVersionIsNullOrEmpty` — regression guard on unversioned load.
4. Skip nupkg entirely (only DLL present) + versioned request → returns null (can't confirm identity → defer to next loader).

Integration coverage:

5. Extend [tests/Cli.Tests/AssemblyLoading/ExtensionSelectionPathOverrideTests.cs](tests/Cli.Tests/AssemblyLoading/ExtensionSelectionPathOverrideTests.cs) with a scenario where a pinned user-installed version (e.g. `2.0.0` in `~/.azclips`) shadows a locally-built `0.1.0` in the builtin root — assert dispatch still lands in the multi-version loader.
6. Add an end-to-end style test in [tests/Cli.Tests/Extensions/ExtensionDispatcherTests.cs](tests/Cli.Tests/Extensions/ExtensionDispatcherTests.cs) that stages a fake extension in a temp "builtin root", clears the user extensions dir, and asserts `dispatcher.Parse(["group", "list"])` produces a `CommandInvocation` whose subsequent `LoadExtension` returns the assembly (mirrors the failing scenario).

Manual validation:

- `.\artifacts\Debug\Core.Cli\az.exe group list` → succeeds (parser error is fine, load must succeed).
- `.\artifacts\Debug\Core.Cli\az.exe vm list --subscription-id X` → still succeeds.
- `.\artifacts\Debug\Core.Cli\az.exe extension list` → unchanged output shape.
- Repeat with `Release` config.

---

## 6. Risk / rollout

- Scope: single file (`ExtensionLoader.cs`) + tests. No public surface changes.
- No config schema changes.
- No behavior change on packaged/released `az` (built-in root already matches nupkg version at packaging time).
- Local dev impact: previously-invisible extensions become dispatchable — no removals.
- PowerShell modules: unaffected — PS uses `Import-Module` against manifests in `src/PS/`, not this loader chain.

---

## 7. Docs / references touched

- [docs/howto/config.md](docs/howto/config.md) — `assemblyLoading.extensionFolderPrefix` doc is still accurate; no edit needed.
- [docs/spec/extension-framework.md §3 & §4](docs/spec/extension-framework.md) — describes discovery walking `~/.azclips/extensions/` + builtin `Extensions/`; still accurate after the fix. Consider a one-line clarifier under §4 stating "built-in loader honors versioned requests when the on-disk .nupkg matches — required for `dotnet build`-style local dev".
- Wiki: [02-deep-dive/extension-system.md](wiki/02-deep-dive/extension-system.md) diagram at line 34 stays correct.

---

## 8. PR body draft

> Fixes Azure/CLIPS#522. Also covers the duplicate Azure/azclips#473 (Yabo's original repro).
>
> `BuiltinExtensionLoader.LoadExtension` refused every versioned load (short-circuit at the top). Discovery, however, has always stamped a version onto every extension it finds — including ones it discovers under the built-in root (`artifacts/<Config>/` in local dev, `AppContext.BaseDirectory/Extensions` when packaged). Dispatch therefore called `LoadExtension(name, discoveredVersion)`, which the built-in loader declined, forcing the load onto `MultiVersionExtensionLoader`, which only knows about `~/.azclips/extensions/`. On dev machines without a matching user-install, dispatch died with *"Extension 'X' could not be found by any extension loader"* while `az extension list` and `az X --help` (which have a versioned-load fallback) kept working — a confusing split that made this look like a discovery bug.
>
> This PR lets the built-in loader honor a versioned request when the on-disk `.nupkg` in the same directory reports the matching version — the same identity check `MultiVersionExtensionLoader.HasInstalledVersion` already performs. User-installed shadows and pinned versions continue to route through the multi-version loader unchanged.

---

## 9. Open questions

- [ ] Should we also collapse the help-side `LoadAssemblyWithFallback` (now redundant for the built-in loader) or leave it as defense-in-depth for third-party `IExtensionLoader` implementations? Leaning **leave it** — it's cheap insurance and there is a comment describing why.
- [ ] `AssemblyLoadingOptions.ExtensionFolderPrefix` is `""` in Release builds. Does that break the versioned identity check for packaged built-ins where the folder name has no prefix? Confirm by inspecting a packaged artifact under `artifacts/Release/Extensions/` — the `ResolvePackageVersionFromNupkg(packageDir, directoryName, ...)` call passes the directory name as the packageId, and the nupkg identity check inside `TryReadPackageIdentityVersion` compares `identity.Id` (which is always the full `Microsoft.Azure.Azclips.Extensions.X` in packaged nupkgs) to `directoryName`. Need to test with a stripped-prefix folder before landing.
