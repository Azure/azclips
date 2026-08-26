# Benchmark Home Paths Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Give the benchmark home tree and the telemetry staging root one owner, so the file that deletes the tree and the three files that populate it agree by construction rather than by coincidence.

**Architecture:** A new `BenchmarkPaths` static class in `Tests.Common.Utilities`, modelled on `Core.Abstractions.HomePaths` — name constants plus composition methods taking `repoRoot`. It must live in `Tests.Common` because `BenchmarkProfileStore` already lives there and cannot reference the `Benchmark` executable; `Tests.Common` is the only assembly both can see.

**Tech Stack:** C# / .NET 10, xUnit, BenchmarkDotNet.

## Global Constraints

- Build with `dotnet build azclips.slnx -p:CopilotNpmRegistryUrl=https://packagefeedproxy.microsoft.io/npm/`. Use the .NET SDK pinned in `global.json`.
- C# with nullable enabled and implicit usings. Follow existing naming, namespace, and folder conventions.
- Every new file starts with exactly two header lines: `// Copyright (c) Microsoft Corporation.` then `// Licensed under the MIT License.`
- **Every path this change touches must resolve to the byte-identical string it resolves to today.** This is a pure centralisation; a changed directory is a defect, not an improvement.
- The telemetry staging root is `artifacts/BenchmarkTelemetry` — a **sibling** of `artifacts/Benchmark`, never a child. Moving it under `artifacts/Benchmark` reintroduces a Windows failure where an on-access scanner holds `artifacts/Benchmark/home/.azclips/profiles.json` open and the per-iteration profile reseed then fails, because a file with an open handle cannot be deleted. This reason must appear in the XML doc.
- Scope is the home tree plus the telemetry staging root. Do **not** migrate: the timestamped/CI run paths in `Program.cs`, the PS staging root in `PSManifestStager`, the build-output paths in `CLIBenchmarkBase`/`StartupBenchmarkBase`, `MsalCacheFixture`'s `AppContext.BaseDirectory` fixture path, or `ChartGenerator`'s filenames.
- Do not change `[WarmupCount]` or `[IterationCount]`; both stay 5000 in committed code.

---

### Task 1: Add `BenchmarkPaths`

The type every later task migrates to. Nothing consumes it yet after this task — that is expected.

**Files:**
- Create: `tests/Common/Utilities/BenchmarkPaths.cs`
- Create: `tests/Core.Extensions.Tests/BenchmarkPathsTests.cs`

**Interfaces:**
- Consumes: nothing. Pure `Path.Combine` composition.
- Produces, all `public static string` taking `string repoRoot`:
  - `GetBenchmarkRoot(repoRoot)` → `<repoRoot>/artifacts/Benchmark`
  - `GetHomeRoot(repoRoot)` → `<repoRoot>/artifacts/Benchmark/home`
  - `GetExtensionsRoot(repoRoot)` → `<repoRoot>/artifacts/Benchmark/home/extensions`
  - `GetMsalCacheDirectory(repoRoot)` → `<repoRoot>/artifacts/Benchmark/home/msal`
  - `GetProfileStoreRoot(repoRoot)` → `<repoRoot>/artifacts/Benchmark/home/.azclips`
  - `GetTelemetryStagingRoot(repoRoot)` → `<repoRoot>/artifacts/BenchmarkTelemetry`

- [ ] **Step 1: Write the failing tests**

Create `tests/Core.Extensions.Tests/BenchmarkPathsTests.cs`:

```csharp
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Tests.Common.Utilities;

namespace Core.Extensions.Tests;

public class BenchmarkPathsTests
{
    private const string RepoRoot = "/repo";

    [Fact]
    public void HomeChildren_ShouldAllSitUnderTheHomeRoot_SoDeletingItReclaimsEveryOne()
    {
        // CLIBenchmarkBase.GlobalCleanup deletes the home root. Anything seeded outside it would
        // survive that delete and leak between benchmark classes.
        var home = BenchmarkPaths.GetHomeRoot(RepoRoot);

        Assert.StartsWith(home, BenchmarkPaths.GetExtensionsRoot(RepoRoot), StringComparison.Ordinal);
        Assert.StartsWith(home, BenchmarkPaths.GetMsalCacheDirectory(RepoRoot), StringComparison.Ordinal);
        Assert.StartsWith(home, BenchmarkPaths.GetProfileStoreRoot(RepoRoot), StringComparison.Ordinal);
    }

    [Fact]
    public void TelemetryStagingRoot_ShouldBeASiblingOfTheBenchmarkRoot_NotAChild()
    {
        // Writing telemetry inside artifacts/Benchmark makes on-access scanners walk that tree; a
        // scanner holding profiles.json open then breaks the per-iteration reseed on Windows.
        var benchmarkRoot = BenchmarkPaths.GetBenchmarkRoot(RepoRoot);
        var telemetryRoot = BenchmarkPaths.GetTelemetryStagingRoot(RepoRoot);

        Assert.False(
            telemetryRoot.StartsWith(benchmarkRoot + Path.DirectorySeparatorChar, StringComparison.Ordinal),
            $"Telemetry staging '{telemetryRoot}' must not sit under '{benchmarkRoot}'.");

        Assert.Equal(
            Path.GetDirectoryName(benchmarkRoot),
            Path.GetDirectoryName(telemetryRoot));
    }

    [Fact]
    public void Paths_ShouldMatchTheDirectoriesTheBenchmarksAlreadyUse()
    {
        // Pins the literals that existed before centralisation. A change here relocates real
        // benchmark output, so it must be deliberate.
        Assert.Equal(
            Path.Combine(RepoRoot, "artifacts", "Benchmark"),
            BenchmarkPaths.GetBenchmarkRoot(RepoRoot));
        Assert.Equal(
            Path.Combine(RepoRoot, "artifacts", "Benchmark", "home"),
            BenchmarkPaths.GetHomeRoot(RepoRoot));
        Assert.Equal(
            Path.Combine(RepoRoot, "artifacts", "Benchmark", "home", "extensions"),
            BenchmarkPaths.GetExtensionsRoot(RepoRoot));
        Assert.Equal(
            Path.Combine(RepoRoot, "artifacts", "Benchmark", "home", "msal"),
            BenchmarkPaths.GetMsalCacheDirectory(RepoRoot));
        Assert.Equal(
            Path.Combine(RepoRoot, "artifacts", "Benchmark", "home", ".azclips"),
            BenchmarkPaths.GetProfileStoreRoot(RepoRoot));
        Assert.Equal(
            Path.Combine(RepoRoot, "artifacts", "BenchmarkTelemetry"),
            BenchmarkPaths.GetTelemetryStagingRoot(RepoRoot));
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test tests/Core.Extensions.Tests/Core.Extensions.Tests.csproj -c Release --filter "FullyQualifiedName~BenchmarkPathsTests"`
Expected: FAIL to compile — `The name 'BenchmarkPaths' does not exist in the current context`.

- [ ] **Step 3: Create the type**

Create `tests/Common/Utilities/BenchmarkPaths.cs`:

```csharp
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Tests.Common.Utilities;

/// <summary>
/// Repository-relative locations benchmarks read and write. Centralised so the code that clears a
/// directory and the code that populates it cannot disagree.
/// </summary>
public static class BenchmarkPaths
{
    public const string ArtifactsDirectoryName = "artifacts";
    public const string BenchmarkDirectoryName = "Benchmark";
    public const string TelemetryStagingDirectoryName = "BenchmarkTelemetry";
    public const string HomeDirectoryName = "home";
    public const string ExtensionsDirectoryName = "extensions";
    public const string MsalCacheDirectoryName = "msal";
    public const string ProfileStoreDirectoryName = ".azclips";

    /// <summary>Root for benchmark run output and the benchmark-scoped home tree.</summary>
    public static string GetBenchmarkRoot(string repoRoot) =>
        Path.Combine(repoRoot, ArtifactsDirectoryName, BenchmarkDirectoryName);

    /// <summary>
    /// A stand-in for the real per-user <c>~/.azclips</c> home, so benchmarks exercise file-backed
    /// paths without touching the developer's own profile.
    /// </summary>
    /// <remarks>
    /// Holds <see cref="GetExtensionsRoot"/>, <see cref="GetMsalCacheDirectory"/>, and
    /// <see cref="GetProfileStoreRoot"/>. <c>CLIBenchmarkBase.GlobalCleanup</c> deletes this whole
    /// tree, so anything seeded for a benchmark belongs under here rather than beside it.
    /// </remarks>
    public static string GetHomeRoot(string repoRoot) =>
        Path.Combine(GetBenchmarkRoot(repoRoot), HomeDirectoryName);

    /// <summary>Staged extensions, in the layout the resolver and loaders expect.</summary>
    public static string GetExtensionsRoot(string repoRoot) =>
        Path.Combine(GetHomeRoot(repoRoot), ExtensionsDirectoryName);

    /// <summary>Seeded MSAL token cache, so auth file IO is measured and the network is not hit.</summary>
    public static string GetMsalCacheDirectory(string repoRoot) =>
        Path.Combine(GetHomeRoot(repoRoot), MsalCacheDirectoryName);

    /// <summary>Seeded profile store backing the real, file-backed <c>ProfileService</c>.</summary>
    public static string GetProfileStoreRoot(string repoRoot) =>
        Path.Combine(GetHomeRoot(repoRoot), ProfileStoreDirectoryName);

    /// <summary>
    /// Where benchmark processes stage traces before the host collects them.
    /// </summary>
    /// <remarks>
    /// A sibling of <see cref="GetBenchmarkRoot"/> rather than a child, and it must stay one.
    /// Creating files inside that tree makes on-access scanners walk it, and a scanner holding
    /// <c>home/.azclips/profiles.json</c> open breaks the per-iteration profile reseed on Windows,
    /// where a file with an open handle cannot be deleted.
    /// </remarks>
    public static string GetTelemetryStagingRoot(string repoRoot) =>
        Path.Combine(repoRoot, ArtifactsDirectoryName, TelemetryStagingDirectoryName);
}
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test tests/Core.Extensions.Tests/Core.Extensions.Tests.csproj -c Release --filter "FullyQualifiedName~BenchmarkPathsTests"`
Expected: PASS (3 tests).

- [ ] **Step 5: Commit**

```bash
git add tests/Common/Utilities/BenchmarkPaths.cs tests/Core.Extensions.Tests/BenchmarkPathsTests.cs
git commit -m "{Benchmark} Add BenchmarkPaths for the benchmark home tree"
```

---

### Task 2: Migrate the home tree and the telemetry staging root

Point all five call sites at `BenchmarkPaths`. Behaviour must not change.

Note `tests/Benchmark/Program.cs` currently calls `BenchmarkTelemetryPaths.StagingRoot(...)`, a type deleted in commit `5ccdb0ca7`, so the Benchmark project does not build at HEAD. Step 4 fixes that as a side effect of the migration — it is not separate work.

**Files:**
- Modify: `tests/Common/Utilities/BenchmarkProfileStore.cs` — `GetRoot`
- Modify: `tests/Benchmark/BenchmarkBase.cs` — `MsalCacheDirectory` assignment
- Modify: `tests/Benchmark/CLI/CLIBenchmarkBase.cs` — `_extensionsRoot` assignment, `GlobalCleanup` delete
- Modify: `tests/Benchmark/Telemetry/BenchmarkTelemetryArtifacts.cs` — `_stagingRoot` assignment
- Modify: `tests/Benchmark/Program.cs` — `telemetryStagingRoot` assignment

**Interfaces:**
- Consumes: `BenchmarkPaths` from Task 1.
- Produces: nothing new. `BenchmarkProfileStore.GetRoot(string repoRoot)` keeps its signature and its callers.

- [ ] **Step 1: Point `BenchmarkProfileStore.GetRoot` at the shared helper**

In `tests/Common/Utilities/BenchmarkProfileStore.cs`, replace the `GetRoot` member and its doc comment:

```csharp
    /// <summary>Store location. See <see cref="BenchmarkPaths.GetProfileStoreRoot"/>.</summary>
    public static string GetRoot(string repoRoot) => BenchmarkPaths.GetProfileStoreRoot(repoRoot);
```

Same file and namespace (`Tests.Common.Utilities`), so no `using` change is needed.

- [ ] **Step 2: Point `BenchmarkBase` at the shared helper**

In `tests/Benchmark/BenchmarkBase.cs`, replace the `MsalCacheDirectory` assignment (currently `Path.Combine(RepoRoot, "artifacts", "Benchmark", "home", "msal")`):

```csharp
        MsalCacheDirectory = BenchmarkPaths.GetMsalCacheDirectory(RepoRoot);
```

`using Tests.Common.Utilities;` is already present in this file.

- [ ] **Step 3: Point `CLIBenchmarkBase` at the shared helper**

