# PowerShell Benchmark Telemetry Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make PowerShell micro-benchmarks emit real span telemetry into `warmup/` and `actual/` directories, sampled into one `.jsonl` per benchmark method, without putting the file write inside BenchmarkDotNet's measured region.

**Architecture:** One production line moves — the PowerShell module registers `ITelemetrySender` in the *prepend* phase instead of the *override* phase, so a benchmark host can substitute its own. `PSBenchmarkBase` then registers a benchmark-only sender that wires the production `FileExporter` behind a `BatchActivityExportProcessor`. Everything is per-iteration: the `TracerProvider` is built in `[IterationSetup]` and disposed in `[IterationCleanup]`, and that dispose is what drains the queue to disk. A tiny `StageLatch` processor reads the BenchmarkDotNet stage at span end — the only moment the answer is knowable — and the exporter reads that latch at drain time to choose its directory.

**Tech Stack:** C# / .NET 10, xUnit, BenchmarkDotNet 0.13.12, OpenTelemetry 1.15.3, `Microsoft.Extensions.DependencyInjection`.

**Design doc:** `docs/superpowers/specs/2026-08-20-ps-benchmark-telemetry-design.md`

## Global Constraints

- Build with `dotnet build azclips.slnx`. Use the .NET SDK pinned in `global.json`.
- C# with nullable enabled and implicit usings. Follow existing naming, namespace, and folder conventions.
- Every new file starts with the two-line copyright header used across the repo:
  `// Copyright (c) Microsoft Corporation.` / `// Licensed under the MIT License.`
- All new benchmark types live in `tests/Benchmark/Telemetry/`, namespace `Benchmark.Telemetry`, and are declared `internal sealed`. They must never be referenced from `src/`.
- Every new benchmark type's XML `<remarks>` must state (a) that it is benchmark-only and (b) whether its code runs inside BenchmarkDotNet's measured region.
- Telemetry failures must never change a benchmark's exit code. Catch, print a `[Telemetry]` line, continue.
- Do not modify `ITelemetrySender`, `DirectTelemetrySender`, `FileTelemetrySender`, `FileExporter`, `PSTelemetryProvider`, `TelemetryService`, `ImportTelemetry`, `StageTraceDirectoryProvider`, `TraceSampler`, `BenchmarkName`, `BenchmarkStageListener`, or `tests/Benchmark/Program.cs`.
- `Core.Telemetry.csproj` already has `InternalsVisibleTo Include="Benchmark"`, so the internal `FileExporter` is reachable from the benchmark project. Do not widen any accessibility.
- Preserve existing public behaviour. Production PowerShell must keep using `DirectTelemetrySender`.

---

### Task 1: Move the PowerShell sender registration to the prepend phase

The PowerShell module currently registers `ITelemetrySender` via `OverrideRegistration`, and calls `Initialize()` in the same method. A benchmark's registrations are enqueued *before* the import runs, so the module's override wins and registering afterwards throws. Moving only this one registration to the prepend phase inverts that: prepends run before the framework defaults and before every override, so a host that supplies its own sender wins, while production — where nothing else registers `ITelemetrySender` — is unchanged.

**Files:**
- Modify: `src/PS/Login/ModuleInitializer.cs:23-29`
- Test: `tests/Core.Telemetry.Tests/TelemetrySenderRegistrationTests.cs` (create)
- Test: `tests/Cmdlets.Tests/ModuleInitializerSenderRegistrationTests.cs` (create)

**Interfaces:**
- Consumes: `AzclipsServiceProvider.PrependRegistration(Action<IServiceCollection>)` and `AzclipsServiceProvider.OverrideRegistration(Action<IServiceCollection>)`, both existing in `Core.DependencyInjection`.
- Produces: the ordering guarantee every later task depends on — after `Import-Module`, `AzclipsServiceProvider.GetRequiredService<ITelemetrySender>()` returns the host's sender when the host registered one via `OverrideRegistration` before the import, and `DirectTelemetrySender` otherwise.

- [ ] **Step 1: Write the regression test that the framework defaults own no sender**

This is the fact the whole prepend scheme rests on. If a default is ever added, PowerShell silently loses `DirectTelemetrySender`.

Create `tests/Core.Telemetry.Tests/TelemetrySenderRegistrationTests.cs`:

```csharp
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Extensions.DependencyInjection;

namespace Core.Telemetry.Tests;

public class TelemetrySenderRegistrationTests
{
    [Fact]
    public void RegisterTelemetryServices_ShouldRegisterNoTelemetrySender_SoHostsPickTheirOwn()
    {
        // ModuleInitializer registers DirectTelemetrySender in the PREPEND phase, which runs
        // before these defaults. A default sender here would clobber it and silently disable
        // PowerShell's telemetry export.        var services = new ServiceCollection();
        services.RegisterTelemetryServices();

        Assert.DoesNotContain(services, descriptor => descriptor.ServiceType == typeof(ITelemetrySender));
    }
}
```

- [ ] **Step 2: Run it to confirm it passes against today's code**

Run: `dotnet test tests/Core.Telemetry.Tests/Core.Telemetry.Tests.csproj --filter "FullyQualifiedName~TelemetrySenderRegistrationTests"`
Expected: PASS. This test is a regression lock, not a red-green driver — it documents an existing property that must not change.

