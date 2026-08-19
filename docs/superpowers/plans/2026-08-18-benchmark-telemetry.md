# Benchmark Telemetry Emission Implementation Plan

> **Status: executed, then rebased.** This plan was written against `main` and carried out
> task-by-task. The branch was later rebased onto
> `jaysontaimicrosoft-refactor-file-telemetry-sender`, which decomposed `FileTelemetrySender` and
> injected `ITelemetrySender` into the client providers. The task steps below are kept as the record
> of how the work was done; they name APIs that refactor has since moved. Where the two disagree,
> the design spec and the code are current, not this plan.
>
> What the rebase changed:
> - `FileTelemetrySender.GetTraceDirectory()` → `HomePaths.GetDefaultTraceDirectory()`
> - `FileTelemetrySender.ParseTraceFile` → `TraceFileFormat.Parse`
> - `FileTelemetrySender.TraceEntry` → top-level `TraceEntry`
> - `ITelemetrySender.ConfigureExporter(builder, dir)` → `ConfigureExporter(builder)`
> - Task 1's `CLITelemetryProvider` change was **dropped**: the refactor already injects
>   `ITelemetrySender` there, so the directory seam moved down into `FileTelemetrySender` instead.

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make CLI benchmarks emit real span-level telemetry, leaving one sampled JSONL file per benchmark method in the run's artifacts directory.

**Architecture:** A new `ITraceDirectoryProvider` seam in `Core.Telemetry` makes the trace-buffer directory injectable. The benchmark overrides only that value, so the production `TelemetryService` → `CLITelemetryProvider` → `FileTelemetrySender` → `FileExporter` chain runs untouched. A BenchmarkDotNet `EventListener` splits warmup from measured traces into sibling directories; `GlobalCleanup` samples the measured ones down to 100 and deletes the raw files; the host collects the results into the timestamped artifacts folder.

**Tech Stack:** C# / .NET 10, BenchmarkDotNet 0.13.12, OpenTelemetry 1.15.3, xUnit, `Microsoft.Extensions.DependencyInjection`.

**Design spec:** `docs/superpowers/specs/2026-08-18-benchmark-telemetry-design.md`

## Global Constraints

- Target framework is `net10.0`; nullable reference types and implicit usings are enabled repo-wide.
- Every new file starts with the two-line header used across the repo:
  `// Copyright (c) Microsoft Corporation.` then `// Licensed under the MIT License.`
- Test method naming convention is `MethodName_ShouldBehavior_WhenCondition`.
- Build with `dotnet build azclips.slnx`. Benchmarks additionally require a **Release** build.
- **Full-solution builds and solution-wide test runs must pass**
  `-p:CopilotNpmRegistryUrl=https://packagefeedproxy.microsoft.io/npm/`. Without it, `Core.Agent`
  tries to download a GitHub Copilot npm package from `registry.npmjs.org` and fails with a TLS
  handshake error. Targeted builds of projects that do not pull in `Core.Agent` (for example
  `tests/Benchmark/Benchmark.csproj` or `src/Core/Core.Telemetry/Core.Telemetry.csproj`) do not
  need it.
- `Release` builds delay-sign with `MSSharedLibPublicKey` (`Shared.props:22-27`), so every
  `InternalsVisibleTo` needs the Release variant with `Key="$(MSSharedLibPublicKey)"` **and** the
  non-Release variant without it.
- Do **not** modify `ITelemetrySender`, `FileTelemetrySender`, `DirectTelemetrySender`,
  `TUITelemetryProvider`, `PSTelemetryProvider`, or `BenchmarkBase.RegisterBenchmarkStubs()`.
  PowerShell and startup benchmarks must keep their current behaviour.
- Telemetry plumbing failures must never change the benchmark exit code — they print a
  `[Telemetry]` warning and the run stays green.
- Sampling target is **100** traces per benchmark method.
- BenchmarkDotNet constants used: `EngineEventSource.SourceName` is
  `"BenchmarkDotNet.EngineEventSource"`, `WorkloadActualStartEventId` is `15`,
  `WorkloadActualStopEventId` is `16`. Reference them by name, never by literal.

---

### Task 1: Make the trace directory injectable in production telemetry

Today `CLITelemetryProvider` hard-codes its destination by calling the static
`FileTelemetrySender.GetTraceDirectory()`. This task turns that single value into a DI-resolved
service so a host can redirect buffering without touching the sender or exporter.

**Files:**
- Create: `src/Core/Core.Telemetry/ITraceDirectoryProvider.cs`
- Modify: `src/Core/Core.Telemetry/DIRegisterExtensions.cs`
- Modify: `src/CLI/Telemetry/CLITelemetryProvider.cs:23-47`
- Test: `tests/Core.Telemetry.Tests/TraceDirectoryProviderTests.cs`

**Interfaces:**
- Consumes: nothing from earlier tasks.
- Produces: `Core.Telemetry.ITraceDirectoryProvider` with a single method
  `string GetTraceDirectory()`, and `Core.Telemetry.DefaultTraceDirectoryProvider` implementing it.
  Task 2 implements this interface; Task 5 registers an implementation of it.

- [ ] **Step 1: Write the failing test**

