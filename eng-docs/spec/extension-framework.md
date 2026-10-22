# Design Spec: Extension Framework

## Overview

Azclips ships as both a standalone CLI (`az`) and a set of PowerShell modules. Both surfaces share the same extension assemblies and core engine, but differ in how they build, package, load extensions, and manage assembly isolation.

> Items marked **[Proposal]** describe planned changes not yet implemented on `main`.
> Items marked **[CLI]** or **[PS]** are specific to that surface.

---

## 1. Architecture at a Glance

Azclips produces three categories of build artifact:

- **Core CLI** (`az.exe`) — Self-contained host process bundling the engines, AI agent, DI container, and auth
- **Extensions** (`.nupkg` / DLL) — Dynamically loaded at runtime, carrying their own dependencies
- **PowerShell Modules** (`.psd1` / `.psm1`) — Script-based wrappers that drive the same .NET engine and extensions

Two host surfaces consume the same extensions:

|                       | CLI                            | PowerShell                                     |
| --------------------- | ------------------------------ | ---------------------------------------------- |
| **Entry point**       | `az.exe` → `Program.cs`        | `Import-Module` → `.psm1` → exported functions |
| **Engine**            | `CLIEngine`                    | `PSEngine`                                     |
| **Extension loading** | File-based, into isolated ALCs | Pre-loaded via manifest, into shared ALC       |
| **Output**            | `Console.WriteLine`            | `$PSCmdlet.WriteObject` via channel            |

---

## 2. Extensions

### 2.1 What an Extension Is

An extension is a .NET class library that implements a set of commands for one Azure service area (e.g., Accounts, Resources). Every extension must provide:

- **`ExtensionIndex`** — implements `IExtensionIndex`, returns `ICommandMetadata` for a given command name and client type
- **Processor classes** — implement `IProcessor` with `Init(context)` and `ProcessAsync()`
- **Dual command tables** — the index maps CLI command names (e.g., `create`) and PowerShell command names (e.g., `New-AzResourceGroup`) to the same `IProcessor` types

### 2.2 How to Build an Extension

Key `.csproj` conventions:

- `EnableDynamicLoading=true` — produces `.deps.json` / `.runtimeconfig.json` for runtime dependency resolution
- `GeneratePackageOnBuild=true` — produces a `.nupkg` for distribution
- `Core.Abstractions` referenced as **compile-only** (`Private=false`, `ExcludeAssets=runtime`) — shared contracts come from the host at runtime, not the extension's output

All outputs land in `artifacts/{Configuration}/{ExtName}/` controlled by `Shared.props`.

> **[Proposal]** Extensions should depend **only** on `Core.Abstractions`. References to `Core.Engine` and `Core.Implementation` would be removed.

> **[Proposal]** A Roslyn incremental source generator (`Core.Index.Generator`) auto-generates `ExtensionIndex` from `[ProcessorInfo]` attributes, eliminating hand-maintained command tables.

### 2.3 Extension tiers

Azclips distinguishes two tiers of extension by _how they get onto the machine_, not by their package format:

| Tier          | How it gets there                                                                                                                            | Where it lives at runtime                                           | Removable?                    |
| ------------- | -------------------------------------------------------------------------------------------------------------------------------------------- | ------------------------------------------------------------------- | ----------------------------- |
| **Bundled**   | Shipped alongside `az.exe` at packaging time (`<install>/Extensions/<PackageId>/`) or dropped there by a local build (`artifacts/<Config>/`) | Loaded by `BundledExtensionLoader`                                  | No — shipped with the install |
| **Installed** | Placed by `az extension add`                                                                                                                 | `~/.azclips/extensions/<PackageId>/<Version>/`; multi-version aware | Yes — `az extension remove`   |

The two tiers are **independent**: an extension can appear in either or both. Dispatch is uniform across both — no runtime distinction between "Core" and other extensions. The set of extensions expected to ship in every install lives in the packaging profile ([`inject_builtin_extensions.sh`](../../devops/scripts/packaging/common/inject_builtin_extensions.sh) with `lean_package.config` / `full_package.config`), not the runtime.