- [ ] **Step 3: Write the failing ordering tests**

Create `tests/Cmdlets.Tests/ModuleInitializerSenderRegistrationTests.cs`. The collection name matches the one `LoginProcessorValidationTests` already uses, so these tests never run in parallel with other tests that mutate the process-wide `AzclipsServiceProvider`:

```csharp
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.DependencyInjection;
using Core.Telemetry;
using Microsoft.Azure.Azclips.Cmdlets.Login;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Trace;

namespace Cmdlets.Tests;

/// <summary>
/// Locks the registration-phase contract that lets a host — PSBenchmarkBase — supply its own
/// telemetry sender to the PowerShell module.
/// </summary>
[Collection("Azclips Service Provider Tests")]
public class ModuleInitializerSenderRegistrationTests
{
    [Fact]
    public void OnImport_ShouldResolveDirectSender_WhenHostRegistersNone()
    {
        AzclipsServiceProvider.Dispose();
        try
        {
            new ModuleInitializer().OnImport();

            Assert.IsType<DirectTelemetrySender>(
                AzclipsServiceProvider.GetRequiredService<ITelemetrySender>());
        }
        finally
        {
            AzclipsServiceProvider.Dispose();
        }
    }

    [Fact]
    public void OnImport_ShouldResolveHostSender_WhenHostOverridesBeforeImport()
    {
        AzclipsServiceProvider.Dispose();
        try
        {
            // Mirrors PSBenchmarkBase: the host enqueues its override, then Import-Module runs
            // ModuleInitializer.OnImport. The override wins.
            AzclipsServiceProvider.OverrideRegistration(services =>
                services.AddSingleton<ITelemetrySender>(new StubTelemetrySender()));

            new ModuleInitializer().OnImport();

            Assert.IsType<StubTelemetrySender>(
                AzclipsServiceProvider.GetRequiredService<ITelemetrySender>());
        }
        finally
        {
            AzclipsServiceProvider.Dispose();
        }
    }

    private sealed class StubTelemetrySender : ITelemetrySender
    {
        public void ConfigureExporter(TracerProviderBuilder builder) { }
    }
}
```

- [ ] **Step 4: Run the ordering tests to verify the second one fails**

Run: `dotnet test tests/Cmdlets.Tests/Cmdlets.Tests.csproj --filter "FullyQualifiedName~ModuleInitializerSenderRegistrationTests"`
Expected: `OnImport_ShouldResolveDirectSender_WhenHostRegistersNone` PASSES, `OnImport_ShouldResolveHostSender_WhenHostOverridesBeforeImport` FAILS with an assertion that the resolved type was `DirectTelemetrySender` rather than `StubTelemetrySender`. That failure is the bug this task fixes: the module's override currently beats the host's.

- [ ] **Step 5: Move the registration to the prepend phase**

In `src/PS/Login/ModuleInitializer.cs`, replace the body of the `using (timeline.Measure(...))` block:

```csharp
        using (timeline.Measure(ImportTelemetry.ContainerSpanName))
        {
            // A default the host may replace: prepends run before the framework defaults and
            // before every override. Nothing in RegisterTelemetryServices registers
            // ITelemetrySender, so this is what PowerShell resolves.
            AzclipsServiceProvider.PrependRegistration(services =>
                services.AddSingleton<ITelemetrySender, DirectTelemetrySender>());

            // Overrides, because RegisterTelemetryServices registers DefaultTelemetryProvider
            // among the defaults and a prepend would lose to it.
            AzclipsServiceProvider.OverrideRegistration(services =>
            {
                services.AddSingleton<IArgumentParsingProvider, PSArgumentParsingProvider>();
                services.AddSingleton<ITelemetryProvider, PSTelemetryProvider>();
                services.AddSingleton<IUserAgentHandler, PSUserAgentHandler>();
            });
            AzclipsServiceProvider.Initialize(ClientType.PowerShell);
        }
```

- [ ] **Step 6: Run both test projects to verify all three tests pass**

Run: `dotnet test tests/Cmdlets.Tests/Cmdlets.Tests.csproj --filter "FullyQualifiedName~ModuleInitializerSenderRegistrationTests"`
Expected: PASS (2 tests).

Run: `dotnet test tests/Core.Telemetry.Tests/Core.Telemetry.Tests.csproj`
Expected: PASS (whole project — confirms nothing else depended on the old phase).

- [ ] **Step 7: Commit**

```bash
git add src/PS/Login/ModuleInitializer.cs tests/Core.Telemetry.Tests/TelemetrySenderRegistrationTests.cs tests/Cmdlets.Tests/ModuleInitializerSenderRegistrationTests.cs
git commit -m "{Telemetry} Let a host replace the PowerShell telemetry sender"
```

---

### Task 2: Extract the shared telemetry-artifacts helper

`CLIBenchmarkBase` owns the staging paths, the benchmark-name lookup, and the post-run pruning. `PSBenchmarkBase` needs all of it. Extract first so Task 4 has something to call, and so CLI and PS cannot drift.

This task changes no behaviour. It is a pure move.