Create `tests/Core.Telemetry.Tests/TraceDirectoryProviderTests.cs`:

```csharp
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Extensions.DependencyInjection;

namespace Core.Telemetry.Tests;

public class TraceDirectoryProviderTests
{
    [Fact]
    public void GetTraceDirectory_ShouldReturnLocalAppDataTracesDirectory_WhenUsingDefaultProvider()
    {
        // Asserts the concrete path contract rather than the delegation, so it catches a real
        // change to where production telemetry lands instead of merely tracking a refactor.
        var expected = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            ".azclips",
            "traces");

        Assert.Equal(expected, new DefaultTraceDirectoryProvider().GetTraceDirectory());
    }

    [Fact]
    public void RegisterTelemetryServices_ShouldRegisterDefaultProvider_WhenContainerIsBuilt()
    {
        var services = new ServiceCollection();
        services.RegisterTelemetryServices();

        using var container = services.BuildServiceProvider();
        var resolved = container.GetRequiredService<ITraceDirectoryProvider>();

        Assert.IsType<DefaultTraceDirectoryProvider>(resolved);
    }
}
```

The second test is the load-bearing one: the benchmark's override only wins because the default is
registered among the telemetry defaults rather than in `Program.Main`.

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test tests/Core.Telemetry.Tests/Core.Telemetry.Tests.csproj --filter TraceDirectoryProviderTests`
Expected: FAIL to compile — `ITraceDirectoryProvider` and `DefaultTraceDirectoryProvider` do not exist.

- [ ] **Step 3: Create the interface and default implementation**

Create `src/Core/Core.Telemetry/ITraceDirectoryProvider.cs`:

```csharp
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Core.Telemetry;

/// <summary>
/// Resolves the directory that trace files are buffered into. Injected so a host can redirect
/// buffering without replacing the sender, the exporter, or any part of the export pipeline.
/// </summary>
/// <remarks>
/// This governs where traces are <b>written</b> only. The uploader side
/// (<see cref="FileTelemetrySender.SendPendingStatic"/>) reads
/// <see cref="FileTelemetrySender.GetTraceDirectory"/> directly, because it runs before the DI
/// container exists. Any production redirect through this seam must redirect the uploader too.
/// </remarks>
public interface ITraceDirectoryProvider
{
    string GetTraceDirectory();
}

/// <summary>The shipping destination: the shared per-user trace directory.</summary>
public sealed class DefaultTraceDirectoryProvider : ITraceDirectoryProvider
{
    public string GetTraceDirectory() => FileTelemetrySender.GetTraceDirectory();
}
```

- [ ] **Step 4: Register the default among the telemetry defaults**

In `src/Core/Core.Telemetry/DIRegisterExtensions.cs`, add one line inside
`RegisterTelemetryServices`, immediately after the `ITelemetryService` registration:

```csharp
        services.AddSingleton<ITelemetryService, TelemetryService>();
        services.AddSingleton<ITraceDirectoryProvider, DefaultTraceDirectoryProvider>();
