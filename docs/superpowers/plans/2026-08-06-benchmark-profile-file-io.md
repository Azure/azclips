# Benchmark Profile File I/O Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make BenchmarkDotNet benchmarks exercise the real file-backed `ProfileService` instead of the in-memory `StubProfileService`, so profile persistence cost is actually measured.

**Architecture:** A new `BenchmarkProfileStore` class owns a process-scoped directory under `artifacts/Benchmark/home/` and seeds it with a `default` profile by calling the real `ProfileService`. `BenchmarkBase` initializes that store during `InitializeCommon()` and registers a real `ProfileService` pointed at it, replacing the `StubProfileService` registration. `StubProfileService` itself is untouched and remains the in-memory implementation for unit and scenario tests.

**Tech Stack:** C# / .NET 10, xUnit, BenchmarkDotNet 0.13.12, `Microsoft.Extensions.DependencyInjection`.

**Design spec:** `docs/superpowers/specs/2026-08-06-benchmark-profile-file-io-design.md`

## Global Constraints

- Use the .NET SDK pinned in `global.json`.
- **Every full-solution build and every `dotnet test` in this plan must pass
  `-p:CopilotNpmRegistryUrl=https://packagefeedproxy.microsoft.io/npm/`.** `azclips.slnx`
  includes `Agent/Core.Agent`, which downloads a GitHub Copilot npm package at build time;
  without this property the restore reaches `registry.npmjs.org` and fails with an
  `MSB3923` / TLS handshake error. `Cli.Tests` references `Agent.Commands`, so its
  `dotnet test` needs it as well. A TLS failure here is a missing property, not a broken
  build — add the property, do not "fix" anything else.
- The commands are therefore:
  ```powershell
  dotnet build azclips.slnx -c Release -p:CopilotNpmRegistryUrl=https://packagefeedproxy.microsoft.io/npm/
  dotnet test <project>.csproj -c Release -p:CopilotNpmRegistryUrl=https://packagefeedproxy.microsoft.io/npm/
  ```
  `tests/Benchmark/Benchmark.csproj` does **not** pull in `Core.Agent`, so `dotnet run`
  against it needs no property.
- C# with `nullable` enabled and `ImplicitUsings` enabled (`Benchmark.csproj` sets both), so `System`, `System.IO`, `System.Threading.Tasks`, and `System.Linq` need no `using` directives.
- Every new `.cs` file starts with the repo's two-line copyright header:
  ```csharp
  // Copyright (c) Microsoft Corporation.
  // Licensed under the MIT License.
  ```
- Do not modify `src/Core/Core.Runtime/ProfileService.cs` or any other production source. This change is confined to `tests/` and docs.
- Do not change the behaviour of `StubProfileService` — only its XML doc comment.
- The benchmark profile directory must never be the developer's real `~/.azclips`. Always pass an explicit `baseDirectory` to `ProfileService`.
- Preserve `[WarmupCount(5000)]` and `[IterationCount(5000)]` in both benchmark base classes. **The `validate-benchmarks` skill text says these are `10000` — that is stale. The repo value is `5000`, and `5000` is what must be restored after any temporary lowering.**
- Keep changes focused; no unrelated refactors.

---

## File Structure

| File | Status | Responsibility |
|---|---|---|
| `tests/Benchmark/BenchmarkProfileStore.cs` | Create | Owns the benchmark profile directory path, its one-time seeding, its deletion, and a silent logger for seeding. Single responsibility: the benchmark's on-disk profile store. |
| `tests/Benchmark/BenchmarkBase.cs` | Modify | Holds the store, initializes it in `InitializeCommon()`, and registers the real `ProfileService` in `RegisterBenchmarkStubs()`. |
| `tests/Benchmark/PS/PSBenchmarkBase.cs` | Modify | Adds the `[GlobalCleanup]` it currently lacks, to delete the store. |
| `tests/Common/Stubs/StubProfileService.cs` | Modify | XML doc note only. Behaviour unchanged. |
| `wiki/02-deep-dive/testing-benchmarking.md` | Modify | Line 92 currently lists `StubProfileService` as a benchmark stub. |
| `tests/Benchmark/README.md` | Modify | Lifecycle bullets (lines 85–88) and the stubbing paragraph (line 97). |

**Note on naming:** the design spec calls the path property `Directory`. The plan uses **`RootDirectory`** instead, because a property named `Directory` shadows `System.IO.Directory` inside the class and breaks `Directory.CreateDirectory(...)` / `Directory.Exists(...)` calls. Use `RootDirectory` everywhere.