- **Bundled** membership depends on the packaging profile. Full installs typically ship every packaged extension's DLL under `<install>/Extensions/`; lean installs ship a subset. Local `dotnet build` output puts every extension DLL under `artifacts/<Config>/`.
- **Installed** membership is entirely user-driven — whatever they've run `az extension add` for.

When the same `PackageId` appears in both the Bundled and Installed roots at different versions, [`ExtensionCandidateSelector`](../../src/Core/Core.Extensions/ExtensionCandidateSelector.cs) picks the winner — see §3.4.

#### Write boundary

The extension API (`az extension add` / `remove` / `update` / `pin` / `unpin`) reads and writes **only** under the Installed root (`~/.azclips/extensions/`, or whatever `assemblyLoading.installedExtensionsRoot` points to). The Bundled root (`<install>/Extensions/`) is part of the install artifact and is managed by the OS-level install/uninstall of azcli itself — it cannot be mutated at runtime by any `az extension` command.

Consequences:

- Removing an extension that exists **only** in the Bundled tier is not possible via `az extension remove` — the command reports `ExtensionNotFound` because it only searches the Installed root. To change what's in the Bundled tier, reinstall azcli with a different packaging profile (see `docs/azure-cli/docs-conceptual/install-overview.md`).
- If a package id exists in both tiers, `az extension remove` deletes the Installed-tier payload only; the Bundled-tier copy remains discoverable and dispatch will fall back to it on the next invocation.
- `az extension add` never overwrites a Bundled payload. On a package-id collision the new payload lands under the Installed root at a fresh `<pkg>/<version>/` and, if it's a newer version, wins via `ExtensionCandidateSelector` (see §3.4).
- Bundled and Installed contributions collide on the same terms as any other pair (`LeafConflict` at install time, `LeafIndex` hard-fail at dispatch time). There is no tier bias.

---

## 3. Extension Discovery

### 3.1 Discovery Is Authoritative

Azclips uses **runtime discovery** to determine what extensions are installed and what they contribute. Normal CLI dispatch uses sidecar-first routing discovery over the installed extensions enumerated by the assembly-loading service. Detailed metadata consumers retain `DiscoverySession`, which loads extension indexes. Both paths use the file system (`~/.azclips/extensions/` + builtin `Extensions/`) as the **authoritative source of truth**, rather than treating registry entries as proof of installation.

The registry (`~/.azclips/extensions/registry.json`) is a **preferences layer** on top of discovery. It stores user choices (pinned versions, active-version overrides, install metadata) but does **not** determine installed state. If an extension is present on disk but missing from the registry, it is still discovered and available.

### 3.2 DiscoveredExtensionInfo

`DiscoverySession` emits `DiscoveredExtensionInfo` for each discovered extension:

```csharp
public sealed record DiscoveredExtensionInfo(
  string PackageId,
  string ResolvedVersion,
  IReadOnlyList<string> Verbs,
  IReadOnlyDictionary<string, CommandMetadata> Commands);
```

**Key fields:**

- **PackageId** — Unique extension identifier. Bundled and Installed extensions use the same id scheme.
- **ResolvedVersion** — The semantic version being used for this discovery pass.
    - For **Installed** extensions the version is the directory name at `~/.azclips/extensions/<pkg>/<version>/`.
    - For **Bundled** extensions the version is parsed from the on-disk `.nupkg` in the package folder (`BundledExtensionLoader.ResolvePackageVersionFromNupkg`). Local `dotnet build` output pairs each DLL with a freshly-packed `.nupkg` (via `GeneratePackageOnBuild=true`) and reports a real version. Packaged installs strip the `.nupkg` at packaging time (see [`inject_builtin_extensions.sh`](../../devops/scripts/packaging/common/inject_builtin_extensions.sh)) and report `ResolvedVersion = ""` — dispatch takes the unversioned load path in that case. Reporting a non-empty version for packaged Bundled extensions is tracked as a follow-up.