**Files:**
- Create: `tests/Benchmark/Telemetry/BenchmarkTelemetryArtifacts.cs`
- Modify: `tests/Benchmark/CLI/CLIBenchmarkBase.cs` — remove the `_stagingRoot`, `_telemetryRoot`, `_benchmarkName` fields, the `PruneTelemetry` method, and the private `TryDeleteDirectory`; call the new type instead.

**Interfaces:**
- Consumes: `BenchmarkName.FromCommandLine()` and `TraceSampler.Prune(string actualDirectory, string outputPath)`, both existing in `Benchmark.Telemetry`.
- Produces:
  - `internal sealed class BenchmarkTelemetryArtifacts`
  - `internal BenchmarkTelemetryArtifacts(string repoRoot)` — resolves paths and deletes any stale root
  - `internal string WarmupDirectory { get; }`
  - `internal string ActualDirectory { get; }`
  - `internal void Prune()` — samples `actual/` into `<stagingRoot>/<benchmark>.jsonl`, deletes the raw root, never throws
  - `internal static void TryDeleteDirectory(string path)`

- [ ] **Step 1: Create the helper**

Create `tests/Benchmark/Telemetry/BenchmarkTelemetryArtifacts.cs`:

```csharp
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Benchmark.Telemetry;

/// <summary>
/// Owns one benchmark method's telemetry staging directories and the post-run pruning that turns
/// them into the run's deliverable. Shared by the CLI and PowerShell benchmark bases.
/// </summary>
/// <remarks>
/// Benchmark-only.
///
/// <para><b>Never runs inside the measured region.</b> Constructed in [GlobalSetup] and used from
/// [GlobalCleanup], neither of which BenchmarkDotNet measures.</para>
/// </remarks>
internal sealed class BenchmarkTelemetryArtifacts
{
    private readonly string _stagingRoot;
    private readonly string _telemetryRoot;
    private readonly string _benchmarkName;

    /// <summary>
    /// Resolves this process's benchmark method and its directories, then clears the root. The
    /// staging path is stable across runs, so that clear is what keeps runs separate.
    /// </summary>
    internal BenchmarkTelemetryArtifacts(string repoRoot)
    {
        // One process per benchmark case, so this is constant for the whole run.
        _benchmarkName = BenchmarkName.FromCommandLine();
        _stagingRoot = Path.Combine(repoRoot, "artifacts", "Benchmark", "telemetry");
        _telemetryRoot = Path.Combine(_stagingRoot, _benchmarkName);

        TryDeleteDirectory(_telemetryRoot);
    }

    /// <summary>Where warmup-iteration traces are written. Deleted unread by <see cref="Prune"/>.</summary>
    internal string WarmupDirectory => Path.Combine(_telemetryRoot, "warmup");

    /// <summary>Where measured-iteration traces are written. The only directory consumed.</summary>
    internal string ActualDirectory => Path.Combine(_telemetryRoot, "actual");

    /// <summary>
    /// Samples the measured traces into the run's output file and removes the raw ones. Never
    /// throws: a telemetry failure must not change the benchmark's exit code.
    /// </summary>
    internal void Prune()
    {
        try
        {
            var kept = TraceSampler.Prune(
                ActualDirectory,
                Path.Combine(_stagingRoot, _benchmarkName + ".jsonl"));

            if (kept == 0)
            {
                Console.WriteLine(
                    $"[Telemetry] {_benchmarkName}: no traces captured. Either telemetry is " +
                    "disabled in ~/.azclips/config.json, or no measured iteration ran.");
            }
            else
            {
                Console.WriteLine($"[Telemetry] {_benchmarkName}: kept {kept} sampled trace(s)");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Telemetry] {_benchmarkName}: prune failed ({ex.Message})");
        }
        finally
        {
            TryDeleteDirectory(_telemetryRoot);
        }
    }

    internal static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch (IOException) { /* best-effort */ }
        catch (UnauthorizedAccessException) { /* best-effort */ }
    }
}
```

- [ ] **Step 2: Point `CLIBenchmarkBase` at the helper**

In `tests/Benchmark/CLI/CLIBenchmarkBase.cs`, replace the three staging fields with one. Change:

```csharp
    private string _extensionsRoot = null!;
    private BenchmarkStageListener _stage = null!;
    private StageTraceDirectoryProvider _traceDirectory = null!;
    private string _stagingRoot = null!;    // artifacts/Benchmark/telemetry
    private string _telemetryRoot = null!;  // <_stagingRoot>/<benchmark>
    private string _benchmarkName = null!;
```

to:

```csharp
    private string _extensionsRoot = null!;
    private BenchmarkStageListener _stage = null!;
    private StageTraceDirectoryProvider _traceDirectory = null!;
    private BenchmarkTelemetryArtifacts _telemetry = null!;
```

- [ ] **Step 3: Update `GlobalSetup`**

Replace the trailing telemetry block of `GlobalSetup` (everything from `// One process per benchmark case` to the end of the method) with:

```csharp
        _telemetry = new BenchmarkTelemetryArtifacts(RepoRoot);

        _stage = new BenchmarkStageListener();
        _traceDirectory = new StageTraceDirectoryProvider(
            _telemetry.WarmupDirectory,
            _telemetry.ActualDirectory,
            _stage);
```