**There is no unit-test project for `tests/Benchmark`** (verified — no `Benchmark.Tests` exists, and no test project references `Benchmark.csproj`). Adding one is out of scope. Verification therefore uses: the compiler, the existing `Cli.Tests` / `Scenario.Tests` suites, and a real benchmark run whose on-disk output is inspected. Task 1 exploits the fact that `PSBenchmarkBase` has no `[GlobalCleanup]` *yet* to inspect the seeded files the benchmark actually wrote — that is the direct proof that real file I/O happened.

---

### Task 1: Benchmark profile store and DI registration

**Files:**
- Create: `tests/Benchmark/BenchmarkProfileStore.cs`
- Modify: `tests/Benchmark/BenchmarkBase.cs` (lines 7–13 usings, lines 21–27 fields, lines 55–61 `InitializeCommon`, lines 63–77 `RegisterBenchmarkStubs`)

**Interfaces:**
- Consumes: `Core.Runtime.ProfileService(ILoggingService logger, string? baseDirectory = null)`; `Core.Abstractions.Profile` (a `record` with `init` properties: `Name`, `TenantId`, `SubscriptionId`, `CloudName`, `AuthMethod`); `Core.Abstractions.AuthMethod.Browser`; `Core.Abstractions.ILoggingService` (six members: `Log`, `Info`, `Verbose`, `Debug`, `Warn`, `Error`, each `void M(string message)`); `Tests.Common.Utilities.RepoLocator.GetRepoRoot()`.
- Produces:
  - `Benchmark.BenchmarkProfileStore` — `public string RootDirectory { get; private set; }`, `public void Initialize(string repoRoot)`, `public void Delete()`.
  - `Benchmark.BenchmarkBase` — `protected BenchmarkProfileStore ProfileStore { get; }`, consumed by Task 2.

- [ ] **Step 1: Create `tests/Benchmark/BenchmarkProfileStore.cs`**

```csharp
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using Core.Runtime;

namespace Benchmark;

/// <summary>
/// Owns the benchmark-scoped profile store on disk so benchmarks exercise the real
/// file-backed <see cref="ProfileService"/> rather than an in-memory stub. Profile
/// persistence is a genuine cost of commands such as <c>login</c> — a JSON read, a
/// serialize, and two atomic temp-write-plus-rename cycles — and stubbing it out hid
/// that cost entirely.
/// </summary>
/// <remarks>
/// The directory is suffixed with the current process id. BenchmarkDotNet runs each
/// benchmark case in its own child process, so this guarantees a clean seeded store per
/// process and makes it impossible for two concurrent benchmark invocations to collide
/// on the same files. It lives under the existing <c>artifacts/Benchmark/home</c> tree
/// that <c>CLIBenchmarkBase</c> already creates and removes.
/// </remarks>
public sealed class BenchmarkProfileStore
{
    /// <summary>
    /// Seeded into every benchmark process so each measured iteration reads an existing
    /// <c>profiles.json</c> rather than short-circuiting on a missing file. Mirrors the
    /// profile <c>StubProfileService</c> seeds, preserving the connection state
    /// benchmarks previously saw.
    /// </summary>
    private static readonly Profile SeedProfile = new()
    {
        Name = "default",
        TenantId = "00000000-0000-0000-0000-000000000000",
        SubscriptionId = "00000000-0000-0000-0000-000000000000",
        CloudName = "AzureCloud",
        AuthMethod = AuthMethod.Browser,
    };

    /// <summary>
    /// Absolute path passed to <see cref="ProfileService"/> as its base directory.
    /// Empty until <see cref="Initialize"/> runs.
    /// </summary>
    public string RootDirectory { get; private set; } = string.Empty;

    /// <summary>
    /// Resolves the directory, resets it, and seeds it. Call once per process from
    /// <c>[GlobalSetup]</c> so the seeding I/O stays outside the measured region.
    /// </summary>
    public void Initialize(string repoRoot)
    {
        RootDirectory = Path.Combine(
            repoRoot, "artifacts", "Benchmark", "home", $".azclips-{Environment.ProcessId}");

        Delete();
        Directory.CreateDirectory(RootDirectory);

        // Seed through the real service rather than hand-written JSON so the on-disk
        // format always matches production and the serializer options stay in one place.
        var seeder = new ProfileService(new SilentLoggingService(), RootDirectory);
        seeder.SaveAndActivateProfileAsync(SeedProfile).GetAwaiter().GetResult();

        Console.WriteLine($"[Setup] Benchmark profile store seeded at {RootDirectory}");
    }

    /// <summary>Removes the store, best-effort.</summary>
    public void Delete()
    {
        if (string.IsNullOrEmpty(RootDirectory))
        {
            return;
        }

        try
        {
            if (Directory.Exists(RootDirectory))
            {
                Directory.Delete(RootDirectory, recursive: true);
            }
        }
        catch (IOException) { /* best-effort */ }
        catch (UnauthorizedAccessException) { /* best-effort */ }
    }

    /// <summary>
    /// Discards seeding log output. <see cref="ProfileService"/> writes a Verbose line on
    /// every load and save, and with no active logging scope <c>ScopedLoggingService</c>
    /// falls back to <c>ConsoleLoggingService</c>, which writes to stderr.
    /// </summary>
    private sealed class SilentLoggingService : ILoggingService
    {
        public void Log(string message) { }
        public void Info(string message) { }
        public void Verbose(string message) { }
        public void Debug(string message) { }
        public void Warn(string message) { }
        public void Error(string message) { }
    }
}
```