```

- [ ] **Step 5: Consume the provider in `CLITelemetryProvider`**

In `src/CLI/Telemetry/CLITelemetryProvider.cs`, change the constructor signature and the
`ConfigureExporter` argument. Everything else in the file stays exactly as it is — the
`FileTelemetrySender sender = new();` line does **not** change.

```csharp
    public CLITelemetryProvider(
        IEnumerable<BaseProcessor<Activity>> filterProcessors,
        ITraceDirectoryProvider traceDirectoryProvider)
    {
        FileTelemetrySender sender = new();
```

and near the end of the constructor:

```csharp
        sender.ConfigureExporter(builder, traceDirectoryProvider.GetTraceDirectory());
        TracerProvider = builder.Build();
```

- [ ] **Step 6: Run the tests to verify they pass**

Run: `dotnet test tests/Core.Telemetry.Tests/Core.Telemetry.Tests.csproj --filter TraceDirectoryProviderTests`
Expected: PASS, 2 tests.

- [ ] **Step 7: Build the solution to confirm nothing else consumed the old constructor**

Run: `dotnet build azclips.slnx`
Expected: build succeeds. `TUITelemetryProvider` and `PSTelemetryProvider` construct their own
senders and are unaffected; no test constructs `CLITelemetryProvider`.

- [ ] **Step 8: Commit**

```bash
git add src/Core/Core.Telemetry/ITraceDirectoryProvider.cs src/Core/Core.Telemetry/DIRegisterExtensions.cs src/CLI/Telemetry/CLITelemetryProvider.cs tests/Core.Telemetry.Tests/TraceDirectoryProviderTests.cs
git commit -m "feat(telemetry): make trace buffer directory injectable"
```

---

### Task 2: Benchmark identity and the fixed directory provider

Two small helpers the benchmark needs before it can name or route anything.

**Files:**
- Create: `tests/Benchmark/Telemetry/BenchmarkName.cs`
- Create: `tests/Benchmark/Telemetry/FixedTraceDirectoryProvider.cs`

**Interfaces:**
- Consumes: `Core.Telemetry.ITraceDirectoryProvider` from Task 1.
- Produces:
  - `Benchmark.Telemetry.BenchmarkName.FromCommandLine()` → `string`, throws
    `InvalidOperationException` when `--benchmarkName` is absent.
  - `Benchmark.Telemetry.BenchmarkName.FromArguments(IReadOnlyList<string>)` → `string`, the
    testable core of the above.
  - `Benchmark.Telemetry.FixedTraceDirectoryProvider(string directory)`, an
    `ITraceDirectoryProvider`. Task 5 constructs one per iteration.

- [ ] **Step 1: Create the benchmark-name resolver**

Create `tests/Benchmark/Telemetry/BenchmarkName.cs`:

```csharp
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text;

namespace Benchmark.Telemetry;

/// <summary>
/// Resolves which benchmark method the current process is running.
/// </summary>
/// <remarks>
/// BenchmarkDotNet runs one child process per benchmark case and passes
/// <c>--benchmarkName &lt;fully.qualified.Type.Method&gt;</c> to it (see
/// <c>BenchmarkDotNet.Running.BenchmarkId.ToArguments</c>). This is the only authoritative source:
/// neither <c>[GlobalSetup]</c> nor <c>[IterationSetup]</c> receives the running
/// <c>BenchmarkCase</c>, and <c>GetType()</c> yields only the class — at runtime, BenchmarkDotNet's
/// generated subclass of it. Using the argument also means the file name matches the BenchmarkDotNet
/// summary row exactly, so a telemetry file always correlates to a reported result.
/// </remarks>
internal static class BenchmarkName
{
    private const string ArgumentName = "--benchmarkName";
    private const string NamespacePrefix = "Benchmark.";

    internal static string FromCommandLine() => FromArguments(Environment.GetCommandLineArgs());

    internal static string FromArguments(IReadOnlyList<string> args)
    {
        // Stop one short of the end: the flag is useless without its value.
        for (var i = 0; i < args.Count - 1; i++)
        {
            if (string.Equals(args[i], ArgumentName, StringComparison.Ordinal))
            {
                return Sanitize(StripNamespace(args[i + 1]));
            }
        }

        throw new InvalidOperationException(
            $"'{ArgumentName}' was not on the command line, so this benchmark cannot name its " +
            "telemetry output. BenchmarkDotNet passes it to every benchmark child process, so this " +
            "means the benchmark was launched directly, or BenchmarkDotNet changed its " +
            "child-process arguments.");
    }

    private static string StripNamespace(string fullName) =>
        fullName.StartsWith(NamespacePrefix, StringComparison.Ordinal)
            ? fullName[NamespacePrefix.Length..]
            : fullName;

    private static string Sanitize(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var buffer = new StringBuilder(name.Length);
        foreach (var character in name)
        {
            buffer.Append(Array.IndexOf(invalid, character) >= 0 ? '_' : character);
        }

        return buffer.ToString();
    }
}
```

- [ ] **Step 2: Create the fixed directory provider**

Create `tests/Benchmark/Telemetry/FixedTraceDirectoryProvider.cs`:

```csharp
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Telemetry;

namespace Benchmark.Telemetry;

/// <summary>
/// Returns one fixed directory.
/// </summary>
/// <remarks>
/// Deliberately carries no warmup-versus-measured logic. The DI container — and therefore this
/// registration — is rebuilt every iteration, so <c>[IterationSetup]</c> already knows which stage
/// it is in and registers the directory for that iteration.
/// </remarks>
internal sealed class FixedTraceDirectoryProvider : ITraceDirectoryProvider
{
    private readonly string _directory;

    internal FixedTraceDirectoryProvider(string directory) => _directory = directory;

    public string GetTraceDirectory() => _directory;
}
```

- [ ] **Step 3: Build to verify both compile**

Run: `dotnet build tests/Benchmark/Benchmark.csproj`
Expected: build succeeds.

If `ITraceDirectoryProvider` fails to resolve, it is a namespace-lookup collision: these files sit in
`Benchmark.Telemetry` and import `Core.Telemetry`, so qualify the using as `global::Core.Telemetry`.
The same applies to `TraceSampler.cs` in Task 4.

- [ ] **Step 4: Commit**

```bash
git add tests/Benchmark/Telemetry/BenchmarkName.cs tests/Benchmark/Telemetry/FixedTraceDirectoryProvider.cs
git commit -m "feat(benchmark): add benchmark name resolver and fixed trace directory provider"
```

---

### Task 3: Detect the measured stage

Warmup and measured iterations are otherwise indistinguishable from benchmark code, and both must
emit telemetry so the cost stays uniform. This listener is what lets them be written to different
directories.

**Files:**
- Create: `tests/Benchmark/Telemetry/BenchmarkStageListener.cs`

**Interfaces:**
- Consumes: nothing from earlier tasks.
- Produces: `Benchmark.Telemetry.BenchmarkStageListener`, an `IDisposable` `EventListener` exposing
  `bool IsMeasured`. Task 5 constructs one in `GlobalSetup`, reads `IsMeasured` in
  `IterationSetup`, and disposes it in `GlobalCleanup`.

- [ ] **Step 1: Create the listener**

Create `tests/Benchmark/Telemetry/BenchmarkStageListener.cs`:

```csharp
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Diagnostics.Tracing;
using BenchmarkDotNet.Engines;

namespace Benchmark.Telemetry;

/// <summary>
/// Tracks whether the BenchmarkDotNet engine is inside the measured (WorkloadActual) stage.
/// </summary>
/// <remarks>
/// Counting iterations instead would be wrong: BenchmarkDotNet's jitting stage invokes the workload
/// once or twice before warmup begins, so a counted boundary is offset, and it drifts silently
/// whenever the configured warmup or iteration counts change. <c>EngineEventSource</c> is the
/// engine's own signal, so it is exact and needs no knowledge of the counts.
/// </remarks>
internal sealed class BenchmarkStageListener : EventListener
{
    // EventListener can deliver OnEventSourceCreated from the base constructor, before this type's
    // field initializers run, and events arrive on the engine's thread. Both are fine for a
    // volatile bool defaulting to false.
    private volatile bool _isMeasured;

    internal bool IsMeasured => _isMeasured;

    protected override void OnEventSourceCreated(EventSource eventSource)
    {
        base.OnEventSourceCreated(eventSource);

        if (string.Equals(eventSource.Name, EngineEventSource.SourceName, StringComparison.Ordinal))
        {
            EnableEvents(eventSource, EventLevel.Informational);
        }
    }

    protected override void OnEventWritten(EventWrittenEventArgs eventData)
    {
        switch (eventData.EventId)
        {
            case EngineEventSource.WorkloadActualStartEventId:
                _isMeasured = true;
                break;

            case EngineEventSource.WorkloadActualStopEventId:
                _isMeasured = false;
                break;
        }
    }
}
```

- [ ] **Step 2: Build to verify it compiles**

Run: `dotnet build tests/Benchmark/Benchmark.csproj`
Expected: build succeeds. If `EngineEventSource` does not resolve, confirm `using
BenchmarkDotNet.Engines;` is present — the type and all the `*EventId` constants are public in
BenchmarkDotNet 0.13.12.

- [ ] **Step 3: Commit**

```bash
git add tests/Benchmark/Telemetry/BenchmarkStageListener.cs
git commit -m "feat(benchmark): detect the BenchmarkDotNet measured stage"
```

---

### Task 4: Sample the measured traces into a single JSONL file

**Files:**
- Modify: `src/Core/Core.Telemetry/Core.Telemetry.csproj:20-25`
- Create: `tests/Benchmark/Telemetry/TraceSampler.cs`

**Interfaces:**
- Consumes: `FileTelemetrySender.ParseTraceFile(string)` and `FileTelemetrySender.TraceEntry`, both
  `internal` to `Core.Telemetry` and reached through the `InternalsVisibleTo` added in this task.
- Produces: `Benchmark.Telemetry.TraceSampler.Prune(string actualDirectory, string outputPath)` →
  `int`, the number of traces written. Task 5 calls it from `GlobalCleanup`.

- [ ] **Step 1: Grant the Benchmark assembly access to `Core.Telemetry` internals**

In `src/Core/Core.Telemetry/Core.Telemetry.csproj`, add `Benchmark` to **both** existing
`InternalsVisibleTo` item groups. Both are required — Release builds are delay-signed, Debug builds
are not:

```xml
  <ItemGroup Condition="'$(Configuration)' == 'Release'">
    <InternalsVisibleTo Include="Core.Telemetry.Tests" Key="$(MSSharedLibPublicKey)" />
    <InternalsVisibleTo Include="Benchmark" Key="$(MSSharedLibPublicKey)" />
  </ItemGroup>
  <ItemGroup Condition="'$(Configuration)' != 'Release'">
    <InternalsVisibleTo Include="Core.Telemetry.Tests" />
    <InternalsVisibleTo Include="Benchmark" />
  </ItemGroup>
```

- [ ] **Step 2: Create the sampler**

Create `tests/Benchmark/Telemetry/TraceSampler.cs`:

```csharp
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Globalization;
using System.Text.Json;
using Core.Telemetry;

namespace Benchmark.Telemetry;

/// <summary>
/// Reduces a benchmark's measured trace files to a fixed-size, evenly spread sample and writes them
/// as JSON Lines.
/// </summary>
/// <remarks>
/// Every iteration emits telemetry so the cost stays uniform across the run — sampling only decides
/// what survives on disk, and it happens after the measurements are taken. Parsing goes through
/// <see cref="FileTelemetrySender.ParseTraceFile"/> so benchmark files are read exactly as the
/// production uploader reads them.
/// </remarks>
internal static class TraceSampler
{
    private const int TargetSampleCount = 100;

    /// <summary>
    /// Writes up to <see cref="TargetSampleCount"/> traces from <paramref name="actualDirectory"/>
    /// to <paramref name="outputPath"/>, one span per line, grouped by trace and in invocation
    /// order. Returns the number of traces written.
    /// </summary>
    internal static int Prune(string actualDirectory, string outputPath)
    {
        if (!Directory.Exists(actualDirectory))
        {
            return 0;
        }

        var sampled = Sample(ReadTracesInOrder(actualDirectory), TargetSampleCount);

        var outputDirectory = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(outputDirectory))
        {
            Directory.CreateDirectory(outputDirectory);
        }

        using var writer = new StreamWriter(outputPath, append: false);
        foreach (var trace in sampled)
        {
            foreach (var span in trace)
            {
                writer.WriteLine(JsonSerializer.Serialize(span));
            }
        }

        return sampled.Count;
    }

    /// <summary>
    /// Groups every span by TraceId — each group is one CLI invocation — and orders the groups by
    /// their earliest span. File names carry no sequence, but StartTime is ISO-8601 with 100ns
    /// resolution, which recovers exact invocation order.
    /// </summary>
    private static List<List<FileTelemetrySender.TraceEntry>> ReadTracesInOrder(string directory)
    {
        var groups = new Dictionary<string, List<FileTelemetrySender.TraceEntry>>(StringComparer.Ordinal);

        foreach (var file in Directory.EnumerateFiles(directory, "traces-*.log"))
        {
            foreach (var entry in FileTelemetrySender.ParseTraceFile(File.ReadAllText(file)))
            {
                var key = entry.TraceId ?? string.Empty;
                if (!groups.TryGetValue(key, out var spans))
                {
                    spans = new List<FileTelemetrySender.TraceEntry>();
                    groups[key] = spans;
                }

                spans.Add(entry);
            }
        }

        return groups.Values.OrderBy(EarliestStart).ToList();
    }

    private static DateTime EarliestStart(List<FileTelemetrySender.TraceEntry> spans)
    {
        var earliest = DateTime.MaxValue;
        foreach (var span in spans)
        {
            if (DateTime.TryParse(
                    span.StartTime,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.RoundtripKind,
                    out var parsed)
                && parsed < earliest)
            {
                earliest = parsed;
            }
        }

        return earliest;
    }

    /// <summary>
    /// Takes every stride-th trace so the sample is spread evenly across the whole measured run
    /// rather than clustered at the start. The stride adapts to the trace count: a 5-iteration
    /// smoke run yields a stride of 1 and keeps all 5 traces rather than emitting nothing.
    /// </summary>
    private static List<List<FileTelemetrySender.TraceEntry>> Sample(
        List<List<FileTelemetrySender.TraceEntry>> traces,
        int target)
    {
        var sampled = new List<List<FileTelemetrySender.TraceEntry>>();
        if (traces.Count == 0 || target <= 0)
        {
            return sampled;
        }

        var stride = Math.Max(1, traces.Count / target);
        for (var i = 0; i < traces.Count && sampled.Count < target; i += stride)
        {
            sampled.Add(traces[i]);
        }

        return sampled;
    }
}
```

- [ ] **Step 3: Build to verify internals access works**

Run: `dotnet build tests/Benchmark/Benchmark.csproj`
Expected: build succeeds. A `CS0122 'FileTelemetrySender.TraceEntry' is inaccessible` error means
the `InternalsVisibleTo` in Step 1 did not take effect — check that the assembly name is exactly
`Benchmark` and that both item groups were edited.

- [ ] **Step 4: Verify the Release configuration too, since the signing key differs**

Run: `dotnet build tests/Benchmark/Benchmark.csproj -c Release`
Expected: build succeeds. This is the configuration benchmarks actually run in.

- [ ] **Step 5: Commit**

```bash
git add src/Core/Core.Telemetry/Core.Telemetry.csproj tests/Benchmark/Telemetry/TraceSampler.cs
git commit -m "feat(benchmark): sample measured traces into a single JSONL file"
```

---

### Task 5: Wire telemetry capture into `CLIBenchmarkBase`

The three hooks that turn the pieces above into a working pipeline. PowerShell benchmarks are
untouched because `RegisterBenchmarkStubs()` is not modified — this class layers on top of it.

**Files:**
- Modify: `tests/Benchmark/CLI/CLIBenchmarkBase.cs:41-91`

**Interfaces:**
- Consumes: `BenchmarkName.FromCommandLine()`, `FixedTraceDirectoryProvider(string)`,
  `BenchmarkStageListener`, `TraceSampler.Prune(string, string)` from Tasks 2–4;
  `Core.Telemetry.ITraceDirectoryProvider` from Task 1.
- Produces: pruned `<benchmark>.jsonl` files in `artifacts/Benchmark/telemetry/`, which Task 6
  collects.

- [ ] **Step 1: Add the using directives and instance fields**

In `tests/Benchmark/CLI/CLIBenchmarkBase.cs`, add to the existing using block:

```csharp
using Benchmark.Telemetry;
using Core.Telemetry;
```

and add these fields next to the existing `private string _extensionsRoot = null!;`:

```csharp
    private BenchmarkStageListener _stage = null!;
    private string _stagingRoot = null!;    // artifacts/Benchmark/telemetry
    private string _telemetryRoot = null!;  // <_stagingRoot>/<benchmark>
    private string _benchmarkName = null!;
