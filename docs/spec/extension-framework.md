# Design Spec: Extension Framework

## Overview

Cirrus ships as both a standalone CLI (`azv2`) and a set of PowerShell modules. Both surfaces share the same extension assemblies and core engine, but differ in how they build, package, load extensions, and manage assembly isolation.

> Items marked **[Proposal]** describe planned changes not yet implemented on `main`.
> Items marked **[CLI]** or **[PS]** are specific to that surface.

---

## 1. Architecture at a Glance

Cirrus produces three categories of build artifact:

- **Core CLI** (`azv2.exe`) — Self-contained host process bundling the engines, AI agent, DI container, and auth
- **Extensions** (`.nupkg` / DLL) — Dynamically loaded at runtime, carrying their own dependencies
- **PowerShell Modules** (`.psd1` / `.psm1`) — Script-based wrappers that drive the same .NET engine and extensions

Two host surfaces consume the same extensions:

| | CLI | PowerShell |
|---|-----|------------|
| **Entry point** | `azv2.exe` → `Program.cs` | `Import-Module` → `.psm1` → exported functions |
| **Engine** | `CLIEngine` | `PSEngine` |
| **Extension loading** | File-based, into isolated ALCs | Pre-loaded via manifest, into shared ALC |
| **Output** | `Console.WriteLine` | `$PSCmdlet.WriteObject` via channel |

---

## 2. Extensions

### 2.1 What an Extension Is

An extension is a .NET class library that implements a set of commands for one Azure service area (e.g., Login, Resources). Every extension must provide:

- **`ExtensionIndex`** — implements `IExtensionIndex`, returns `ICommandMetadata` for a given command name and client type
- **Command classes** — implement `IProcessor` with `Init(context)` and `ProcessAsync()`
- **Dual command tables** — the index maps CLI command names (e.g., `create`) and PowerShell command names (e.g., `New-ResourceGroup`) to the same `IProcessor` types

### 2.2 How to Build an Extension

Key `.csproj` conventions:
- `EnableDynamicLoading=true` — produces `.deps.json` / `.runtimeconfig.json` for runtime dependency resolution
- `GeneratePackageOnBuild=true` — produces a `.nupkg` for distribution
- `Core.Abstractions` referenced as **compile-only** (`Private=false`, `ExcludeAssets=runtime`) — shared contracts come from the host at runtime, not the extension's output

All outputs land in `artifacts/{Configuration}/{ExtName}/` controlled by `Shared.props`.

> **[Proposal]** Extensions should depend **only** on `Core.Abstractions`. References to `Core.Engine` and `Core.Implementation` would be removed.

> **[Proposal]** A Roslyn incremental source generator (`Core.Index.Generator`) auto-generates `ExtensionIndex` from `[CommandInfo]` attributes, eliminating hand-maintained command tables.

### 2.3 How Extensions Are Packaged

- **Builtin** — shipped alongside the CLI host in an `Extensions/` subdirectory
- **User-installed** — downloaded as NuGet packages via `azv2 extension add`, placed in `~/.cirrus/extensions/<PackageId>/<Version>/`; latest semantic version is selected automatically

---

## 3. Host ↔ Extension Communication

### 3.1 Contract Boundary

The host and extensions share type identity through `Core.Abstractions`. Extensions exclude this assembly from their output, and the extension ALC falls back to the host for `Core.*` assemblies. This ensures both sides see the same interface types.

### 3.2 Key Interfaces

| Interface | Role |
|-----------|------|
| `IExtensionIndex` | Extension entry point — returns command metadata by name and client type |
| `ICommandMetadata` | Describes one command — name, .NET type, assembly |
| `IProcessor` | The command — `Init(context)` receives runtime services, `ProcessAsync()` executes |
| `ICommandContext` | Bundle of runtime services provided by the host |
| `ICommandInvocation` | Parsed user input — extension name, command name, parameter dictionary |

> **[Proposal]** Replace `Init(ICommandContext)` with **constructor injection** via `ActivatorUtilities.CreateInstance`, removing two-phase initialization.

### 3.3 Runtime Services (ICommandContext)