- [ ] **Step 2: Add the `Core.Runtime` using to `tests/Benchmark/BenchmarkBase.cs`**

The file's using block currently reads:

```csharp
using System.Reflection;
using Core.Abstractions;
using Core.Abstractions.Runtime.Handler;
using Core.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Tests.Common.Stubs;
using Tests.Common.Utilities;
```

Insert `using Core.Runtime;` after `using Core.DependencyInjection;`:

```csharp
using System.Reflection;
using Core.Abstractions;
using Core.Abstractions.Runtime.Handler;
using Core.DependencyInjection;
using Core.Runtime;
using Microsoft.Extensions.DependencyInjection;
using Tests.Common.Stubs;
using Tests.Common.Utilities;
```

Leave `using Tests.Common.Stubs;` in place — `StubHttpTerminateHandler`, `StubTelemetryService`, and `StubAuthService` still come from it.

- [ ] **Step 3: Add the `ProfileStore` property to `BenchmarkBase`**

Find this block near the top of the class:

```csharp
    protected string RepoRoot { get; set; } = null!;
    protected int RgCounter = 0;
    protected int VmCounter = 0;
    protected string SubscriptionId { get; set; } = "00000000-0000-0000-0000-000000000000";
```

Replace it with:

```csharp
    protected string RepoRoot { get; set; } = null!;
    protected int RgCounter = 0;
    protected int VmCounter = 0;
    protected string SubscriptionId { get; set; } = "00000000-0000-0000-0000-000000000000";

    /// <summary>
    /// On-disk profile store backing the real <see cref="ProfileService"/> used by
    /// benchmarks. Seeded once per process by <see cref="InitializeCommon"/>.
    /// </summary>
    protected BenchmarkProfileStore ProfileStore { get; } = new();
```

- [ ] **Step 4: Seed the store in `InitializeCommon`**

Replace:

```csharp
    /// <summary>
    /// Initialize repo root (one-time setup).
    /// </summary>
    protected void InitializeCommon()
    {
        RepoRoot = RepoLocator.GetRepoRoot();
    }
```

with:

```csharp
    /// <summary>
    /// Initialize repo root and seed the benchmark profile store (one-time setup).
    /// </summary>
    /// <remarks>
    /// Seeding happens here rather than in a new <c>[GlobalSetup]</c> because
    /// BenchmarkDotNet permits only one <c>[GlobalSetup]</c> per type hierarchy and both
    /// <c>CLIBenchmarkBase</c> and <c>PSBenchmarkBase</c> already declare one. Both call
    /// this method from theirs.
    /// </remarks>
    protected void InitializeCommon()
    {
        RepoRoot = RepoLocator.GetRepoRoot();
        ProfileStore.Initialize(RepoRoot);
    }
```

- [ ] **Step 5: Register the real `ProfileService` in `RegisterBenchmarkStubs`**

Replace:

```csharp
    /// <summary>
    /// Registers stub DI overrides for benchmark isolation.
    /// Call before each iteration since <see cref="AzclipsServiceProvider.Dispose"/>
    /// clears all registrations.
    /// </summary>
    protected void RegisterBenchmarkStubs()
    {
        AzclipsServiceProvider.OverrideRegistration(services =>
        {
            services.AddSingleton<IHttpTerminateHandler, StubHttpTerminateHandler>();
            services.AddSingleton<ITelemetryService, StubTelemetryService>();
            services.AddSingleton<IProfileService, StubProfileService>();
            services.AddSingleton<IAuthService, StubAuthService>();
        });
    }
```