```

- [ ] **Step 2: Extend `GlobalSetup`**

Append to the existing `GlobalSetup`, immediately before its closing `Console.WriteLine`:

```csharp
        // One process per benchmark case, so these are constant for the whole run.
        _benchmarkName = BenchmarkName.FromCommandLine();
        _stagingRoot = Path.Combine(RepoRoot, "artifacts", "Benchmark", "telemetry");
        _telemetryRoot = Path.Combine(_stagingRoot, _benchmarkName);

        // The staging path is stable across runs, so this is what stops runs bleeding together.
        TryDeleteDirectory(_telemetryRoot);

        _stage = new BenchmarkStageListener();
```

These must stay in `GlobalSetup`. Moving `TryDeleteDirectory` into `IterationSetup` would delete
every trace written by the previous 9,999 iterations; moving `new BenchmarkStageListener()` there
would leave 10,000 live `EventListener` subscriptions attached for the process lifetime.

- [ ] **Step 3: Add the per-iteration registration**

Change `IterationSetup` to add a third call, and add the new method below it:

```csharp
    [IterationSetup]
    public void IterationSetup()
    {
        RegisterBenchmarkStubs();
        RegisterExtensionResolution();
        RegisterTelemetryCapture();
    }

    /// <summary>
    /// Swaps the stubbed telemetry service back to the production one and points the exporter at
    /// this iteration's stage directory. Runs after <see cref="BenchmarkBase.RegisterBenchmarkStubs"/>
    /// so DI last-wins semantics replace <c>StubTelemetryService</c>, and before
    /// <c>Core.Cli.Program.Main</c> registers <c>CLITelemetryProvider</c> — which is what actually
    /// builds the real exporter pipeline.
    /// </summary>
    private void RegisterTelemetryCapture()
    {
        // Read the stage here rather than lazily inside the provider: BenchmarkDotNet raises
        // WorkloadActualStart before the first measured iteration's IterationSetup, and
        // WorkloadActualStop only after the last one finishes, so this value cannot change
        // underneath the iteration it was captured for.
        var directory = Path.Combine(_telemetryRoot, _stage.IsMeasured ? "actual" : "warmup");

        AzclipsServiceProvider.OverrideRegistration(services =>
        {
            services.AddSingleton<ITelemetryService, TelemetryService>();
            services.AddSingleton<ITraceDirectoryProvider>(new FixedTraceDirectoryProvider(directory));
        });
    }