In `tests/Benchmark/CLI/CLIBenchmarkBase.cs`, replace the `_extensionsRoot` assignment (currently `Path.Combine(RepoRoot, "artifacts", "Benchmark", "home", "extensions")`), keeping the comment above it:

```csharp
        _extensionsRoot = BenchmarkPaths.GetExtensionsRoot(RepoRoot);
```

Then, in `GlobalCleanup`, replace the home-directory delete argument (currently `Path.Combine(RepoRoot, "artifacts", "Benchmark", "home")`), keeping the comment above it:

```csharp
        DirectoryUtils.TryDeleteDirectory(BenchmarkPaths.GetHomeRoot(RepoRoot));
```

`DirectoryUtils` lives in `Tests.Common.Utilities` and is the helper this file already uses for both of its deletes. `using Tests.Common.Utilities;` is already present.

- [ ] **Step 4: Point the telemetry staging paths at the shared helper**

In `tests/Benchmark/Telemetry/BenchmarkTelemetryArtifacts.cs`, replace the `_stagingRoot` assignment (currently `Path.Combine(repoRoot, "artifacts", "BenchmarkTelemetry")`):

```csharp
        _stagingRoot = BenchmarkPaths.GetTelemetryStagingRoot(repoRoot);
```

`using Tests.Common.Utilities;` is already present in that file.

In `tests/Benchmark/Program.cs`, replace the `telemetryStagingRoot` assignment. It currently reads `BenchmarkTelemetryPaths.StagingRoot(RepoLocator.GetRepoRoot())`, referencing a deleted type:

```csharp
        var telemetryStagingRoot = BenchmarkPaths.GetTelemetryStagingRoot(RepoLocator.GetRepoRoot());
```

`using Tests.Common.Utilities;` is already present in `Program.cs`.

- [ ] **Step 5: Verify no benchmark home or telemetry literals remain**

Run:

```powershell
Select-String -Path tests\Benchmark\*.cs,tests\Benchmark\**\*.cs,tests\Common\Utilities\BenchmarkProfileStore.cs `
  -Pattern '"home"|"msal"|"\.azclips"|"BenchmarkTelemetry"'
```

Expected: no matches. A hit means a call site was missed.

Run: `Select-String -Path tests\Benchmark\Program.cs -Pattern 'BenchmarkTelemetryPaths'`
Expected: no matches — the deleted type is no longer referenced.

- [ ] **Step 6: Build**

Run: `dotnet build azclips.slnx -p:CopilotNpmRegistryUrl=https://packagefeedproxy.microsoft.io/npm/`
Expected: Build succeeded, 0 errors. This also confirms the pre-existing `BenchmarkTelemetryPaths` break is resolved.

- [ ] **Step 7: Commit**

```bash
git add tests/Common/Utilities/BenchmarkProfileStore.cs tests/Benchmark/BenchmarkBase.cs tests/Benchmark/CLI/CLIBenchmarkBase.cs tests/Benchmark/Telemetry/BenchmarkTelemetryArtifacts.cs tests/Benchmark/Program.cs
git commit -m "{Benchmark} Resolve the benchmark home tree through BenchmarkPaths"
```

---

### Task 3: Verify end-to-end

The unit tests pin the strings; only a live run proves the directories are still the ones the benchmarks actually use.

**Files:**
- Temporarily modify then restore: `tests/Benchmark/PS/PSBenchmarkBase.cs`, `tests/Benchmark/CLI/CLIBenchmarkBase.cs` (iteration counts only)

**Interfaces:**
- Consumes: everything from Tasks 1-2.
- Produces: nothing.

- [ ] **Step 1: Confirm the working tree is clean**

Run: `git status --short`
Expected: empty. Tasks 1-2 must be committed before the iteration-count edits below, so `git checkout --` can restore them safely.

- [ ] **Step 2: Run the targeted tests**

Run: `dotnet test tests/Core.Extensions.Tests/Core.Extensions.Tests.csproj -c Release`
Expected: PASS, including the 3 new `BenchmarkPathsTests`.

Run: `dotnet test tests/Cli.Tests/Cli.Tests.csproj -c Release -p:CopilotNpmRegistryUrl=https://packagefeedproxy.microsoft.io/npm/`
Expected: PASS except `ManagedIdentityE2ETests.ManagedIdentity_E2E_MultipleIdSelectors_ShouldFail`, which is a known pre-existing failure — it resolves the CLI binary as `az` where Windows produces `az.exe`. If anything else fails, stop and investigate.