with:

```csharp
    /// <summary>
    /// Registers DI overrides for benchmark isolation.
    /// Call before each iteration since <see cref="AzclipsServiceProvider.Dispose"/>
    /// clears all registrations.
    /// </summary>
    /// <remarks>
    /// HTTP, telemetry, and auth are stubbed so benchmarks never leave the machine.
    /// Profiles deliberately are not: the real <see cref="ProfileService"/> is registered
    /// against <see cref="ProfileStore"/> so the JSON read, serialize, and atomic
    /// write-plus-rename that commands such as <c>login</c> perform are actually
    /// measured. The explicit base directory keeps the run away from the developer's
    /// real <c>~/.azclips</c>.
    /// </remarks>
    protected void RegisterBenchmarkStubs()
    {
        AzclipsServiceProvider.OverrideRegistration(services =>
        {
            services.AddSingleton<IHttpTerminateHandler, StubHttpTerminateHandler>();
            services.AddSingleton<ITelemetryService, StubTelemetryService>();
            services.AddSingleton<IProfileService>(sp =>
                new ProfileService(sp.GetRequiredService<ILoggingService>(), ProfileStore.RootDirectory));
            services.AddSingleton<IAuthService, StubAuthService>();
        });
    }
```

- [ ] **Step 6: Build and confirm it compiles**

Run: `dotnet build azclips.slnx -c Release -p:CopilotNpmRegistryUrl=https://packagefeedproxy.microsoft.io/npm/`
Expected: build succeeds, 0 errors.

If you see `CS0119` or `CS1955` mentioning `Directory`, you named the property `Directory` instead of `RootDirectory` and it is shadowing `System.IO.Directory`. Rename it.

- [ ] **Step 7: Confirm the in-memory stub's consumers still pass**

`StubProfileService` is still used by `Cli.Tests` (`ProfileCliTests`, `ConfigCliTests`) and `Scenario.Tests` (`ScenarioHost`). Nothing should have changed for them.

Run:
```powershell
dotnet test tests\Cli.Tests\Cli.Tests.csproj -c Release -p:CopilotNpmRegistryUrl=https://packagefeedproxy.microsoft.io/npm/
dotnet test tests\Scenario.Tests\Scenario.Tests.csproj -c Release -p:CopilotNpmRegistryUrl=https://packagefeedproxy.microsoft.io/npm/
```
Expected: both suites pass, with the same results as before the change.

- [ ] **Step 8: Stage PowerShell modules for the smoke run**

The PS benchmarks load Release build outputs and a staged module manifest closure.

Run:
```powershell
.\build\Set-ModuleConfiguration.ps1 -c Release
```
Expected: completes without error.

- [ ] **Step 9: Temporarily lower the PS iteration count for a fast smoke run**

In `tests/Benchmark/PS/PSBenchmarkBase.cs` line 18, change `[IterationCount(5000)]` to `[IterationCount(5)]`.
On the line above it, change `[WarmupCount(5000)]` to `[WarmupCount(5)]`.

**Treat Steps 9–12 as try/finally: Step 12 restores both values to `5000` and must run even if Step 10 or 11 fails.**

- [ ] **Step 10: Run the PS Login benchmark and prove real file I/O happened**

`PSBenchmarkBase` has no `[GlobalCleanup]` yet, so the seeded store survives the run. That is what makes this check possible — Task 2 removes the opportunity, so do it now.

Run:
```powershell
dotnet run --project tests\Benchmark\Benchmark.csproj -- --local --product PS --extension Login
```

Expected in the output:
- A line `[Setup] Benchmark profile store seeded at <repo>\artifacts\Benchmark\home\.azclips-<pid>`
- A final line `[Benchmark] PASS — all benchmarks completed successfully.`
- Exit code `0`
- **No** flood of `VERBOSE: Loading profiles from` or `VERBOSE: Saving 2 profile(s)` lines on stderr

Do not interpret the timing numbers; this is a smoke test.

- [ ] **Step 11: Inspect the files the benchmark wrote**