- **Verbs** — All top-level command namespaces contributed by this extension (e.g., an extension may contribute both `vm` and `disk`).
- **Commands** — Full `CommandMetadata` for each command, keyed by invocation name (for example, `"vm create"`).

### 3.3 Discovery Flow

Full discovery through `DiscoverySession` is **index-only**. It does not execute command processors, but it does load extension assemblies and instantiate the extension index type to read declared verbs/commands.

1. Walk **both roots** — the Bundled root (`<install>/Extensions/` in packaged installs, `artifacts/<Config>/` in local builds) and the Installed root (`~/.azclips/extensions/<packageId>/<version>/`). Each `IExtensionLoader.GetInstalledExtensions()` yields the candidates it owns.
2. Resolve the version to inspect (registry preference if present, otherwise latest semver — see §3.4).
3. Load each enumerated installation through its owning loader's deferred exact-load
   operation when available. Legacy providers without installation handles retain
   name/version loading and discovery's existing unversioned retry.
4. Instantiate `IExtensionIndex`, then call `GetVerbs()` and `GetCommandTable()`.
5. Collect results into `DiscoveredExtensionInfo`.

Because assemblies are loaded and index types are instantiated, discovery may run static initializers and index constructors. The runtime guarantee is narrower: discovery avoids processor activation/execution and processor-context initialization.

#### Exact installation loading (Azure/CLIPS#980)

CLI loaders also expose `GetInstallations()`, returning `InstalledExtensionRef` envelopes
with a runtime-only `ExtensionInstallation` handle. The original tuple enumeration and
name/version load methods remain available to legacy callers. Creating a handle captures
the owner, primary DLL path, version, source/root priority, and a file stamp; it does not
load the extension for execution. Installed version pins resolve to the pinned version's
actual DLL and sidecar path.

`IAssemblyLoadingService.LoadAllExtensions()` binds each handle-bearing entry's deferred
load to that installation. `LoadExtension(ExtensionInstallation)` never searches the
loader chain. Sidecar DLL fallback and full discovery do not retry name/version or
unversioned loading after an exact-load failure. Valid sidecars still require no extension
execution assembly loads during routing discovery.

Exact handles validate primary DLL length and last-write UTC time before loading or
reusing an assembly/index cache entry. Missing files or detected replacements fail with
installation context. Exact cache keys include owner, canonical path, and captured stamp;
they cannot alias legacy name/version or path-cache entries. Legacy direct name/version
loads retain their existing shared path cache. File stamps detect ordinary replacement,
not byte-level identity; dependency-only changes require restarting the host.

Enumeration/loading and discovery caches share the same identity. Routing carries
the runtime-only handle through sidecar or DLL-fallback metadata, the selected
registry projection, LeafIndex, resolved extension, and CommandInvocation. Handles are
excluded from JSON; pins and other persisted preferences cannot overwrite them.
Positional constructors and deconstruction shapes remain compatible with legacy callers.

Execution sessions consume that handle. Exact loads never re-resolve by
name/version; persistent execution caches use the same owner/locator/generation identity
as discovery, with stamp validation even on cache hits. Transient invocations use the
owning loader directly. Processor metadata must supply its Type for exact execution;
the legacy assembly-name fallback is not permitted to select a different payload.
Invocations without handles (including PowerShell and named legacy discovery) retain
their existing name/version behavior.

If enumeration fails, the warned registry-only routing index is not authoritative:
registered namespaces retain name/version fallback even for commands absent from saved
leaves. Successful discovery remains authoritative and rejects unselected command surfaces.
Completion preference-read failures are logged and do not discard successfully discovered
commands; selection then proceeds without persisted preferences.