- [ ] **Step 4: Update `GlobalCleanup` and delete the moved members**

Replace `GlobalCleanup` with:

```csharp
    [GlobalCleanup]
    public void GlobalCleanup()
    {
        _stage?.Dispose();
        _telemetry?.Prune();

        // Best-effort: InstalledExtensionLoader caches the extension ALC for the process
        // lifetime, so the staged DLL may still be locked here. A leftover home is harmless.
        BenchmarkTelemetryArtifacts.TryDeleteDirectory(
            Path.Combine(RepoRoot, "artifacts", "Benchmark", "home"));
    }
```

Then delete the entire `PruneTelemetry()` method and the private `static void TryDeleteDirectory(string path)` method from this file. One `TryDeleteDirectory` call site now remains in `GlobalSetup` — the `_extensionsRoot` reset near the top. Change it to `BenchmarkTelemetryArtifacts.TryDeleteDirectory(_extensionsRoot);`.

- [ ] **Step 5: Build to verify the extraction compiles**

Run: `dotnet build tests/Benchmark/Benchmark.csproj`
Expected: Build succeeded, 0 errors. Any `CS0103` naming error means a `TryDeleteDirectory` call site was missed.

- [ ] **Step 6: Commit**

```bash
git add tests/Benchmark/Telemetry/BenchmarkTelemetryArtifacts.cs tests/Benchmark/CLI/CLIBenchmarkBase.cs
git commit -m "{Telemetry} Share the benchmark telemetry artifact paths between bases"
```

---

### Task 3: Add the benchmark telemetry pipeline types

Four small types that together replace `FileTelemetrySender` for benchmark use: an exporter that picks its directory late, a processor that decides which directory that is, a processor that drops module-import traces, and the sender that wires them.

They cannot be unit-tested — `tests/Benchmark` is an `Exe` with no companion test project, and the design accepts that. The gate for this task is compilation; behaviour is proven end-to-end in Task 5.

**Files:**
- Create: `tests/Benchmark/Telemetry/StageDirectedFileExporter.cs`
- Create: `tests/Benchmark/Telemetry/StageLatch.cs`
- Create: `tests/Benchmark/Telemetry/ImportTraceFilter.cs`
- Create: `tests/Benchmark/Telemetry/BenchmarkFileTelemetrySender.cs`

**Interfaces:**
- Consumes: `Core.Telemetry.FileExporter(string rootDirectory)` (internal, visible via `InternalsVisibleTo`), `Core.Telemetry.ITelemetrySender`, `Core.Abstractions.TelemetryTags.ImportKind`, and `Benchmark.Telemetry.BenchmarkStageListener.IsMeasured`.
- Produces:
  - `internal sealed class StageDirectedFileExporter : BaseExporter<Activity>` with `internal StageDirectedFileExporter(string warmupDirectory, string actualDirectory)` and `internal void MarkMeasured()`
  - `internal sealed class StageLatch : BaseProcessor<Activity>` with `internal StageLatch(BenchmarkStageListener stage, StageDirectedFileExporter destination)`
  - `internal sealed class ImportTraceFilter : BaseProcessor<Activity>` with an implicit parameterless constructor; Task 4 registers it through a factory lambda, not by type
  - `internal sealed class BenchmarkFileTelemetrySender : ITelemetrySender` with `internal BenchmarkFileTelemetrySender(BenchmarkStageListener stage, string warmupDirectory, string actualDirectory)` — this is the type Task 4 registers

- [ ] **Step 1: Create the stage-directed exporter**

Create `tests/Benchmark/Telemetry/StageDirectedFileExporter.cs`:

```csharp
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Diagnostics;
using Core.Telemetry;
using OpenTelemetry;

namespace Benchmark.Telemetry;

/// <summary>
/// Writes one benchmark iteration's spans into either the warmup or the measured directory, chosen
/// by the flag <see cref="StageLatch"/> sets while that iteration runs.
/// </summary>
/// <remarks>
/// Benchmark-only.
///
/// <para><b>Does not run inside the measured region.</b> Export is called on the batch processor's
/// drain, which happens when the TracerProvider is disposed in [IterationCleanup].</para>
///
/// <para>The inner FileExporter is created on first export, because the destination is not known
/// until the iteration has run. Export is only ever called by the batch processor's single worker
/// thread, so the lazy assignment needs no synchronisation.</para>
/// </remarks>
internal sealed class StageDirectedFileExporter : BaseExporter<Activity>
{
    private readonly string _warmupDirectory;
    private readonly string _actualDirectory;

    // Written on the cmdlet thread by StageLatch, read on the batch worker thread.
    private volatile bool _measured;

    private FileExporter? _inner;

    internal StageDirectedFileExporter(string warmupDirectory, string actualDirectory)
    {
        _warmupDirectory = warmupDirectory;
        _actualDirectory = actualDirectory;
    }

    /// <summary>
    /// Records that this iteration is a measured one. Set-only: one span observed inside the
    /// measured region proves the whole iteration is measured, and a span ending outside it must
    /// not undo that.
    /// </summary>
    internal void MarkMeasured() => _measured = true;

    public override ExportResult Export(in Batch<Activity> batch) =>
        (_inner ??= new FileExporter(_measured ? _actualDirectory : _warmupDirectory)).Export(batch);
}
```