Run:
```powershell
Get-ChildItem -Path .\artifacts\Benchmark\home -Filter ".azclips-*" -Directory |
    ForEach-Object {
        Write-Output "--- $($_.FullName) ---"
        Get-ChildItem $_.FullName | Select-Object -ExpandProperty Name
        Get-Content (Join-Path $_.FullName "profiles.json") -Raw
        Write-Output "active-profile: $(Get-Content (Join-Path $_.FullName 'active-profile') -Raw)"
    }
```

Expected:
- The directory contains exactly `profiles.json` and `active-profile` — no leftover `*.tmp` files.
- `profiles.json` contains **two** entries: `"default"` (the seed) and `"temp"` (written by `LoginProcessor`, which uses `ProfileNameValidator.ReservedTempName` for a bare login).
- `active-profile` contains `temp`.

This is the proof: the stub never touched disk, so these files existing with a `temp` entry means the benchmark ran the real `ProfileService`. If `profiles.json` holds only `default`, the login iterations did not persist and the registration is wrong.

- [ ] **Step 12: Restore the PS iteration counts and clean up the leftover store**

In `tests/Benchmark/PS/PSBenchmarkBase.cs`, change `[WarmupCount(5)]` back to `[WarmupCount(5000)]` and `[IterationCount(5)]` back to `[IterationCount(5000)]`.

Then verify and clean up:
```powershell
git diff -- tests/Benchmark/PS/PSBenchmarkBase.cs
Remove-Item -Recurse -Force .\artifacts\Benchmark\home -ErrorAction SilentlyContinue
```
Expected: the diff is empty (the file is byte-identical to `HEAD`), and the leftover store is gone.

- [ ] **Step 13: Commit**

```powershell
git add tests/Benchmark/BenchmarkProfileStore.cs tests/Benchmark/BenchmarkBase.cs
git commit -m "test(benchmark): measure real profile file I/O

Benchmarks registered StubProfileService, so profile persistence was
measured as dictionary writes rather than the JSON read plus atomic
temp-write and rename that ProfileService performs in production. Login
was the clearest case: SaveAndActivateProfileAsync touches three files
and cost nothing in the numbers.

Register the real ProfileService against a process-scoped store under
artifacts/Benchmark/home, seeded once per process so every iteration
measures the warm steady state rather than the create-from-scratch path.
Seeding goes through ProfileService itself so the on-disk format cannot
drift from production.

StubProfileService is unchanged and still backs the unit and scenario
tests.

Co-authored-by: Copilot App <223556219+Copilot@users.noreply.github.com>"
```

---

### Task 2: Delete the store after PowerShell benchmark runs

**Files:**
- Modify: `tests/Benchmark/PS/PSBenchmarkBase.cs` (append a method after `IterationCleanup`, which ends at line 76)

**Interfaces:**
- Consumes: `BenchmarkBase.ProfileStore` and `BenchmarkProfileStore.Delete()` from Task 1.
- Produces: nothing consumed by later tasks.

**Why only PS:** `CLIBenchmarkBase.GlobalCleanup` already deletes the whole `artifacts/Benchmark/home` tree, which contains the new store, so the CLI side needs no change. `PSBenchmarkBase` has no `[GlobalCleanup]` at all, which is why Task 1 could inspect its leftovers. BenchmarkDotNet permits only one `[GlobalCleanup]` per type hierarchy, so this must go on `PSBenchmarkBase` and **not** on `BenchmarkBase` — putting it on the base would collide with the one `CLIBenchmarkBase` already declares.

- [ ] **Step 1: Add the `[GlobalCleanup]` to `PSBenchmarkBase`**

Find the end of `IterationCleanup`:

```csharp
    [IterationCleanup]
    public void IterationCleanup()
    {
        CleanupCommon();

        _runspace?.Close();
        _runspace?.Dispose();
        _runspace = null!;
    }
```

Insert this immediately after it:

```csharp
    /// <summary>
    /// Removes the benchmark profile store. The CLI base cleans the whole
    /// <c>artifacts/Benchmark/home</c> tree in its own <c>[GlobalCleanup]</c>; the PS base
    /// had none, so without this a PS-only run leaves the store behind.
    /// </summary>
    [GlobalCleanup]
    public void GlobalCleanup()
    {
        ProfileStore.Delete();
    }
```

- [ ] **Step 2: Build**

Run: `dotnet build azclips.slnx -c Release -p:CopilotNpmRegistryUrl=https://packagefeedproxy.microsoft.io/npm/`
Expected: build succeeds, 0 errors.

A BenchmarkDotNet validation error mentioning multiple `[GlobalCleanup]` methods means one was added to `BenchmarkBase` or `CLIBenchmarkBase` by mistake — it belongs only on `PSBenchmarkBase`.