The host creates a per-invocation `ICommandContext` providing everything the extension needs:

| Service | What It Provides |
|---------|-----------------|
| `ICommandAuthHandler` | Auth context (tenant, subscription) + token acquisition delegate |
| `ICommandOutputHandler` | Output delegate — **[CLI]** `Console.WriteLine`, **[PS]** `$PSCmdlet.WriteObject` |
| `ICommandTelemetryHandler` | Distributed tracing via `StartActivity` |
| `IRuntimeHttpPipeline` | Pre-authenticated HTTP pipeline — extensions never manage their own `HttpClient` |

### 3.4 Execution Flow

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
  Execute ───────── Host creates ICommandContext
                    → Init(context) → ProcessAsync()
                    Extension calls back through context for I/O and auth
```

> **[Proposal]** Replace `ExtensionDispatcher` with `CommandRouter` (thin routing) and `CommandActivator` (resolution + help integration).

### 3.5 [Proposal] NativeAOT Compilation

NativeAOT eliminates the JIT compiler, producing a fully native executable. This conflicts with several patterns the extension framework currently relies on:

- **No dynamic assembly loading** — `AssemblyLoadContext`, `Assembly.LoadFrom`, and runtime DLL discovery are not supported. The current extension loading model (scan directory → load DLL → create ALC) cannot work under NativeAOT.
- **Limited reflection** — `Activator.CreateInstance` on types discovered at runtime, `Type.GetType()` from dynamically loaded assemblies, and reflection-based property binding via `ArgumentInfoAttribute` all require types to be statically reachable at compile time.
- **No `AssemblyDependencyResolver`** — the `.deps.json`-based resolution that `ExtensionLoadContext` uses does not exist in NativeAOT.

To adopt NativeAOT, the extension model would need to change fundamentally:

1. **Builtin extensions compiled into the host** — extensions are statically linked at build time, eliminating dynamic loading entirely. Source-generated `ExtensionIndex` (already proposed) would replace reflection-based discovery.
2. **User-installed extensions as separate NativeAOT libraries** — each extension is independently AoT-compiled and exposes entry points via `[UnmanagedCallersOnly]` / C-ABI function pointers. The host loads them via native library loading (`NativeLibrary.Load`) instead of managed assembly loading.
3. **Marshalling across the boundary** — `IProcessor`, `ICommandContext`, and other managed interfaces cannot cross a native interop boundary directly. Communication would need to use C-ABI-compatible patterns (function pointers, flat structs, or a serialized protocol).

This is a significant architectural change. A pragmatic path would be to start with option 1 (static linking for builtin extensions) to enable NativeAOT for the core CLI, while deferring option 2 (native plugin model for user-installed extensions) to a later phase. will

---

## 4. Assembly Isolation

### 4.1 The Problem

Extensions carry their own dependencies that may conflict with each other or the host. Without isolation, the first loaded version of a shared dependency wins, potentially breaking other extensions.

### 4.2 The Solution: ExtensionLoadContext

A custom `AssemblyLoadContext` per extension with one rule:

- **Shared** (`Core.*`, `Microsoft.Extensions.DependencyInjection.*`) → `Load()` returns `null` → falls back to host ALC → preserves type identity across the boundary
- **Isolated** (everything else) → loaded from the extension's directory → sandboxed per extension

### 4.3 [CLI] Dedicated ALC per Extension

Each extension is loaded into its own `ExtensionLoadContext`. Two loaders are tried in order:
1. `MultiVersionExtensionLoader` — user-installed extensions at `~/.cirrus/extensions/`
2. `BuiltinExtensionLoader` — bundled extensions at `<appDir>/Extensions/`

Each loader creates a fresh ALC per extension. Dependencies between extensions cannot conflict.

### 4.4 [PS] Shared Default ALC

PowerShell modules load all assemblies into the host's default ALC via `RequiredAssemblies` in the module manifest. There are no custom ALCs. `PSEngine` resolves extension types via `Type.GetType()` from assemblies already in the default ALC.

**Current Problem**:
* For PowerShell, there are in fact **3** different runtime contexts that could cause conflicts between each other:
    1. The PowerShell host process and its loaded assemblies
    2. The `runtime` / `command host` shared across all Cirrus PowerShell modules
    3. The individual extension assemblies loaded by the command host
* If we would like to share the type between extension and PowerShell host, we have to load the extension assembly into the default ALC, which means all extensions share the same ALC and can conflict with each other.
* When developing configuration commands, we already see conflicts between `System.Text.Json` versions used by the PowerShell host and the extension, causing runtime errors.

**Consequence**: if two modules carry different versions of the same dependency, the first imported module wins. No isolation. This could also cause conflicts with the host if an extension's dependency version differs from the host's.

### 4.5 Comparison

| Aspect | CLI | PowerShell |
|--------|-----|------------|
| Extension isolation | Dedicated ALC per extension | All in default ALC |
| Dependency conflicts | Impossible — sandboxed | Possible — first loaded wins |
| Core assembly sharing | `Load()` → `null` → host fallback | Naturally shared (same ALC) |
| Type resolution | `Activator.CreateInstance` in extension ALC | `Type.GetType()` in default ALC |

> **[Proposal]** Introduce `CommandHost` with per-module ALCs for PowerShell, mirroring the CLI isolation model (similar to the `Az.Accounts` pattern).

---

## 5. Build & Deployment

### 5.1 [CLI] Core CLI

**Build**: `dotnet publish src/CLI/Core.Cli.csproj -c Release -r win-x64 --self-contained`

**Deployment layout**:
```
publish/win-x64/
  azv2.exe                         ← Self-contained host
  Core.*.dll                       ← Core framework (host ALC)
  prompt.md                        ← AI agent system prompt
  Extensions/                      ← Builtin extensions (each in own ALC)
    Microsoft.Azure.Cirrus.Extensions.Extension/