- [ ] **Step 2: Create the stage latch**

Create `tests/Benchmark/Telemetry/StageLatch.cs`:

```csharp
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Diagnostics;
using OpenTelemetry;

namespace Benchmark.Telemetry;

/// <summary>
/// Records whether the iteration that produced a span is a measured one, so
/// <see cref="StageDirectedFileExporter"/> can pick a directory after BenchmarkDotNet has closed
/// the measured region.
/// </summary>
/// <remarks>
/// Benchmark-only.
///
/// <para><b>Runs inside the measured region.</b> OpenTelemetry calls OnEnd on the thread that ends
/// the activity — the cmdlet thread — so this is limited to a flag check, a volatile read, and a
/// store: no allocation, no I/O, no locking.</para>
///
/// <para>Engine.RunIteration raises WorkloadActualStart immediately before the measured call and
/// WorkloadActualStop immediately after it, so BenchmarkStageListener.IsMeasured reads false in
/// [IterationSetup], where the TracerProvider is built, and false again in [IterationCleanup],
/// where the file is written. It is true only while the benchmark method runs.</para>
/// </remarks>
internal sealed class StageLatch : BaseProcessor<Activity>
{
    private readonly BenchmarkStageListener _stage;
    private readonly StageDirectedFileExporter _destination;

    internal StageLatch(BenchmarkStageListener stage, StageDirectedFileExporter destination)
    {
        _stage = stage;
        _destination = destination;
    }

    public override void OnEnd(Activity data)
    {
        // Unrecorded spans are dropped downstream, so they get no vote on the iteration's stage.
        // ImportTraceFilter runs earlier in the chain and clears Recorded on the module import
        // trace, which is emitted from a background thread whose timing is not controlled.
        if (data is null || !data.Recorded) return;

        if (_stage.IsMeasured) _destination.MarkMeasured();
    }
}
```

- [ ] **Step 3: Create the import-trace filter**

Create `tests/Benchmark/Telemetry/ImportTraceFilter.cs`:

```csharp
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Diagnostics;
using Core.Abstractions;
using OpenTelemetry;

namespace Benchmark.Telemetry;

/// <summary>
/// Drops the PowerShell module-import trace so a benchmark's sampled output contains only cmdlet
/// invocations.
/// </summary>
/// <remarks>
/// Benchmark-only.
///
/// <para><b>Runs inside the measured region.</b> OnEnd executes on the thread that ends the
/// activity, so this is limited to a tag lookup and a prefix comparison.</para>
///
/// <para>Every iteration imports the module, so ImportTelemetry emits one startup trace per
/// iteration. Its root is named "PowerShell.Activity.Root", the same name a cmdlet root uses, so
/// the discriminator is the importKind tag ImportTelemetry.Emit sets on it; the child phases match
/// on name prefix. The tag survives DefaultTelemetryPropertyFilterProcessor because
/// TelemetryTags.ImportKind is on that filter's allowlist.</para>
///
/// <para>Clearing the Recorded flag is the mechanism DefaultActivityNameFilterProcessor uses: both
/// SimpleActivityExportProcessor and BatchActivityExportProcessor skip unrecorded activities.</para>
/// </remarks>
internal sealed class ImportTraceFilter : BaseProcessor<Activity>
{
    private const string ImportPhasePrefix = "PowerShell.Startup";

    public override void OnEnd(Activity data)
    {
        if (data is null) return;

        var isImportTrace =
            data.GetTagItem(TelemetryTags.ImportKind) is not null ||
            data.OperationName.StartsWith(ImportPhasePrefix, StringComparison.Ordinal);

        if (!isImportTrace) return;

        data.ActivityTraceFlags &= ~ActivityTraceFlags.Recorded;
    }
}
```

- [ ] **Step 4: Create the benchmark sender**

Create `tests/Benchmark/Telemetry/BenchmarkFileTelemetrySender.cs`:

```csharp
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Telemetry;
using OpenTelemetry;
using OpenTelemetry.Trace;

namespace Benchmark.Telemetry;

/// <summary>
/// The <see cref="ITelemetrySender"/> PowerShell benchmarks run with. Writes trace files through
/// the production FileExporter, off the measured thread.
/// </summary>
/// <remarks>
/// Benchmark-only. PSBenchmarkBase supplies it via AzclipsServiceProvider.OverrideRegistration,
/// which beats the DirectTelemetrySender the PowerShell module registers in the prepend phase.
///
/// <para><b>ConfigureExporter does not run inside the measured region.</b> PSBenchmarkBase resolves
/// ITelemetryService in [IterationSetup] to force provider construction — and therefore this
/// method — before the stopwatch starts.</para>
///
/// <para>The batch processor means the timed thread only enqueues; the stage-directed exporter
/// separates warmup traces from measured ones. Everything created here is owned by the
/// TracerProvider and dies with it in [IterationCleanup], where the batch shutdown drains the queue
/// to disk.</para>
/// </remarks>
internal sealed class BenchmarkFileTelemetrySender : ITelemetrySender
{
    private readonly BenchmarkStageListener _stage;
    private readonly string _warmupDirectory;
    private readonly string _actualDirectory;

    internal BenchmarkFileTelemetrySender(
        BenchmarkStageListener stage,
        string warmupDirectory,
        string actualDirectory)
    {
        _stage = stage;
        _warmupDirectory = warmupDirectory;
        _actualDirectory = actualDirectory;
    }

    public void ConfigureExporter(TracerProviderBuilder builder)
    {
        // One exporter per iteration, so each iteration writes its own traces-<traceId>.log.
        var exporter = new StageDirectedFileExporter(_warmupDirectory, _actualDirectory);

        // Order matters: the latch reads the stage while the measured region is open, and the
        // batch processor enqueues afterwards.
        builder.AddProcessor(new StageLatch(_stage, exporter));
        builder.AddProcessor(new BatchActivityExportProcessor(exporter));
    }
}
```