- [ ] **Step 3: Temporarily lower the PS iteration count**

In `tests/Benchmark/PS/PSBenchmarkBase.cs`, change `[WarmupCount(5000)]` to `[WarmupCount(5)]` and `[IterationCount(5000)]` to `[IterationCount(5)]`.

**Treat Steps 3–6 as try/finally: Step 6 restores both to `5000` and must run even if Step 4 or 5 fails.**

- [ ] **Step 4: Run the PS Login benchmark again**

Run:
```powershell
dotnet run --project tests\Benchmark\Benchmark.csproj -- --local --product PS --extension Login
```
Expected: exit code `0` and `[Benchmark] PASS — all benchmarks completed successfully.`

- [ ] **Step 5: Confirm no store is left behind**

Run:
```powershell
Get-ChildItem -Path .\artifacts\Benchmark\home -Filter ".azclips-*" -Directory -ErrorAction SilentlyContinue
```
Expected: no output. Before this task the same command listed a directory, so an empty result is the behaviour change.

- [ ] **Step 6: Restore the PS iteration counts**

Change `[WarmupCount(5)]` back to `[WarmupCount(5000)]` and `[IterationCount(5)]` back to `[IterationCount(5000)]`.

Run: `git diff -- tests/Benchmark/PS/PSBenchmarkBase.cs`
Expected: the diff shows **only** the new `GlobalCleanup` method — no iteration-count changes.

- [ ] **Step 7: Commit**

```powershell
git add tests/Benchmark/PS/PSBenchmarkBase.cs
git commit -m "test(benchmark): remove the profile store after PS runs

CLIBenchmarkBase already deletes the artifacts/Benchmark/home tree in its
GlobalCleanup, but PSBenchmarkBase had no GlobalCleanup at all, so a
PS-only benchmark run left its seeded profile store on disk.

Co-authored-by: Copilot App <223556219+Copilot@users.noreply.github.com>"
```

---

### Task 3: Documentation and final validation

**Files:**
- Modify: `tests/Common/Stubs/StubProfileService.cs` (XML doc, lines 8–11)
- Modify: `wiki/02-deep-dive/testing-benchmarking.md` (line 92)
- Modify: `tests/Benchmark/README.md` (lines 85, 86, 88, 97)

**Interfaces:**
- Consumes: the behaviour established in Tasks 1 and 2.
- Produces: nothing consumed by later tasks.

- [ ] **Step 1: Update the `StubProfileService` doc comment**

In `tests/Common/Stubs/StubProfileService.cs`, replace:

```csharp
/// <summary>
/// In-memory <see cref="IProfileService"/> stub seeded with a single active "default" profile,
/// preserving the connection state previously provided by the removed AuthContext stub.
/// </summary>
```

with:

```csharp
/// <summary>
/// In-memory <see cref="IProfileService"/> stub seeded with a single active "default" profile,
/// preserving the connection state previously provided by the removed AuthContext stub.
/// </summary>
/// <remarks>
/// Used by the unit and scenario test suites, where profile persistence is incidental and
/// file I/O would only add cost and flakiness. Benchmarks deliberately do <b>not</b> use this
/// stub: they register the real file-backed <c>ProfileService</c> against a scoped directory
/// (see <c>BenchmarkProfileStore</c>) so profile persistence is actually measured.
/// </remarks>
```

Do not change any code in this file.

- [ ] **Step 2: Update the wiki**

In `wiki/02-deep-dive/testing-benchmarking.md`, replace line 92:

```markdown
Benchmarks stay deterministic by reusing the *same* stub infrastructure as the unit tests. `BenchmarkBase.RegisterBenchmarkStubs()` overrides the DI container to register `StubHttpTerminateHandler`, `StubTelemetryService`, `StubProfileService`, and `StubAuthService` before each iteration, then disposes the container in cleanup (`tests/Benchmark/BenchmarkBase.cs:41-55`).
```

with:

```markdown
Benchmarks stay deterministic by reusing the *same* stub infrastructure as the unit tests. `BenchmarkBase.RegisterBenchmarkStubs()` overrides the DI container to register `StubHttpTerminateHandler`, `StubTelemetryService`, and `StubAuthService` before each iteration, then disposes the container in cleanup (`tests/Benchmark/BenchmarkBase.cs`).

Profiles are the deliberate exception: benchmarks register the *real* file-backed `ProfileService`, not `StubProfileService`. Profile persistence is a genuine cost of commands like `login` — `SaveAndActivateProfileAsync` reads `profiles.json`, rewrites it atomically, and rewrites the `active-profile` pointer — and stubbing it out hid that cost. `BenchmarkProfileStore` points the service at a process-scoped `artifacts/Benchmark/home/.azclips-<pid>` directory, seeded once per process so each iteration measures the warm steady state rather than the first-invocation path, and never touches the developer's real `~/.azclips` (`tests/Benchmark/BenchmarkProfileStore.cs`).
```

- [ ] **Step 3: Update the Benchmark README lifecycle bullets**

In `tests/Benchmark/README.md`, replace line 85:

```markdown
- `[GlobalSetup]` (once per class): discovers the repo root; for CLI it also stages the required extensions into a benchmark-scoped `artifacts/Benchmark/home/extensions` layout; for PS it stages the module manifest closure under `artifacts/Benchmark/PS`.
```

with:

```markdown
- `[GlobalSetup]` (once per class): discovers the repo root and seeds a benchmark-scoped profile store at `artifacts/Benchmark/home/.azclips-<pid>`; for CLI it also stages the required extensions into a benchmark-scoped `artifacts/Benchmark/home/extensions` layout; for PS it stages the module manifest closure under `artifacts/Benchmark/PS`.
```

Replace line 86:

```markdown
- `[IterationSetup]` (before each run): re-registers stub HTTP/auth/telemetry services; for CLI it also re-registers the extension loaders + registry store pointed at the staged home; for PS it creates and opens a fresh runspace and imports the module (both excluded from the measurement, so only the cmdlet invocation is measured).
```

with:

```markdown
- `[IterationSetup]` (before each run): re-registers stub HTTP/auth/telemetry services plus the real file-backed `ProfileService` pointed at the seeded store; for CLI it also re-registers the extension loaders + registry store pointed at the staged home; for PS it creates and opens a fresh runspace and imports the module (both excluded from the measurement, so only the cmdlet invocation is measured).
```

Replace line 88:

```markdown
- `[GlobalCleanup]` (once per class): removes the staged extensions home (best-effort).
```

with:

```markdown
- `[GlobalCleanup]` (once per class): removes the staged extensions home for CLI, and the seeded profile store for PS (both best-effort).
```

- [ ] **Step 4: Update the Benchmark README stubbing paragraph**

Replace line 97:

```markdown
HTTP is short-circuited by a resource-aware stub terminate handler (`StubHttpTerminateHandler`, backed by `StubResponseMappings` + `Stubs/Fixtures/*.json`): it returns a minimal, correctly-shaped ARM body per resource type (or `{}` for unmapped paths) plus a `Retry-After: 0` header, so benchmarks measure the local CLI/PS pipeline only, never call Azure, and orchestrated long-running creates complete without real polling.
```

with:

```markdown
HTTP is short-circuited by a resource-aware stub terminate handler (`StubHttpTerminateHandler`, backed by `StubResponseMappings` + `Stubs/Fixtures/*.json`): it returns a minimal, correctly-shaped ARM body per resource type (or `{}` for unmapped paths) plus a `Retry-After: 0` header, so benchmarks measure the local CLI/PS pipeline only, never call Azure, and orchestrated long-running creates complete without real polling.

Profile persistence is **not** stubbed. Benchmarks use the real `ProfileService` against a process-scoped store (`BenchmarkProfileStore`), so the JSON read, serialize, and atomic temp-write-plus-rename that `login` performs are included in the measurement. The store is seeded before the first iteration, so every iteration measures the warm path a user hits after installation rather than the one-off create-from-scratch path.
```

- [ ] **Step 5: Build**

Run: `dotnet build azclips.slnx -c Release -p:CopilotNpmRegistryUrl=https://packagefeedproxy.microsoft.io/npm/`
Expected: build succeeds, 0 errors. (Only the XML doc comment is code-adjacent, but confirm nothing broke.)

- [ ] **Step 6: Run the full benchmark smoke test**

Invoke the `validate-benchmarks` skill.

**Two corrections to that skill's instructions — apply both:**
1. It states the iteration attribute is `[IterationCount(10000)]`. The repo value is **`[IterationCount(5000)]`**. Restore to `5000`, never `10000`.
2. It only mentions `[IterationCount]`. Also lower and restore `[WarmupCount(5000)]` in the same two files, otherwise 5,000 warmup iterations still run and the smoke test is not fast.

The two files are `tests/Benchmark/CLI/CLIBenchmarkBase.cs` (lines 20–21) and `tests/Benchmark/PS/PSBenchmarkBase.cs` (lines 17–18).

The run itself:
```powershell
dotnet run --project tests\Benchmark\Benchmark.csproj -- --local --product ALL
```
Expected: exit code `0` and `[Benchmark] PASS — all benchmarks completed successfully.`

- [ ] **Step 7: Verify the restore and a clean tree**

Run:
```powershell
git diff -- tests/Benchmark/CLI/CLIBenchmarkBase.cs tests/Benchmark/PS/PSBenchmarkBase.cs
Select-String -Path tests\Benchmark\CLI\CLIBenchmarkBase.cs,tests\Benchmark\PS\PSBenchmarkBase.cs -Pattern "IterationCount|WarmupCount"
Get-ChildItem -Path .\artifacts\Benchmark\home -Filter ".azclips-*" -Directory -ErrorAction SilentlyContinue
```

Expected:
- The `git diff` for `CLIBenchmarkBase.cs` is empty, and for `PSBenchmarkBase.cs` shows only the Task 2 `GlobalCleanup` method.
- Every match shows `5000`; no `(5)` remains.
- No leftover `.azclips-*` directory.

Do not finish until all three hold.

- [ ] **Step 8: Commit**

```powershell
git add tests/Common/Stubs/StubProfileService.cs wiki/02-deep-dive/testing-benchmarking.md tests/Benchmark/README.md
git commit -m "docs: record that benchmarks use real profile file I/O

The wiki and benchmark README both listed StubProfileService among the
benchmark stubs, which is no longer true. Document the real ProfileService
registration, the process-scoped store, and why profiles are the one
deliberate exception to benchmark stubbing.

Co-authored-by: Copilot App <223556219+Copilot@users.noreply.github.com>"
```

---

## Self-Review

**Spec coverage:**

| Spec requirement | Task |
|---|---|
| Register real `ProfileService` in the benchmark DI override | Task 1, Step 5 |
| Benchmark-scoped directory, never the real `~/.azclips` | Task 1, Steps 1 and 5 |
| Process-id suffix for clean per-process state | Task 1, Step 1 |
| Seed via `ProfileService` for format parity | Task 1, Step 1 |
| Warm (seeded) rather than cold measurement | Task 1, Step 1; verified Step 11 |
| No-op logger for silent seeding | Task 1, Step 1; verified Step 10 |
| Init hangs off `InitializeCommon`, not a new `[GlobalSetup]` | Task 1, Step 4 |
| `[GlobalCleanup]` on `PSBenchmarkBase` only | Task 2, Step 1 |
| `StubProfileService` behaviour unchanged, doc updated | Task 3, Step 1; regression-checked Task 1, Step 7 |
| Wiki line 92 updated | Task 3, Step 2 |
| Other three stubs untouched | Task 1, Step 5 (verbatim retained) |
| `StartupBenchmarkBase` unaffected | No task needed — it does not derive from `BenchmarkBase` |
| Validation: build, tests, `validate-benchmarks` | Task 1 Steps 6–7; Task 3 Steps 5–7 |
| Validation: no verbose flood, no leftover directory | Task 1 Step 10; Task 3 Step 7 |

Two items go beyond the spec and are deliberate: the `Directory` → `RootDirectory` rename (avoids shadowing `System.IO.Directory`) and the `tests/Benchmark/README.md` updates (the spec named only the wiki, but the README documents the same lifecycle and would otherwise be left stale).

**Placeholder scan:** no `TBD`, `TODO`, "implement later", "add appropriate error handling", or "similar to Task N". Every code step shows literal before/after content. Every command is runnable as written.

**Type consistency:** `RootDirectory`, `Initialize(string repoRoot)`, and `Delete()` are defined in Task 1 Step 1 and used with those exact names in Task 1 Step 5 and Task 2 Step 1. `ProfileStore` is declared in Task 1 Step 3 and consumed in Task 1 Step 4, Task 1 Step 5, and Task 2 Step 1. `ILoggingService`'s six members match the interface at `src/Core/Core.Abstractions/ILoggingService.cs`. `Profile`'s initialized properties match the `record` at `src/Core/Core.Abstractions/Models/Profile.cs`. The `ProfileService(ILoggingService, string?)` constructor matches `src/Core/Core.Runtime/ProfileService.cs:22`.