The regression suite stages concrete Bundled and Installed DLLs in isolated temporary
installation roots. Distinct fixtures expose different processor output and Installed-only
commands; tests follow sidecar lookup through dispatch, processor acquisition, and execution
for both transient and cached sessions. The matrix includes identical/divergent surfaces,
equivalent versions, a newer or older Installed copy, matching pins, and an absent pin target.
Early/late Installed-only command names do not change the winner. Load tracking proves that
valid-sidecar routing loads zero execution assemblies, and loader diagnostics confirm only
the selected copy loads during execution. Separate fallback tests verify missing/malformed
sidecars retain their concrete handle.

Tests also remove the competing DLL before execution to verify the selected copy works
without an alternative. Repeated invocations and switching between same-version handles
test cache isolation.
After a selected DLL is removed or its stamp changes, execution fails even with a surviving
alternative and a warmed index cache. Rediscovery creates a new generation; it does not
repair an invocation already bound to a stale handle. These tests do not claim protection
against changes that preserve both file stamps, dependency replacement, or filesystem races.

### 3.4 Candidate Selection Across Roots

Candidates from the Bundled and Installed roots are merged by `PackageId` (case-insensitive) and passed through [`ExtensionCandidateSelector.Select`](../../src/Core/Core.Extensions/ExtensionCandidateSelector.cs). Rules, in order:

1. **Single candidate** → win. Concrete installations are not collapsed by command-metadata equality.
2. **Pinned in registry AND pinned version present in candidates** → the pinned candidate wins.
3. **Pinned in registry BUT pinned version absent** → fall back to highest SemVer _and emit a user-visible diagnostic + telemetry event_ (`extension.pin.unresolvable`) so the drift is not silent. The pinned version may have been manually deleted from `~/.azclips/extensions/` or never installed. See [`ExtensionCandidateSelector.ReportUnresolvablePin`](../../src/Core/Core.Extensions/ExtensionCandidateSelector.cs).
4. **No pin** → highest SemVer wins.
5. **Equivalent versions** → Bundled wins over Installed, including when both match a pin.
6. **Within a tier** → explicit root priority wins, followed by locator ordering for
   residual ties. Bundled roots use the existing ordered candidate-root list with the
   default root first. Installed currently has one root. Metadata fingerprints and
   command spelling do not decide precedence for concrete installations.

Pins retain their existing case-insensitive string matching, not semantic-version
equality. VersionRelease ordering still ignores build metadata and orders known
versions ahead of unknown versions. Equivalent unknown-version concrete candidates
use the same source/root precedence.

Legacy candidates without installation handles retain fingerprint deduplication and
ordering when no concrete candidates are present. In a mixed equal-version set, a
concrete installation takes precedence over an unresolved legacy candidate; version
and pin selection still happen first.

Example:

```
Bundled root:                                           Installed root:
<install>/Extensions/                                   ~/.azclips/extensions/
  Microsoft.Azure.Azclips.Extensions.Compute/             Microsoft.Azure.Azclips.Extensions.Compute/
    Microsoft.Azure.Azclips.Extensions.Compute.dll          0.9.0/
    (no .nupkg → ResolvedVersion = "")                      1.0.0/    ← registry pins this
                                                            1.1.0/    ← latest available
```

With `registry.json` pinning `1.0.0`, the Installed `1.0.0` candidate wins (rule 2). If the user deletes the `1.0.0` directory, the selector falls back to `1.1.0` and emits an error-level diagnostic naming the pin. If the registry is silent, `1.1.0` wins outright.

**Dispatch provenance.** Once the selector chooses an installation, dispatch carries
its handle alongside the package/version. Unknown commands in the selected surface
produce CommandNotFound; a stale registry cannot restore a losing installation's
commands or namespaces. Registry-only degraded routing and callers without handles
retain unresolved compatibility behavior. Execution uses the selected installation
and fails rather than substituting another copy if that installation is missing or changed.

**Completions and help.** CLI completions enumerate full discovery envelopes, select
one candidate per package using the shared selector and registry pins, and then
project only the selected command metadata. They no longer deduplicate installations
to names and independently load them. Help and extension list/show already use the
shared selector and now receive exact discovery provenance. Agent help's all-installation
catalog continues to consume the owner-bound deferred loads rather than re-searching
by name/version. Selection and execution debug messages identify source and location;
normal command output is unchanged.