- [ ] **Step 5: Build to verify all four types compile**

Run: `dotnet build tests/Benchmark/Benchmark.csproj`
Expected: Build succeeded, 0 errors.

If `BaseExporter`, `BaseProcessor`, or `BatchActivityExportProcessor` cannot be resolved, the transitive `OpenTelemetry` package reference is not flowing. Fix by adding to the `PackageReference` group of `tests/Benchmark/Benchmark.csproj`:

```xml
    <PackageReference Include="OpenTelemetry" Version="1.15.3" />
```

Use exactly `1.15.3` — it must match `src/Core/Core.Telemetry/Core.Telemetry.csproj`.

- [ ] **Step 6: Commit**

```bash
git add tests/Benchmark/Telemetry/StageDirectedFileExporter.cs tests/Benchmark/Telemetry/StageLatch.cs tests/Benchmark/Telemetry/ImportTraceFilter.cs tests/Benchmark/Telemetry/BenchmarkFileTelemetrySender.cs tests/Benchmark/Benchmark.csproj
git commit -m "{Telemetry} Add the PowerShell benchmark telemetry pipeline"
```

---

### Task 4: Wire telemetry capture into `PSBenchmarkBase`

Register the benchmark telemetry stack per iteration, force the provider to build outside the measured region, and prune at the end of the run.

**Files:**
- Modify: `tests/Benchmark/PS/PSBenchmarkBase.cs` — fields, `GlobalSetup`, `IterationSetup`; add `RegisterTelemetryCapture` and `GlobalCleanup`.

**Interfaces:**
- Consumes: `BenchmarkTelemetryArtifacts` (Task 2), `BenchmarkFileTelemetrySender` and `ImportTraceFilter` (Task 3), the registration-phase guarantee from Task 1, and existing `BenchmarkStageListener`.
- Produces: nothing other tasks consume. This is the wiring that makes the feature run.

- [ ] **Step 1: Add the imports and fields**

In `tests/Benchmark/PS/PSBenchmarkBase.cs`, replace the using block at the top:

```csharp
using System.Diagnostics;
using System.Management.Automation;
using System.Management.Automation.Runspaces;
using Benchmark.Telemetry;
using BenchmarkDotNet.Attributes;
using Core.Abstractions;
using Core.DependencyInjection;
using Core.Telemetry;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry;
using Perfolizer.Mathematics.OutlierDetection;
```

Then replace the two existing fields:

```csharp
    private string _psModuleManifestPath = null!;
    private Runspace _runspace = null!;
```

with:

```csharp
    private string _psModuleManifestPath = null!;
    private Runspace _runspace = null!;
    private BenchmarkStageListener _stage = null!;
    private BenchmarkTelemetryArtifacts _telemetry = null!;
    private BenchmarkFileTelemetrySender _sender = null!;
```

- [ ] **Step 2: Extend `GlobalSetup`**

Replace `GlobalSetup` with:

```csharp
    [GlobalSetup]
    public void GlobalSetup()
    {
        InitializeCommon();

        _psModuleManifestPath = PSManifestStager.StageModule(RepoRoot, ModuleName);

        Console.WriteLine($"[Setup] PS benchmarks ready for '{ModuleName}' (stub HTTP)");

        // One process per benchmark case, so these are constant for the whole run. The listener
        // tracks the engine's stage and the sender holds two paths; only what ConfigureExporter
        // builds is per-iteration.
        _telemetry = new BenchmarkTelemetryArtifacts(RepoRoot);
        _stage = new BenchmarkStageListener();
        _sender = new BenchmarkFileTelemetrySender(
            _stage,
            _telemetry.WarmupDirectory,
            _telemetry.ActualDirectory);
    }
```

- [ ] **Step 3: Add `RegisterTelemetryCapture`**

Add this method to `PSBenchmarkBase`, directly after `GlobalSetup`:

```csharp
    /// <summary>
    /// Registers the benchmark telemetry stack: the real TelemetryService, a sender that writes
    /// trace files, and the filter that drops module-import traces.
    /// </summary>
    /// <remarks>
    /// Called from [IterationSetup], which BenchmarkDotNet does not measure. Of what is registered
    /// here, only StageLatch.OnEnd, ImportTraceFilter.OnEnd, and the batch enqueue run inside the
    /// measured region; provider construction and the file write do not.
    ///
    /// <para>Repeated every iteration because [IterationCleanup] disposes the container. DI
    /// last-wins semantics put TelemetryService ahead of the stub registration. ITelemetrySender
    /// wins over the module's DirectTelemetrySender, which is registered in the prepend phase.</para>
    ///
    /// <para>ITelemetryProvider is deliberately absent: ModuleInitializer registers
    /// PSTelemetryProvider during Import-Module, which is enqueued after this and must win.</para>
    /// </remarks>
    private void RegisterTelemetryCapture()
    {
        AzclipsServiceProvider.OverrideRegistration(services =>
        {
            services.AddSingleton<ITelemetryService, TelemetryService>();
            services.AddSingleton<ITelemetrySender>(_sender);

            // Factory rather than by-type: ImportTraceFilter is internal and DI's constructor
            // selection wants a public one. A fresh instance per container also keeps disposal
            // correct, since the container is rebuilt every iteration.
            services.AddSingleton<BaseProcessor<Activity>>(_ => new ImportTraceFilter());
        });
    }
```

- [ ] **Step 4: Extend `IterationSetup` with the registration and the pre-warm**

Replace `IterationSetup` with:

```csharp
    [IterationSetup]
    public void IterationSetup()
    {
        RegisterBenchmarkStubs();
        RegisterTelemetryCapture();

        // Prime the runspace and import the module. Module import execution is excluded from the measured region, so that the benchmark measures only the cmdlet invocation.
        _runspace = RunspaceFactory.CreateRunspace();
        try
        {
            _runspace.Open();

            using var import = PowerShell.Create();
            import.Runspace = _runspace;
            import.AddCommand("Import-Module").AddParameter("Name", _psModuleManifestPath).AddParameter("Force", true);
            import.Invoke();
            ThrowOnPowerShellErrors(import, "Import-Module");

            // Build the telemetry stack — TracerProvider, processors, exporter — here, outside the
            // measured region. Import-Module has just initialised the container, so this is the
            // earliest point it can be resolved.
            AzclipsServiceProvider.GetRequiredService<ITelemetryService>();
        }
        catch
        {
            _runspace.Dispose();
            _runspace = null!;
            throw;
        }
    }
```

- [ ] **Step 5: Add `GlobalCleanup`**

Add this method after `IterationCleanup`:

```csharp
    /// <summary>
    /// Samples this benchmark's measured traces into the run's deliverable and removes the raw
    /// directories. No draining is needed: each iteration's TracerProvider was disposed in
    /// [IterationCleanup], which flushed that iteration's spans to disk.
    /// </summary>
    [GlobalCleanup]
    public void GlobalCleanup()
    {
        _stage?.Dispose();
        _telemetry?.Prune();
    }
```

- [ ] **Step 6: Build**

Run: `dotnet build tests/Benchmark/Benchmark.csproj`
Expected: Build succeeded, 0 errors.

- [ ] **Step 7: Build the whole solution to confirm nothing else broke**

Run: `dotnet build azclips.slnx`
Expected: Build succeeded, 0 errors.

If restore fails while downloading the GitHub Copilot npm package from `registry.npmjs.org`, use the `building-the-solution` skill, which documents the proxy workaround.

- [ ] **Step 8: Commit**

```bash
git add tests/Benchmark/PS/PSBenchmarkBase.cs
git commit -m "{Telemetry} Emit real telemetry from PowerShell benchmark iterations"
```

---

### Task 5: Validate end-to-end

The benchmark types have no unit tests by design, so this run is the real gate. It must prove four things that only a live run can: that traces are produced at all, that they land in `actual/` rather than `warmup/`, that import traces are gone, and that CLI still works after the Task 2 extraction.

**Files:**
- Temporarily modify then restore: `tests/Benchmark/PS/PSBenchmarkBase.cs`, `tests/Benchmark/CLI/CLIBenchmarkBase.cs` (iteration counts only — handled by the skill)

**Interfaces:**
- Consumes: everything from Tasks 1–4.
- Produces: `artifacts/Benchmark/<timestamp>/telemetry/*.jsonl` — one file per benchmark method.

- [ ] **Step 1: Confirm the working tree is clean before starting**

Run: `git status --short`
Expected: no modifications. This matters because the `validate-benchmarks` skill restores iteration counts with `git checkout --` on the two base classes; uncommitted work in them would be destroyed. Tasks 1–4 must all be committed first.

- [ ] **Step 2: Build Release artifacts**

Run:

```bash
dotnet build azclips.slnx -c Release
./build/Set-ModuleConfiguration.ps1 -c Release
```

Expected: both succeed. PS benchmarks load the Release build output via the staged manifests, so a Debug-only build makes `GlobalSetup` throw.

- [ ] **Step 3: Run the benchmark smoke test**

Use the `validate-benchmarks` skill, which lowers `[WarmupCount]` and `[IterationCount]` from 5000 to 5 in both base classes, runs the suite, and always restores them.

Expected: exit code 0 and a `[Benchmark] PASS` line. Do not interpret the timing numbers — this is an execution check.

The run must also print one `[Telemetry] PS.<Class>.<Method>: kept 5 sampled trace(s)` line per PS benchmark method. A `kept 0` line means telemetry never reached `actual/`.

- [ ] **Step 4: Assert the telemetry output**