```

**MSIX packaging** is supported via `Core.Cli.appxmanifest` for Windows distribution.

> **[Proposal]** Replace the static `Bootstrapper` with `CirrusHostBuilder`, a non-static builder exposing `AddCirrusCore()` / `AddCirrusCli()` / `AddCirrusPS()` and scoped DI per invocation.

> **[Proposal]** Extract CLI argument parsing from `CLIEngine` into standalone DI-registered services: `ArgumentParser`, `PropertyParser`, `CommandResolver`.

### 5.2 [PS] PowerShell Modules

**Current state** (development only): modules live in `src/PS/` and reference `artifacts/Debug/` via relative paths in `RequiredAssemblies`.

**Target production layout** (not yet implemented):
```
Cirrus/
  Login/
    Login.psd1, Login.psm1, exports/*.ps1
    lib/                            ← Assembled DLLs
      Core.*.dll, Login.dll, ...
  Resources/
    Resources.psd1, ...
```

A publish pipeline — copying assemblies into a portable module folder and updating manifest paths — is **not yet implemented**.

---

## 6. Proposals

| Area | Proposal | Impact |
|------|----------|--------|
| DI & hosting | Replace static `Bootstrapper` with `CirrusHostBuilder` + scoped DI per invocation | Core CLI, PS modules |
| CLI parsing | Extract `ArgumentParser`, `PropertyParser`, `CommandResolver` from `CLIEngine` | CLI only |
| CLI routing | Replace `ExtensionDispatcher` with `CommandRouter` + `CommandActivator` | CLI only |
| Command init | Constructor injection via `ActivatorUtilities` instead of `Init(ICommandContext)` | Extensions |
| Extension deps | Extensions reference only `Core.Abstractions`, not `Core.Engine`/`Core.Implementation` | Extensions |
| Index generation | Roslyn source generator auto-generates `ExtensionIndex` from `[CommandInfo]` | Extensions |
| PS isolation | `CommandHost` with per-module `AssemblyLoadContext` for PowerShell | PS modules |

---

## 7. Open Items

| Area | Status | Description |
|------|--------|-------------|
| PowerShell module publish | Not implemented | Need a build step producing a portable module folder with correct assembly layout |
| CLI publish automation | Partial | Manual steps documented; `build.yml` CI exists but `deploy.sh` is incomplete |