- [ ] **Step 3: Build Release and stage the PS modules**

Run:

```
dotnet build -c Release azclips.slnx -p:CopilotNpmRegistryUrl=https://packagefeedproxy.microsoft.io/npm/
./build/Set-ModuleConfiguration.ps1 -c Release
```

Expected: both succeed. Benchmarks load Release output, so a Debug-only build makes `GlobalSetup` throw.

- [ ] **Step 4: Lower the iteration counts**

In BOTH `tests/Benchmark/PS/PSBenchmarkBase.cs` and `tests/Benchmark/CLI/CLIBenchmarkBase.cs`, change `[WarmupCount(5000)]` → `[WarmupCount(5)]` and `[IterationCount(5000)]` → `[IterationCount(5)]`. Change nothing else.

- [ ] **Step 5: Run the benchmarks**

Run: `dotnet run --project tests\Benchmark\Benchmark.csproj -c Release -- --local --product ALL --extension Resources`

Expected: exit code 0 and a `[Benchmark] PASS` line, plus one `[Telemetry] <name>: kept 5 sampled trace(s)` per benchmark method. A `kept 0` line means telemetry did not reach the measured directory — treat it as a failure.

Do not interpret the timing statistics; this is an execution check.

- [ ] **Step 6: Assert the directories are unchanged**

Run:

```powershell
$run = Get-ChildItem artifacts\Benchmark -Directory |
    Where-Object { $_.Name -match '^\d{4}-\d{2}-\d{2}_' } |
    Sort-Object Name -Descending | Select-Object -First 1

[pscustomobject]@{
    RunTelemetryFiles = @(Get-ChildItem (Join-Path $run.FullName 'telemetry') -Filter '*.jsonl').Count
    StagingCleanedUp  = -not (Test-Path 'artifacts\BenchmarkTelemetry')
    HomeCleanedUp     = -not (Test-Path 'artifacts\Benchmark\home')
    WrongTelemetryDir = Test-Path 'artifacts\Benchmark\telemetry'
} | Format-List
```

Expected: `RunTelemetryFiles` greater than 0; `StagingCleanedUp` and `HomeCleanedUp` both `True`; `WrongTelemetryDir` `False`.

`WrongTelemetryDir` being `True` is the specific regression this change must not cause — it means telemetry staging moved back under `artifacts/Benchmark`, which is what breaks the profile reseed on Windows.

- [ ] **Step 7: Restore the iteration counts**

This is mandatory even if the run failed. Run:

```bash
git checkout -- tests/Benchmark/PS/PSBenchmarkBase.cs tests/Benchmark/CLI/CLIBenchmarkBase.cs
```

Then verify: `git diff -- tests/Benchmark/PS/PSBenchmarkBase.cs tests/Benchmark/CLI/CLIBenchmarkBase.cs`
Expected: empty diff, and both files contain `[WarmupCount(5000)]` and `[IterationCount(5000)]`.

- [ ] **Step 8: Commit any fixes**

Only if Steps 2-6 required changes:

```bash
git add -A
git commit -m "{Benchmark} Fix path defects found in validation"
```

---

## Notes for the implementer

**Why `Tests.Common` and not `tests/Benchmark`.** `BenchmarkProfileStore` lives in `Tests.Common` and is consumed by `BenchmarkBase`. `Tests.Common` cannot reference the `Benchmark` executable, so a `BenchmarkPaths` inside `tests/Benchmark` could not be used by `BenchmarkProfileStore.GetRoot` — which is one of the five call sites this change exists to unify.

**Why `GetRoot` survives rather than being deleted.** `BenchmarkProfileStore.GetRoot` is called from `BenchmarkBase` and reads naturally where it is used. Delegating keeps one definition of the path without churning its callers.

**Paths deliberately left alone.** The timestamped and CI run paths (`Program.cs`), the PS staging root (`PSManifestStager`), and the build-output paths (`CLIBenchmarkBase`, `StartupBenchmarkBase`) each have a single call site and no cross-file coupling, so centralising them would add indirection without removing a hazard. `.github/workflows/benchmark.yml` hard-codes `artifacts/Benchmark/ci`, which cannot consume a C# constant.