### 3.5 Discovery in Help & Dispatch

**Normal command dispatch** builds its leaf index from lightweight routing metadata:

1. Enumerate installed extensions without loading their assemblies.
2. Read each installation's adjacent `<PackageId>.commands.json`. Require schema version 1,
   matching package identity, a valid package version, and valid verb/leaf arrays.
3. Compare a known installation version using NuGet release-version semantics (build metadata
   does not change version precedence). Preserve an unknown loader version rather than
   promoting the sidecar's version into a new execution-loader request.
4. For a missing, unreadable, malformed, or incompatible sidecar, load only that extension's
   index through the existing loader chain. A failure is warned and skipped, as in full discovery.
5. Apply the same candidate-selection rules as full discovery, overlay persisted preferences,
   and build the existing `LeafIndex`. Execution loads the selected extension normally.

Empty contracts are valid. Sidecars contain command ownership, not processor types, parameter
metadata, or full help. Version checks do not detect a same-version DLL replaced without its
sidecar; the build/package contract must keep the pair together. No persistent discovery cache
is introduced.

With `--debug`, routing discovery logs enumeration, sidecar validation, per-extension fallback
reasons, candidate selection, index construction, and a timing table distinguishing sidecar
reads from DLL fallbacks. These diagnostics use the existing logging pipeline, not command stdout.

**Help routing** uses discovery to resolve verb ownership. When a user runs `az vm --help`, the runtime:

1. Runs a discovery pass to enumerate installed extensions
2. Finds which extension's `Verbs` list includes `"vm"`
3. Routes to that extension's help provider

**Unknown verbs** fall back to an embedded hint table. The hint table is a build-time snapshot of first-party package mappings and is used only when discovery finds no installed owner.

**Extension listing** (`az extension list`) runs discovery and overlays registry preferences (pin state, install timestamp) onto the discovered extensions.

---

## 4. Host ↔ Extension Communication

### 4.1 Contract Boundary

The host and extensions share type identity through `Core.Abstractions`. Extensions exclude this assembly from their output, and the extension ALC falls back to the host for `Core.*` assemblies. This ensures both sides see the same interface types.

### 4.2 Key Interfaces

| Interface            | Role                                                                                 |
| -------------------- | ------------------------------------------------------------------------------------ |
| `IExtensionIndex`    | Extension entry point — returns command metadata by name and client type             |
| `ICommandMetadata`   | Describes one processor — name, .NET type, assembly                                  |
| `IProcessor`         | The processor — `Init(context)` receives runtime services, `ProcessAsync()` executes |
| `IProcessorContext`  | Bundle of runtime services provided by the host                                      |
| `ICommandInvocation` | Parsed user input — extension name, command name, parameter dictionary               |

> **[Proposal]** Replace `Init(IProcessorContext)` with **constructor injection** via `ActivatorUtilities.CreateInstance`, removing two-phase initialization.

### 4.3 Runtime Services (IProcessorContext)

The host creates a per-invocation `IProcessorContext` providing everything the processor needs:

| Service                | What It Provides                                                                  |
| ---------------------- | --------------------------------------------------------------------------------- |
| `IAuthHandler`         | Auth context (tenant, subscription) + token acquisition delegate                  |
| `IOutputHandler`       | Output delegate — **[CLI]** `Console.WriteLine`, **[PS]** `$PSCmdlet.WriteObject` |
| `ITelemetryHandler`    | Distributed tracing via `StartActivity`                                           |
| `IRuntimeHttpPipeline` | Pre-authenticated HTTP pipeline — extensions never manage their own `HttpClient`  |
| `IRuntimeHttpPipeline` | Pre-authenticated HTTP pipeline — extensions never manage their own `HttpClient`  |

### 4.4 Execution Flow