```

- [ ] **Step 4: Extend `GlobalCleanup` to prune and clean up**

Replace the existing `GlobalCleanup`, keeping its current extensions-home delete and comment:

```csharp
    [GlobalCleanup]
    public void GlobalCleanup()
    {
        _stage?.Dispose();
        PruneTelemetry();

        // Best-effort: InstalledExtensionLoader caches the extension ALC for the process
        // lifetime, so the staged DLL may still be locked here. A leftover home is harmless.
        TryDeleteDirectory(Path.Combine(RepoRoot, "artifacts", "Benchmark", "home"));
    }

    /// <summary>
    /// Samples the measured traces down to the deliverable and removes the raw files. Never throws:
    /// a telemetry-plumbing failure must not change the benchmark's exit code or trip the
    /// regression gate.
    /// </summary>
    private void PruneTelemetry()
    {
        try
        {
            var kept = TraceSampler.Prune(
                Path.Combine(_telemetryRoot, "actual"),
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
```

- [ ] **Step 5: Build both configurations**

Run: `dotnet build azclips.slnx` then `dotnet build azclips.slnx -c Release`
Expected: both succeed.

- [ ] **Step 6: Commit**

```bash
git add tests/Benchmark/CLI/CLIBenchmarkBase.cs
git commit -m "feat(benchmark): capture real telemetry from CLI benchmark iterations"
```

---

### Task 6: Collect telemetry into the timestamped run folder

Benchmarks run in BenchmarkDotNet child processes that cannot know the host's timestamped artifacts
path, so they write to a stable staging directory and the host collects afterwards.

**Files:**
- Modify: `tests/Benchmark/Program.cs:55-99`

**Interfaces:**
- Consumes: the `<benchmark>.jsonl` files Task 5 writes into `artifacts/Benchmark/telemetry/`.
- Produces: those same files under `<artifactsPath>/telemetry/`.

- [ ] **Step 1: Clear the staging directory before the run**

In `tests/Benchmark/Program.cs`, immediately after `artifactsPath` is computed and before the
`ManualConfig.CreateEmpty()` block, add:

```csharp
        // Benchmarks write telemetry to a stable staging path because their child processes cannot
        // know artifactsPath. Clear it first so post-run collection can only pick up this run.
        var telemetryStagingRoot = Path.Combine(
            RepoLocator.GetRepoRoot(), "artifacts", "Benchmark", "telemetry");
        TryDeleteDirectory(telemetryStagingRoot);
```

- [ ] **Step 2: Collect after the benchmarks finish**

Immediately after the `var summaries = ...` line and before `var failures = new List<string>();`,
add:

```csharp
        CollectTelemetry(telemetryStagingRoot, Path.Combine(artifactsPath, "telemetry"));
```

- [ ] **Step 3: Add the helper methods**

Add both methods to `Program`, next to the existing private helpers:

```csharp
    /// <summary>
    /// Moves the pruned per-benchmark telemetry files into this run's artifacts folder.
    /// </summary>
    /// <remarks>
    /// Never throws and never contributes to the failure list: telemetry plumbing must not trip the
    /// 115% regression gate.
    /// </remarks>
    private static void CollectTelemetry(string stagingRoot, string destination)
    {
        try
        {
            var files = Directory.Exists(stagingRoot)
                ? Directory.GetFiles(stagingRoot, "*.jsonl")
                : Array.Empty<string>();

            if (files.Length == 0)
            {
                Console.WriteLine("[Telemetry] No telemetry was produced by this run");
                return;
            }

            Directory.CreateDirectory(destination);
            foreach (var file in files)
            {
                File.Move(file, Path.Combine(destination, Path.GetFileName(file)), overwrite: true);
            }

            Console.WriteLine($"[Telemetry] Collected {files.Length} file(s) into {destination}");
            TryDeleteDirectory(stagingRoot);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Telemetry] Collection failed, benchmarks unaffected: {ex.Message}");
        }
    }

    private static void TryDeleteDirectory(string path)
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
```

- [ ] **Step 4: Build**

Run: `dotnet build tests/Benchmark/Benchmark.csproj -c Release`
Expected: build succeeds.

- [ ] **Step 5: Commit**

```bash
git add tests/Benchmark/Program.cs
git commit -m "feat(benchmark): collect telemetry into the run artifacts folder"
```

---

### Task 7: Update the documentation that now describes stale behaviour

Two documents currently state that command-level telemetry is stubbed out during benchmarks. That
remains true for PowerShell but is no longer true for the CLI.

**Files:**
- Modify: `wiki/02-deep-dive/testing-benchmarking.md:92,96-101`
- Modify: `tests/Benchmark/README.md:56-59,85-88,174`

**Interfaces:**
- Consumes: the behaviour delivered by Tasks 1–6.
- Produces: nothing consumed by later tasks.

- [ ] **Step 1: Correct the stub description in the wiki**

In `wiki/02-deep-dive/testing-benchmarking.md`, keep the existing paragraph that begins
`Benchmarks stay deterministic by reusing the _same_ stub infrastructure` and add this paragraph
directly after it:

```markdown
CLI benchmarks are the one exception to the telemetry stub. `CLIBenchmarkBase.RegisterTelemetryCapture()` runs after `RegisterBenchmarkStubs()` and swaps `ITelemetryService` back to the production `TelemetryService`, so every iteration emits real spans through the real `CLITelemetryProvider`, allowlist filters, and `FileExporter`. Only the destination differs: an injected `ITraceDirectoryProvider` points the exporter at `artifacts/Benchmark/telemetry/<benchmark>/{warmup,actual}/` instead of the shared per-user trace directory. All 10,000 iterations pay the cost so the distribution does not become bimodal; `GlobalCleanup` then samples the measured traces down to 100 and deletes the raw files. See [the design spec](../../docs/superpowers/specs/2026-08-18-benchmark-telemetry-design.md). PowerShell and startup benchmarks keep `StubTelemetryService`.
```

- [ ] **Step 2: Correct the hooks table in the wiki**

In the same file, replace the sentence introducing the hooks table:

```markdown
Two hooks in the CLI entrypoint exist specifically so BenchmarkDotNet can drive `az` in-process without cache warmth skewing timings or benchmark telemetry escaping to Azure:
```

and replace the `In-process guard` row's purpose cell:

```markdown
| In-process guard  | `src/CLI/Program.cs` (`SpawnTelemetryUploader`)                                | Returns early unless the process is a real `az`/`az.exe`, so a benchmark process never spawns a detached telemetry uploader — benchmark traces stay a local artifact and are never uploaded                                                                              |
```

- [ ] **Step 3: Correct the benchmark README**

In `tests/Benchmark/README.md`, add to the `### Output` bullet list:

```markdown
- `telemetry/<PRODUCT>.<Class>.<Method>.jsonl` - 100 sampled CLI traces per benchmark method (one span per line)
```

replace the `[IterationSetup]` bullet:

```markdown
- `[IterationSetup]` (before each run): re-registers stub HTTP/auth services; for CLI it also re-registers the extension loaders + registry store pointed at the staged home, and swaps telemetry back to the production `TelemetryService` with the exporter aimed at this iteration's warmup/measured directory; for PS it creates and opens a fresh runspace and imports the module (both excluded from the measurement, so only the cmdlet invocation is measured).
```

replace the `[GlobalCleanup]` bullet:

```markdown
- `[GlobalCleanup]` (once per class): removes the staged extensions home (best-effort); for CLI it also samples the measured traces down to 100 and deletes the raw trace files.
```

and replace the telemetry sentence in the **Telemetry & Feedback Loop** section:

```markdown
Aggregated benchmark results are what get exported to App Insights. Command-level telemetry is stubbed out for PS benchmarks (`StubTelemetryService`), but CLI benchmarks emit real spans to `artifacts/Benchmark/<timestamp>/telemetry/` so a slow benchmark can be broken down by phase locally. Those files are never uploaded. Analyze them to spot bottlenecks → add new benchmark cases → validate fixes.
```

- [ ] **Step 4: Commit**

```bash
git add wiki/02-deep-dive/testing-benchmarking.md tests/Benchmark/README.md
git commit -m "docs: describe CLI benchmark telemetry emission"
```

---

### Task 8: End-to-end validation

This is the only coverage for `TraceSampler` and `BenchmarkName`, so the assertions are deliberately
stronger than a smoke test's would normally be.

**Files:**
- Temporarily modify: `tests/Benchmark/CLI/CLIBenchmarkBase.cs:21-22` (reverted in Step 6)

**Interfaces:**
- Consumes: everything from Tasks 1–6.
- Produces: nothing.

- [ ] **Step 1: Build Release, which benchmarks require**

```bash
dotnet build azclips.slnx -c Release -p:CopilotNpmRegistryUrl=https://packagefeedproxy.microsoft.io/npm/
./build/Set-ModuleConfiguration.ps1 -c Release
```

Expected: both succeed. The `CopilotNpmRegistryUrl` property is mandatory — without it `Core.Agent`
fails restoring a GitHub Copilot npm package from `registry.npmjs.org` with a TLS handshake error,
and the CLI extensions this run needs never get built.

- [ ] **Step 2: Lower the iteration counts for a fast run**

In `tests/Benchmark/CLI/CLIBenchmarkBase.cs`, change `[WarmupCount(5000)]` → `[WarmupCount(5)]` and
`[IterationCount(5000)]` → `[IterationCount(5)]`. Change nothing else.

This mirrors the `validate-benchmarks` procedure. Treat Steps 2–6 as a try/finally: Step 6 must run
even if the benchmark run fails.

- [ ] **Step 3: Run the CLI benchmarks**

```bash
dotnet run --project tests\Benchmark\Benchmark.csproj -- --local --product CLI -c Release
```

Expected: exit code 0, ending with `[Benchmark] PASS — all benchmarks completed successfully.`, one
`[Telemetry] <name>: kept 5 sampled trace(s)` line per CLI benchmark method, and a
`[Telemetry] Collected N file(s) into ...` line. Ignore all timing numbers — this is a smoke test.

- [ ] **Step 4: Assert the output layout**

```powershell
$run = Get-ChildItem artifacts\Benchmark -Directory |
       Where-Object { $_.Name -match '^\d{4}-\d{2}-\d{2}_' } |
       Sort-Object LastWriteTime | Select-Object -Last 1
Get-ChildItem "$($run.FullName)\telemetry" -Filter *.jsonl | ForEach-Object {
    $traces = (Get-Content $_.FullName | ForEach-Object { ($_ | ConvertFrom-Json).TraceId } |
               Sort-Object -Unique).Count
    "{0}  traces={1}" -f $_.Name, $traces
}
Test-Path artifacts\Benchmark\telemetry
```

Expected, and every assertion is load-bearing:

- One `.jsonl` per CLI benchmark method, each named after a BenchmarkDotNet summary row — proves
  `BenchmarkName.FromCommandLine` resolved and sanitised correctly.
- `traces=5` for every file. **Not 10.** The run does 5 warmup plus 5 measured iterations, so this
  is what proves `BenchmarkStageListener` actually fired and warmup traces were excluded rather
  than merely trimmed. A value of 10 means the listener never saw `WorkloadActualStart`.
- `Test-Path` prints `False` — the staging directory was cleaned up.
- The degenerate stride path is covered for free: 5 traces against a target of 100 gives
  `stride = 1`, which must keep all 5 rather than emit nothing.

- [ ] **Step 5: Spot-check the span content of one file**

```powershell
Get-Content (Get-ChildItem "$($run.FullName)\telemetry\*.jsonl" | Select-Object -First 1) -TotalCount 5 |
    ForEach-Object { $_ | ConvertFrom-Json } | Select-Object DisplayName, Duration, TraceId
```

Expected: real CLI span names such as `CLI.Activity.Root`, `AzureCLI.Startup`, or
`AzureCLI.ParseGlobalArgs` — confirming the production allowlist filters ran and the spans carry
real content rather than being empty shells.

- [ ] **Step 6: Restore the iteration counts — mandatory, even on failure**

```bash
git checkout -- tests/Benchmark/CLI/CLIBenchmarkBase.cs
```

This is only safe because Task 5 committed its edits to that file. Verify:

```bash
git diff -- tests/Benchmark/CLI/CLIBenchmarkBase.cs
```

Expected: empty output, and `[WarmupCount(5000)]` / `[IterationCount(5000)]` are back.

- [ ] **Step 7: Run the affected test suites for regressions**

```bash
dotnet test tests/Core.Telemetry.Tests/Core.Telemetry.Tests.csproj -p:CopilotNpmRegistryUrl=https://packagefeedproxy.microsoft.io/npm/
dotnet test tests/Cli.Tests/Cli.Tests.csproj -p:CopilotNpmRegistryUrl=https://packagefeedproxy.microsoft.io/npm/
```

Expected: all pass. `Cli.Tests` is included because Task 1 changed a CLI constructor. The
`CopilotNpmRegistryUrl` property is harmless where it is not needed, and required if the test
project transitively pulls in `Core.Agent`.

- [ ] **Step 8: Confirm the working tree is clean**

```bash
git status --short
```

Expected: empty. Benchmark runs write only under `artifacts/`, which is not tracked.

---

## Known consequence to flag at review

CLI benchmark means and `[MemoryDiagnoser]` numbers step up once, uniformly, because every iteration
now pays real telemetry cost. The App Insights historical series and the 115% regression gate will
see a one-time discontinuity on the run this lands. Numbers stay comparable run-to-run afterwards.
This was accepted during design — do not treat it as a regression.
