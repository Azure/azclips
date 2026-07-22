# TEMP-PR-PLAN — `az extension list` should include bundled extensions and expose their on-disk location

> Working document for GH issue [Azure/CLIPS#478](https://github.com/Azure/CLIPS/issues/478).
> Branch: `clips-478-extension-list-incl-ext-at-<install>`. Delete this file before merging.

## 1. Issue summary

> Currently it only lists extensions from `~/.azclips/extensions/`.

The user-visible asks (from the issue title + follow-up conversation):

1. **Include extensions that exist at the install location** — `az extension list` should surface extensions that ship inside the `az` install (the "bundled" tier), not only the ones the user added via `az extension add`.
2. **Include a `location` field per row** — the absolute path to the DLL that would be loaded for that extension.

Decisions from clarification with the requester (2026-07-22):

- **Scope**: list all three tiers — Core + Bundled + Installed (every extension the CLI can dispatch).
- **`Location` value**: full absolute path to the extension DLL that would be loaded.
- **`Tier` column**: **not adding** one. The `Location` path prefix already conveys origin; a separate `Tier` column would be redundant/confusing.

## 2. Current behaviour (verified via code read)

`az extension list` is `Microsoft.Azure.Azclips.Extensions.Extension.Processors.ListExtensionsProcessor` in [src/Extensions/Extension/List.cs](src/Extensions/Extension/List.cs).

Data flow today:

1. `ListExtensionsProcessor.Process` calls [`DiscoveryBackedExtensionRegistry.BuildDiscoveredWithPreferences`](src/Extensions/Extension/DiscoveryBackedExtensionRegistry.cs).
2. That helper runs a transient `DiscoverySession` (via `IEngine.OpenDiscoverySession`) with `extensionNames: []`, which iterates **every** extension both loaders find on disk (`BuiltinExtensionLoader` scans bundled roots; `MultiVersionExtensionLoader` scans `~/.azclips/extensions/`).
3. `DiscoveryBackedExtensionRegistry` then explicitly **drops** any discovered info whose `PackageId` is in `BuiltInExtensions.Map` (the Core-tier compile-time seed) — see [DiscoveryBackedExtensionRegistry.cs](src/Extensions/Extension/DiscoveryBackedExtensionRegistry.cs#L42-L45):

    ```csharp
    if (s_builtInPackageIds.Contains(info.PackageId))
    {
        continue;
    }
    ```

4. Rows are projected into an anonymous object with `PackageId`, `Verb`, `Namespaces`, `Version`, `Pinned`, `Source`, `InstalledAt`. **No filesystem path is exposed today.**

Consequences today:

- Bundled-but-not-Core packages like `Resources` / `Compute` **do** show up (they aren't in `BuiltInExtensions.Map`), but they _look_ installed — no indication they came from the bundled dir.
- Core-tier packages that are also bundled (`Login`, `Config`, `Agent`, `Extension`) are hidden entirely by the filter above. That is why the issue reporter perceives "only shows extensions from `~/.azclips/extensions/`".
- The pre-existing `Source` field is the **NuGet feed source string** (from `registry.json`), not a disk path. Do not overload it.

### Terminology (from `BuiltInExtensions.cs` XML doc)

| Tier          | Where it lives                                                                          | Loader                        |
| ------------- | --------------------------------------------------------------------------------------- | ----------------------------- |
| **Core**      | Compile-time seed in `BuiltInExtensions.Map` — dispatch works even with no DLL on disk. | Static map                    |
| **Bundled**   | `<install>/Extensions/<pkg>/` (packaged) or `artifacts/<Config>/` (dev builds).         | `BuiltinExtensionLoader`      |
| **Installed** | `~/.azclips/extensions/<pkg>/<ver>/`.                                                   | `MultiVersionExtensionLoader` |

## 3. Proposed change

### 3.1 Behaviour

- Remove the Core-tier package-id filter from `DiscoveryBackedExtensionRegistry.BuildDiscoveredWithPreferences` so Core/Bundled entries flow through the same projection as Installed.
- Add a `Location` (absolute DLL path) to each row emitted by `extension list` (and, for symmetry, `extension show`).
- Best-effort: if a Core-tier entry from `BuiltInExtensions.Map` is not discovered on disk (broken install), still emit a row for it with `Location = null` and `Version = null` so users see it exists. **Open question — see §6.**

### 3.2 Output shape (additive, back-compat)

Current row (JSON):

```jsonc
{
    "PackageId": "Microsoft.Azure.Azclips.Extensions.Resources",
    "Verb": "group",
    "Namespaces": ["group"],
    "Version": "1.2.3",
    "Pinned": false,
    "Source": null,
    "InstalledAt": "2026-07-01T12:00:00Z",
}
```

New row (JSON):

```jsonc
{
    "PackageId": "Microsoft.Azure.Azclips.Extensions.Resources",
    "Verb": "group",
    "Namespaces": ["group"],
    "Version": "1.2.3",
    "Pinned": false,
    "Source": null,
    "InstalledAt": "2026-07-01T12:00:00Z",
    "Location": "C:\\Program Files\\azclips\\Extensions\\Microsoft.Azure.Azclips.Extensions.Resources\\Microsoft.Azure.Azclips.Extensions.Resources.dll",
}
```

`--by-verb` mode: same additive change — the nested `ContributorRow` records gain `Location`.

## 4. Where the DLL path has to come from

Assemblies are loaded via `AssemblyLoadContext.LoadFromStream` in both [`BuiltinExtensionLoader.ExtensionLoadContext.LoadManagedAssembly`](src/CLI/AssemblyLoading/ExtensionLoader.cs#L63) and [`MultiVersionExtensionLoader.ExtensionLoadContext.LoadManagedAssembly`](src/CLI/AssemblyLoading/ExtensionLoader.cs#L347) (deliberate — enables shadow-copy). That makes `Assembly.Location` an **empty string** at runtime, so we cannot read it off the loaded assembly.

Options considered:

| Option                                                                                                                                                                    | Pros                                                                                         | Cons                                                                                                                                                                                         |
| ------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | -------------------------------------------------------------------------------------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| **A**: Extend `IExtensionLoader.GetInstalledExtensions()` to yield `(Name, Version, Path)` and thread `Path` through `InstalledExtensionRef` → `DiscoveredExtensionInfo`. | Single, honest source of truth. Path is known exactly at the point the loader enumerates it. | Touches `Core.Abstractions.InstalledExtensionRef` (public record) and the `IExtensionLoader` interface — minor API break for any downstream loader impl.                                     |
| **B**: Have `LoadExtension` return `(Assembly, Path)` and capture during discovery load.                                                                                  | Symmetric with A.                                                                            | Bigger blast radius: every caller of `LoadExtension` changes.                                                                                                                                |
| **C**: Scrape `ExtensionLoadTracker` records.                                                                                                                             | Zero API change.                                                                             | Side-effect based; tracker is a Debug-only log aggregator, not a queryable store; would require restructuring `ExtensionLoadTracker` and coupling `List.cs` to CLI-internal state. Rejected. |

**Pick: Option A**. It's the smallest surface change with the clearest ownership: the loader is already the authority on where a package lives on disk. Concrete edits below.

## 5. Implementation plan (proposed file-by-file)

> Read-only investigation is done. Below is the change list — will be executed sequentially, not all at once.

### 5.1 Core abstractions

- [`src/Core/Core.Abstractions/Models/InstalledExtensionRef.cs`](src/Core/Core.Abstractions/Models/InstalledExtensionRef.cs)
    - Add `string? Location` to the record: `sealed record InstalledExtensionRef(string Name, string Version, string? Location = null)`. Default `null` preserves back-compat with any external impls.
- [`src/Core/Core.Abstractions/Models/DiscoveredExtensionInfo.cs`](src/Core/Core.Abstractions/Models/DiscoveredExtensionInfo.cs)
    - Add `string? Location` to the record. Update the XML `<param>` block.

### 5.2 Loaders

- [`src/CLI/AssemblyLoading/ExtensionLoader.cs`](src/CLI/AssemblyLoading/ExtensionLoader.cs)
    - `IExtensionLoader.GetInstalledExtensions()` — widen default return to include `Location`; update both `BuiltinExtensionLoader.GetInstalledExtensions()` and `MultiVersionExtensionLoader.GetInstalledExtensions()` to emit the full DLL path (they already compute `extensionPath` internally).
- [`src/CLI/AssemblyLoading/CLIAssemblyLoadingProvider.cs`](src/CLI/AssemblyLoading/CLIAssemblyLoadingProvider.cs)
    - `GetAllInstalledExtensions()` — construct `InstalledExtensionRef` with the `Location` from the loader.

### 5.3 Discovery + projection

- [`src/Core/Core.Engine/DiscoverySession.cs`](src/Core/Core.Engine/DiscoverySession.cs)
    - In the "all installed extensions" branch of `RunAsync`, propagate `info.Location` into the emitted `DiscoveredExtensionInfo`.
- [`src/Extensions/Extension/DiscoveryBackedExtensionRegistry.cs`](src/Extensions/Extension/DiscoveryBackedExtensionRegistry.cs)
    - **Remove** the `s_builtInPackageIds.Contains(info.PackageId)` skip so Core/Bundled rows flow through. Keep the field but only use it if we decide to tag rows differently (we're not, per §1 decision — safe to delete).
    - Thread `Location` into whatever we hand to `ListExtensionsProcessor`. Options:
        - Add `Location` to `ExtensionRegistryEntry` **as a `[JsonIgnore(WhenWriting)]` runtime-only field** (do not persist — path is derived per-invocation and installs can move).
        - OR bypass the registry entry and thread a parallel `Dictionary<packageId, Location>` down to `List.cs` via a new return type. Simpler but adds surface.
    - Recommend: runtime-only field on `ExtensionRegistryEntry` with `[JsonIgnore(Condition = JsonIgnoreCondition.Always)]` writer-side; the entry class is already the shared carrier for `Show` + `List`.

### 5.4 List/Show processors

- [`src/Extensions/Extension/List.cs`](src/Extensions/Extension/List.cs)
    - Add `Location = kvp.Value.Location` to the anonymous projection.
    - Add `Location` to `LiveContributor` and `ContributorRow` records (public — used by tests).
    - Update `PivotByVerb` to pass `Location` through.
- [`src/Extensions/Extension/Show.cs`](src/Extensions/Extension/Show.cs)
    - Add `Location` to `BuildResult`'s anonymous object for consistency.

### 5.5 Docs

- [`src/CLI/Help/Docs/extension/list.md`](src/CLI/Help/Docs/extension/list.md) — mention the new `Location` field and clarify that Core/Bundled extensions are now listed.
- [`src/CLI/Help/Docs/extension/show.md`](src/CLI/Help/Docs/extension/show.md) — same `Location` mention (need to verify file exists).

### 5.6 Tests

- [`tests/Cli.Tests/Extensions/ExtensionListByVerbTests.cs`](tests/Cli.Tests/Extensions/ExtensionListByVerbTests.cs)
    - Update the `C(...)` helper to seed `Location`; add a case asserting `Location` round-trips through `PivotByVerb`.
- Add a targeted test that Core-tier packages (e.g. `Microsoft.Azure.Azclips.Extensions.Login`) are no longer filtered out of the list output when discovered on disk.
- Add a loader-level test that `GetInstalledExtensions()` populates `Location` correctly for both bundled and installed roots.
- Search for and update any snapshot/JSON-shape assertions for `extension list` output (grep pending during impl).

## 6. Open decisions

1. **Compile-time-only Core rows** — if `Login` is in `BuiltInExtensions.Map` but no `Login.dll` is discovered on disk (broken install), do we synthesize a row with `Location = null`? Currently leaning **yes**, because the user said "every extension the CLI can dispatch" and dispatch does still route to a Core seed even without a DLL (unclear — worth verifying whether dispatch actually works when DLL is missing).
2. **Persistence** — confirm we do **not** persist `Location` into `registry.json` (paths shift between installs; the file is portable). Plan above assumes runtime-only.
3. **PowerShell surface parity** — the `Cmdlets` project mirrors CLI verbs. Need to check whether `Get-AzExtension` (or equivalent) also needs the `Location` property added. TBD during impl.
4. **OpenSpec** — this repo uses OpenSpec for change-tracking (see [openspec/](openspec/)). Should we author a delta spec under `openspec/changes/list-extension-location/` before writing code, or is the impl small enough to skip and just update `docs/spec/extension-framework.md` in the PR? Deferring the call to the requester.

## 7. Out of scope

- Changing the physical layout of bundled extensions.
- Changing what `az extension add` writes to `registry.json` (schema stays v2).
- Adding a filter flag like `--source bundled|installed` — worth a follow-up but not required by the issue.
- Renaming the `BuiltIn*` types to `Bundled*` (pre-existing tech debt called out in `BuiltInExtensions.cs`; separate PR per its own comments).

## 8. Validation checklist (to run before opening PR)

- [ ] `dotnet build azclips.slnx`
- [ ] `dotnet test tests/Cli.Tests/Cli.Tests.csproj`
- [ ] `dotnet test tests/Core.Extensions.Tests/Core.Extensions.Tests.csproj`
- [ ] `dotnet test tests/Core.Engine.Tests/Core.Engine.Tests.csproj`
- [ ] Manual: `az extension list` on a Debug build shows Core+Bundled entries with `Location` populated to a path under `artifacts/Debug/…`.
- [ ] Manual: `az extension list --by-verb` shows same `Location` under each verb group.
- [ ] Manual: `az extension list --output json | ConvertFrom-Json` produces valid objects with the new field.
- [ ] Registry file (`~/.azclips/extensions/registry.json`) does **not** grow a `location` key after running `list` (Location is runtime-only).