Run this against the run folder the previous step produced:

```powershell
$run = Get-ChildItem artifacts\Benchmark -Directory |
    Where-Object { $_.Name -match '^\d{4}-\d{2}-\d{2}_' } |
    Sort-Object Name -Descending | Select-Object -First 1
$telemetry = Join-Path $run.FullName 'telemetry'

# TraceEntry.Tags serialises as an ARRAY of { Key, Value } pairs (Activity.TagObjects is an
# IEnumerable<KeyValuePair<..>>, not a dictionary), so tags are matched on .Key — see
# TraceReplayer.Replay, which reads the same shape.
Get-ChildItem $telemetry -Filter '*.jsonl' | ForEach-Object {
    $spans = Get-Content $_.FullName | ForEach-Object { $_ | ConvertFrom-Json }
    [pscustomobject]@{
        File        = $_.Name
        Traces      = ($spans | Group-Object TraceId).Count
        ImportSpans = @($spans | Where-Object { $_.OperationName -like 'PowerShell.Startup*' }).Count
        ImportRoots = @($spans | Where-Object { $_.Tags.Key -contains 'importKind' }).Count
        Roots       = @($spans | Where-Object { $_.OperationName -like '*.Activity.Root' }).Count
    }
} | Format-Table -AutoSize
```

Expected, for every row:

| Column | Required value | What a wrong value means |
|---|---|---|
| `Traces` | `5` | `0` — the stage was read outside the measured region and everything went to `warmup/`. `10` — the stage listener never fired, so warmup traces were kept too. |
| `ImportSpans` | `0` | `ImportTraceFilter` is not matching the `PowerShell.Startup` prefix. |
| `ImportRoots` | `0` | `ImportTraceFilter` is not matching the `importKind` tag — check it is still on `DefaultTelemetryPropertyFilter`'s allowlist. |
| `Roots` | `5` | Fewer means traces were truncated by the 500 ms dispose cap. |

There must be at least one `PS.*.jsonl` file and at least one `CLI.*.jsonl` file. The CLI rows must also show `Traces = 5`, which is what proves the Task 2 extraction did not regress CLI.

- [ ] **Step 5: Assert nothing was left behind**

Run: `Test-Path artifacts\Benchmark\telemetry`
Expected: `False`. A `True` means `Prune()` failed to delete the raw directories, or `Program.CollectTelemetry` did not move the files.

- [ ] **Step 6: Confirm the iteration counts were restored**

Run: `git diff -- tests/Benchmark/PS/PSBenchmarkBase.cs tests/Benchmark/CLI/CLIBenchmarkBase.cs`
Expected: empty diff. Also confirm both files contain `[WarmupCount(5000)]` and `[IterationCount(5000)]` and neither contains `[WarmupCount(5)]`.

- [ ] **Step 7: Record the performance delta**

Compare the PS benchmark means in this run against a run from before Task 4 (or re-run on the same machine with the PS telemetry registration commented out). Note both figures in the PR description.

Expect a small uniform step-up: every iteration now creates and tags real spans where `StubTelemetryService` did nothing. A *large* move means the pre-warm in `IterationSetup` is not taking effect and the provider is being built inside the measured region — investigate before merging.

- [ ] **Step 8: Commit any fixes**

Only if Steps 4–6 required changes:

```bash
git add -A
git commit -m "{Telemetry} Fix PowerShell benchmark telemetry defects found in validation"
```

---

## Notes for the implementer

**Registration order is the whole trick.** `AzclipsServiceProvider.Initialize` drains three queues in order: prepends, then framework defaults, then overrides. Within a phase, later wins. For one PS benchmark iteration the sequence is:

1. `RegisterBenchmarkStubs()` — override #1 (`StubTelemetryService`)
2. `RegisterTelemetryCapture()` — override #2 (`TelemetryService`, the benchmark sender, `ImportTraceFilter`)
3. `Import-Module` → `ModuleInitializer.OnImport()` — prepend (`DirectTelemetrySender`) plus override #3 (`PSTelemetryProvider`, others), then `Initialize()`

So `ITelemetryService` resolves to `TelemetryService` (override #2 beats #1), `ITelemetrySender` resolves to the benchmark sender (any override beats a prepend), and `ITelemetryProvider` resolves to `PSTelemetryProvider` (override #3 beats the default). All three are what this design needs.

**Processor chain order.** `PSTelemetryProvider` adds every DI-registered `BaseProcessor<Activity>` in registration order, then calls `sender.ConfigureExporter`. The result:

```
span ends → DefaultActivityNameFilterProcessor → DefaultTelemetryPropertyFilterProcessor
          → ImportTraceFilter → StageLatch → BatchActivityExportProcessor (enqueue) → returns
```

`ImportTraceFilter` ahead of `StageLatch` is what makes `StageLatch`'s `data.Recorded` guard meaningful.

**If `Traces` comes back as 0.** The likely cause is that the pre-warm resolved `ITelemetryService` but something later rebuilt the provider, or `ConfigureExporter` never ran. Check that `PSTelemetryProvider` is resolved exactly once per iteration by adding a temporary counter to `BenchmarkFileTelemetrySender.ConfigureExporter`; it should print once per iteration, not once per benchmark and not per span.