```
  User Input
      │
      ▼
  Parse & Route ─── [CLI] ExtensionDispatcher → CommandGroupIndex
      │              [PS]  Hardcoded in each exported function
      │
      ▼ ICommandInvocation
  Load Extension ── [CLI] IExtensionLoader chain → new ExtensionLoadContext (isolated ALC)
      │              [PS]  Already loaded via RequiredAssemblies (shared ALC)
      │
      ▼ IExtensionIndex
  Resolve Command ─ ExtensionIndex.GetCommandMetaData(name, clientType)
      │              → IProcessor instance + parameter binding
      │
      ▼ IProcessor
  Execute ───────── Host creates IProcessorContext
                    → Init(context) → ProcessAsync()
                    Processor calls back through context for I/O and auth
```

> **[Proposal]** Replace `ExtensionDispatcher` with `CommandRouter` (thin routing) and `CommandActivator` (resolution + help integration).

> **Dispatch model details.** The CLI dispatch path above (`Parse & Route` → `ExtensionDispatcher` → leaf resolution) is fully specified in [extension-management.md](./extension-management.md) — see §3 (user surface), §4 (registry / hint-catalog / `s_cliTable` interaction and the leaf-keyed routing model: shared verbs, per-leaf `(verb, path)` ownership), and §5 (conflict & validation). That document is the source of truth for how a parsed command is bound to a single extension at runtime; this document covers the surrounding host machinery (loading, contexts, packaging).

### 4.5 [Proposal] NativeAOT Compilation

NativeAOT eliminates the JIT compiler, producing a fully native executable. This conflicts with several patterns the extension framework currently relies on:

- **No dynamic assembly loading** — `AssemblyLoadContext`, `Assembly.LoadFrom`, and runtime DLL discovery are not supported. The current extension loading model (scan directory → load DLL → create ALC) cannot work under NativeAOT.
- **Limited reflection** — `Activator.CreateInstance` on types discovered at runtime, `Type.GetType()` from dynamically loaded assemblies, and reflection-based property binding via `ArgumentInfoAttribute` all require types to be statically reachable at compile time.
- **No `AssemblyDependencyResolver`** — the `.deps.json`-based resolution that `ExtensionLoadContext` uses does not exist in NativeAOT.

To adopt NativeAOT, the extension model would need to change fundamentally:

1. **Builtin extensions compiled into the host** — extensions are statically linked at build time, eliminating dynamic loading entirely. Source-generated `ExtensionIndex` (already proposed) would replace reflection-based discovery.
2. **User-installed extensions as separate NativeAOT libraries** — each extension is independently AoT-compiled and exposes entry points via `[UnmanagedCallersOnly]` / C-ABI function pointers. The host loads them via native library loading (`NativeLibrary.Load`) instead of managed assembly loading.
3. **Marshalling across the boundary** — `IProcessor`, `IProcessorContext`, and other managed interfaces cannot cross a native interop boundary directly. Communication would need to use C-ABI-compatible patterns (function pointers, flat structs, or a serialized protocol).

This is a significant architectural change. A pragmatic path would be to start with option 1 (static linking for builtin extensions) to enable NativeAOT for the core CLI, while deferring option 2 (native plugin model for user-installed extensions) to a later phase. will

---

## 5. Assembly Isolation

### 5.1 The Problem

Extensions carry their own dependencies that may conflict with each other or the host. Without isolation, the first loaded version of a shared dependency wins, potentially breaking other extensions.

### 5.2 The Solution: ExtensionLoadContext

A custom `AssemblyLoadContext` per extension with one rule:

- **Shared** (`Core.*`, `Microsoft.Extensions.DependencyInjection.*`) → `Load()` returns `null` → falls back to host ALC → preserves type identity across the boundary
- **Isolated** (everything else) → loaded from the extension's directory → sandboxed per extension

### 5.3 [CLI] Dedicated ALC per Extension

Each extension is loaded into its own `ExtensionLoadContext`. Two loaders are tried in order:

1. `InstalledExtensionLoader` — user-installed extensions at `~/.azclips/extensions/`
2. `BundledExtensionLoader` — bundled extensions at `<appDir>/Extensions/`

Each loader creates a fresh ALC per extension. Dependencies between extensions cannot conflict.

### 5.4 [PS] Shared Default ALC

PowerShell modules load all assemblies into the host's default ALC via `RequiredAssemblies` in the module manifest. There are no custom ALCs. `PSEngine` resolves extension types via `Type.GetType()` from assemblies already in the default ALC.

**Current Problem**:

- For PowerShell, there are in fact **3** different runtime contexts that could cause conflicts between each other:
    1. The PowerShell host process and its loaded assemblies
    2. The `runtime` / `command host` shared across all Azclips PowerShell modules
    3. The individual extension assemblies loaded by the command host
- If we would like to share the type between extension and PowerShell host, we have to load the extension assembly into the default ALC, which means all extensions share the same ALC and can conflict with each other.
- When developing configuration commands, we already see conflicts between `System.Text.Json` versions used by the PowerShell host and the extension, causing runtime errors.

**Consequence**: if two modules carry different versions of the same dependency, the first imported module wins. No isolation. This could also cause conflicts with the host if an extension's dependency version differs from the host's.

### 5.5 Comparison

| Aspect                | CLI                                         | PowerShell                      |
| --------------------- | ------------------------------------------- | ------------------------------- |
| Extension isolation   | Dedicated ALC per extension                 | All in default ALC              |
| Dependency conflicts  | Impossible — sandboxed                      | Possible — first loaded wins    |
| Core assembly sharing | `Load()` → `null` → host fallback           | Naturally shared (same ALC)     |
| Type resolution       | `Activator.CreateInstance` in extension ALC | `Type.GetType()` in default ALC |

> **[Proposal]** Introduce `CommandHost` with per-module ALCs for PowerShell, mirroring the CLI isolation model.

---

## 6. Build & Deployment

### 6.1 [CLI] Core CLI

**Build**: `dotnet publish src/CLI/Core.Cli.csproj -c Release -r win-x64 --self-contained`

**Deployment layout**:

```
publish/win-x64/
  az.exe                         ← Self-contained host
  Core.*.dll                       ← Core framework (host ALC)
  prompt.md                        ← AI agent system prompt
  Extensions/                      ← Builtin extensions (each in own ALC)
    Microsoft.Azure.Azclips.Extensions.Extension/
```

**MSIX packaging** is supported via `Core.Cli.appxmanifest` for Windows distribution.

> **[Proposal]** Replace the static `Bootstrapper` with `AzclipsHostBuilder`, a non-static builder exposing `AddAzclipsCore()` / `AddAzclipsCli()` / `AddAzclipsPS()` and scoped DI per invocation.

> **[Proposal]** Extract CLI argument parsing from `CLIEngine` into standalone DI-registered services: `ArgumentParser`, `PropertyParser`, `CommandResolver`.

### 6.2 [PS] PowerShell Modules

**Current state** (development only): modules live in `src/PS/` and reference `artifacts/Debug/` via relative paths in `RequiredAssemblies`.

**Target production layout** (not yet implemented):

```
Azclips/
  Accounts/
    Accounts.psd1, Accounts.psm1, exports/*.ps1
    lib/                            ← Assembled DLLs
      Core.*.dll, Accounts.dll, ...
  Resources/
    Resources.psd1, ...
```

A publish pipeline — copying assemblies into a portable module folder and updating manifest paths — is **not yet implemented**.

---

## 7. Open Items

| Area                      | Status          | Description                                                                       |
| ------------------------- | --------------- | --------------------------------------------------------------------------------- |
| PowerShell module publish | Not implemented | Need a build step producing a portable module folder with correct assembly layout |
| CLI publish automation    | Partial         | Manual steps documented; `build.yml` CI exists but `deploy.sh` is incomplete      |
