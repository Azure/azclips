# PowerShell Module Import Telemetry Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Capture PowerShell module import time as a telemetry trace, distinguishing cold from warm imports, without the instrumentation itself slowing the import.

**Architecture:** `ModuleInitializer.OnImport` records cheap timestamps while it builds the DI container, then hands the emit to a background thread. This matters because emitting requires `ITelemetryService`, whose construction costs 44 ms against a 21.6 ms import — instrumenting it directly would triple the very thing being measured. The CLI's `PerformanceTimeline` recorder moves to `Core.Telemetry` and is shared by both clients rather than duplicated.

**Tech Stack:** C# / .NET 10, `System.Diagnostics.Activity`, OpenTelemetry 1.15.3, PowerShell 7.6 (`System.Management.Automation`), xunit 2.9.3.

**Design spec:** `docs/superpowers/specs/2026-08-12-ps-module-import-telemetry-design.md`

## Global Constraints

- Use the .NET SDK pinned in `global.json` (`10.0.203`, `rollForward: latestMinor`).
- **Building the solution REQUIRES an extra property**, or `Core.Agent` fails with a TLS handshake error downloading a GitHub Copilot npm package:
  `dotnet build azclips.slnx -p:CopilotNpmRegistryUrl=https://packagefeedproxy.microsoft.io/npm/`
  The same property is needed for `dotnet test` on any project that references `Core.Agent` (`Cli.Tests` does). Add it to every build and test command in this plan.
- `src/**` builds with `TreatWarningsAsErrors=true`, `Nullable=enable`, `ImplicitUsings=enable`, `EnableNETAnalyzers=true`, `AnalysisLevel=latest`. Production code must be analyzer-clean.
- `System.Diagnostics` is **not** an implicit using — add it explicitly in production files that use `Activity`.
- `System.Linq` **is** covered by `ImplicitUsings`; do not add an explicit directive for it.
- Test projects have `Xunit` as an implicit using; `TreatWarningsAsErrors=false`.
- Every new file starts with the two-line header:
  `// Copyright (c) Microsoft Corporation.` / `// Licensed under the MIT License.`
- Span-name convention: `{ClientType}.Performance` for the enclosing span, `{ClientType}.Performance.{Phase}` for phases. PowerShell uses `PowerShell.Performance` and `PowerShell.Performance.Container`.
- Both telemetry filters are deny-by-default. A span name or tag key that is not allow-listed is dropped silently — allowlist entries are not optional.
- Known pre-existing test failure on Windows, **not** caused by this work and not to be fixed here: `Cli.Tests.ManagedIdentityE2ETests.ManagedIdentity_E2E_MultipleIdSelectors_ShouldFail`, because `ResolveCliBinary()` looks for `az` without the `.exe` extension.

## File Structure

| File | Responsibility |
| --- | --- |
| `src/Core/Core.Telemetry/PerformanceTimeline.cs` | **Moved** from `src/CLI/Telemetry/`. Records phases and replays them as backdated spans. Now shared and parameterised by enclosing span name. |
| `src/CLI/Program.cs` | Updated for the parameterised constructor. No behaviour change. |
| `src/Core/Core.Abstractions/TelemetryTags.cs` | Adds the `importKind` tag key. |
| `src/Core/Core.Telemetry/DefaultTelemetryPropertyFilter.cs` | Allows `importKind`. |
| `src/Core/Core.Telemetry/DefaultTelemetryActivityNameFilter.cs` | Allows `PowerShell.Performance` and the `PowerShell.Performance.` prefix. Also reverts a local debug edit. |
| `src/PS/Login/Telemetry/ImportTelemetry.cs` | **New.** Cold/warm detection, the pure `Emit`, and the background wrapper. |
| `src/PS/Login/ModuleInitializer.cs` | Records the import phases and starts the background emit. |
| `src/PS/Login/Cmdlets.Login.csproj` | Grants `InternalsVisibleTo` to `Cmdlets.Tests`. |
| `tests/Cmdlets.Tests/Cmdlets.Tests.csproj` | Adds a `Core.Telemetry` project reference. |
| `tests/Core.Telemetry.Tests/PerformanceTimelineTests.cs` | **Moved** from `tests/Cli.Tests/Telemetry/`. |
| `tests/Cmdlets.Tests/ImportTelemetryTests.cs` | **New.** Covers cold/warm and the emitted shape. |
| `wiki/02-deep-dive/telemetry.md` | Documents the PowerShell import trace. |

---

### Task 1: Share `PerformanceTimeline` across CLI and PowerShell

Moves the recorder to `Core.Telemetry` and parameterises the one hard-coded value, so PowerShell can reuse it instead of duplicating logic whose nesting guard, out-of-order disposal guard, and leaked-handle fallback were each the subject of review findings.

**Files:**
- Modify: `src/Core/Core.Telemetry/DefaultTelemetryActivityNameFilter.cs` (revert only, Step 1)
- Create: `src/Core/Core.Telemetry/PerformanceTimeline.cs`
- Delete: `src/CLI/Telemetry/PerformanceTimeline.cs`
- Modify: `src/CLI/Program.cs:42`
- Create: `tests/Core.Telemetry.Tests/PerformanceTimelineTests.cs`
- Delete: `tests/Cli.Tests/Telemetry/PerformanceTimelineTests.cs`

**Interfaces:**
- Consumes: `ITelemetryService.StartActivity(string name, ActivityKind kind = ActivityKind.Internal, DateTimeOffset startTime = default)`, already present.
- Produces, in namespace `Core.Telemetry`:
  - `public sealed class PerformanceTimeline`
  - `public PerformanceTimeline(string enclosingSpanName)`
  - `public DateTime StartedAt { get; }`
  - `public IDisposable Measure(string name)`
  - `public void Replay(ITelemetryService telemetry)`

- [ ] **Step 1: Revert the local debug edit to the activity-name filter**

The working tree has an uncommitted change that short-circuits `IsAllowed` to `return true;`, disabling the deny-by-default filter. Nothing downstream can be meaningfully tested while it is in place. Restore the method body:

```csharp
    public bool IsAllowed(string? activityName)
    {
        if (string.IsNullOrEmpty(activityName)) return false;
        if (AllowedExactNames.Contains(activityName)) return true;
        foreach (var prefix in AllowedNamePrefixes)
        {
            if (activityName.StartsWith(prefix, StringComparison.Ordinal)) return true;
        }
        return false;
    }
```

Then commit it on its own so the rest of the plan starts from a clean tree:

```bash
git add src/Core/Core.Telemetry/DefaultTelemetryActivityNameFilter.cs
git commit -m "{Telemetry} Restore deny-by-default activity name filtering"
```

- [ ] **Step 2: Move the test file and update it for the new constructor**

Create `tests/Core.Telemetry.Tests/PerformanceTimelineTests.cs` with the content below, then delete `tests/Cli.Tests/Telemetry/PerformanceTimelineTests.cs`. This is the existing suite with three changes: the namespace and usings move, every `new PerformanceTimeline()` becomes `new PerformanceTimeline("CLI.Performance")`, and one new test pins the parameterisation.

```csharp
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Diagnostics;
using Core.Abstractions;

namespace Core.Telemetry.Tests;

/// <summary>
/// PerformanceTimeline records startup phases before the telemetry stack exists and
/// replays them afterwards. These tests pin the properties that make the replay
/// trustworthy: recorded durations survive, and recorded nesting survives.
/// </summary>
public class PerformanceTimelineTests
{
    private const string Enclosing = "CLI.Performance";

    [Fact]
    public void Replay_EmitsMeasuredPhaseWithItsRecordedDuration()
    {
        using var telemetry = new RecordingTelemetryService();
        var timeline = new PerformanceTimeline(Enclosing);

        using (timeline.Measure("CLI.Performance.Container"))
        {
            Thread.Sleep(20);
        }

        timeline.Replay(telemetry);

        var container = Assert.Single(telemetry.Stopped, a => a.OperationName == "CLI.Performance.Container");
        // Sleep(20) can overshoot on Windows' ~15.6ms timer, so assert a floor, not equality.
        Assert.InRange(container.Duration.TotalMilliseconds, 15d, 5000d);
    }

    [Fact]
    public void Replay_WrapsPhasesInAnEnclosingPerformanceSpan()
    {
        using var telemetry = new RecordingTelemetryService();
        var timeline = new PerformanceTimeline(Enclosing);

        using (timeline.Measure("CLI.Performance.Container"))
        {
        }

        timeline.Replay(telemetry);

        var container = Assert.Single(telemetry.Stopped, a => a.OperationName == "CLI.Performance.Container");
        Assert.Equal("CLI.Performance", container.Parent?.OperationName);
    }

    [Fact]
    public void Replay_UsesTheEnclosingSpanNameGivenToTheConstructor()
    {
        // The CLI and the PowerShell module share this type; each supplies its own
        // "{ClientType}.Performance" name. A hard-coded name would silently mislabel one.
        using var telemetry = new RecordingTelemetryService();
        var timeline = new PerformanceTimeline("PowerShell.Performance");

        using (timeline.Measure("PowerShell.Performance.Container"))
        {
        }

        timeline.Replay(telemetry);

        Assert.Single(telemetry.Stopped, a => a.OperationName == "PowerShell.Performance");
        Assert.DoesNotContain(telemetry.Stopped, a => a.OperationName == "CLI.Performance");
    }

    [Fact]
    public void Replay_SequentialMeasures_BecomeSiblings()
    {
        using var telemetry = new RecordingTelemetryService();
        var timeline = new PerformanceTimeline(Enclosing);

        using (timeline.Measure("first"))
        {
        }

        using (timeline.Measure("second"))
        {
        }

        timeline.Replay(telemetry);

        var first = Assert.Single(telemetry.Stopped, a => a.OperationName == "first");
        var second = Assert.Single(telemetry.Stopped, a => a.OperationName == "second");
        Assert.Equal(Enclosing, first.Parent?.OperationName);
        Assert.Equal(Enclosing, second.Parent?.OperationName);
    }

    [Fact]
    public void Replay_NestedMeasures_BecomeNestedSpans()
    {
        using var telemetry = new RecordingTelemetryService();
        var timeline = new PerformanceTimeline(Enclosing);

        using (timeline.Measure("outer"))
        using (timeline.Measure("inner"))
        {
        }

        timeline.Replay(telemetry);

        var outer = Assert.Single(telemetry.Stopped, a => a.OperationName == "outer");
        var inner = Assert.Single(telemetry.Stopped, a => a.OperationName == "inner");
        Assert.Equal(Enclosing, outer.Parent?.OperationName);
        Assert.Equal("outer", inner.Parent?.OperationName);
    }

    [Fact]
    public void Replay_WithNothingMeasured_EmitsNothing()
    {
        using var telemetry = new RecordingTelemetryService();

        new PerformanceTimeline(Enclosing).Replay(telemetry);

        Assert.Empty(telemetry.Stopped);
    }

    [Fact]
    public void Replay_WhenTelemetryIsDisabled_DoesNotThrow()
    {
        // With telemetry.enabled=false every StartActivity returns null. Replay must
        // tolerate that rather than NullReferenceException on the way out.
        var timeline = new PerformanceTimeline(Enclosing);

        using (timeline.Measure("CLI.Performance.Container"))
        {
        }

        Assert.Null(Record.Exception(() => timeline.Replay(new NullTelemetryService())));
    }

    [Fact]
    public void StartedAt_IsCapturedAtConstruction()
    {
        var beforeConstruction = DateTime.UtcNow;
        var timeline = new PerformanceTimeline(Enclosing);
        var afterConstruction = DateTime.UtcNow;

        Assert.InRange(timeline.StartedAt, beforeConstruction, afterConstruction);
    }

    [Fact]
    public void OutOfOrderDisposal_DoesNotCorruptParenting()
    {
        using var telemetry = new RecordingTelemetryService();
        var timeline = new PerformanceTimeline(Enclosing);

        var a = timeline.Measure("a");
        var b = timeline.Measure("b");
        a.Dispose();
        b.Dispose();

        using (timeline.Measure("c"))
        {
        }

        timeline.Replay(telemetry);

        var c = Assert.Single(telemetry.Stopped, x => x.OperationName == "c");
        Assert.Equal(Enclosing, c.Parent?.OperationName);
    }

    [Fact]
    public void EnclosingSpanCoversAllPhases()
    {
        using var telemetry = new RecordingTelemetryService();
        var timeline = new PerformanceTimeline(Enclosing);

        using (timeline.Measure("first"))
        {
            Thread.Sleep(10);
        }

        using (timeline.Measure("second"))
        {
            Thread.Sleep(10);
        }

        timeline.Replay(telemetry);

        var enclosing = Assert.Single(telemetry.Stopped, a => a.OperationName == Enclosing);
        var first = Assert.Single(telemetry.Stopped, a => a.OperationName == "first");
        var second = Assert.Single(telemetry.Stopped, a => a.OperationName == "second");
        var enclosingEnd = enclosing.StartTimeUtc + enclosing.Duration;
        Assert.True(enclosingEnd >= first.StartTimeUtc + first.Duration);
        Assert.True(enclosingEnd >= second.StartTimeUtc + second.Duration);
    }

    [Fact]
    public void Replay_LeakedMeasureHandle_DoesNotEmitNegativeDuration()
    {
        using var telemetry = new RecordingTelemetryService();
        var timeline = new PerformanceTimeline(Enclosing);

        // Deliberately never disposed.
        timeline.Measure("leaked");

        timeline.Replay(telemetry);

        var leaked = Assert.Single(telemetry.Stopped, a => a.OperationName == "leaked");
        Assert.True(leaked.Duration >= TimeSpan.Zero);
    }

    /// <summary>ITelemetryService backed by a real ActivitySource plus a listener, so
    /// spans are actually created and their stop events can be inspected.</summary>
    private sealed class RecordingTelemetryService : ITelemetryService
    {
        private readonly ActivitySource _source;
        private readonly ActivityListener _listener;

        public RecordingTelemetryService()
        {
            _source = new ActivitySource("perf-timeline-test-" + Guid.NewGuid().ToString("N"));
            _listener = new ActivityListener
            {
                ShouldListenTo = src => src == _source,
                Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
                SampleUsingParentId = (ref ActivityCreationOptions<string> _) => ActivitySamplingResult.AllData,
                ActivityStopped = Stopped.Add,
            };
            ActivitySource.AddActivityListener(_listener);
        }

        public List<Activity> Stopped { get; } = new();

        public Activity? StartActivity(string name, ActivityKind kind = ActivityKind.Internal, DateTimeOffset startTime = default) =>
            _source.StartActivity(name, kind, parentContext: default, tags: null, links: null, startTime: startTime);

        public Activity? StartRootActivity(DateTimeOffset startTime = default) =>
            StartActivity("Test.Activity.Root", ActivityKind.Server, startTime);

        public Activity? TagInvocation(CommandInvocation commandInvocation) => Activity.Current;

        public Activity? AddTag(string propertyName, object? value, Activity? activity = null) => activity;

        public void Dispose()
        {
            _listener.Dispose();
            _source.Dispose();
        }
    }

    /// <summary>Stands in for a telemetry service with telemetry.enabled=false.</summary>
    private sealed class NullTelemetryService : ITelemetryService
    {
        public Activity? StartActivity(string name, ActivityKind kind = ActivityKind.Internal, DateTimeOffset startTime = default) => null;
        public Activity? StartRootActivity(DateTimeOffset startTime = default) => null;
        public Activity? TagInvocation(CommandInvocation commandInvocation) => null;
        public Activity? AddTag(string propertyName, object? value, Activity? activity = null) => null;
        public void Dispose() { }
    }
}
```

- [ ] **Step 3: Run the tests to verify they fail**

```bash
dotnet test tests/Core.Telemetry.Tests/Core.Telemetry.Tests.csproj --filter "FullyQualifiedName~PerformanceTimelineTests"
```

Expected: compile error — `PerformanceTimeline` does not exist in `Core.Telemetry`.

- [ ] **Step 4: Create the shared implementation**

Create `src/Core/Core.Telemetry/PerformanceTimeline.cs`:

```csharp
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Diagnostics;
using Core.Abstractions;

namespace Core.Telemetry;

/// <summary>
/// Times startup phases that run before the telemetry stack exists, and replays them
/// as spans once it does.
///
/// Both clients need this for different reasons. In the CLI the ActivitySource and its
/// listener are created during DI resolution, so nothing can be traced live before that.
/// In the PowerShell module the stack could be built during import, but constructing it
/// costs more than the import being measured. Either way the remedy is the same: note
/// <see cref="DateTime.UtcNow"/> — the same clock <c>Activity.Start()</c> uses — and pass
/// the recorded times to <c>StartActivity</c> through the OpenTelemetry <c>startTime</c>
/// parameter, which exists for work that has already happened.
/// </summary>
public sealed class PerformanceTimeline
{
    private readonly string _enclosingSpanName;
    private readonly List<Phase> _roots = new();
    private readonly Stack<Phase> _open = new();

    /// <param name="enclosingSpanName">
    /// Name of the span that encloses every recorded phase. By convention
    /// <c>"{ClientType}.Performance"</c> — <c>"CLI.Performance"</c>,
    /// <c>"PowerShell.Performance"</c>. Deliberately has no default: a shared default
    /// would silently mislabel whichever client forgot to override it.
    /// </param>
    public PerformanceTimeline(string enclosingSpanName)
    {
        _enclosingSpanName = enclosingSpanName;
    }

    /// <summary>
    /// When the timeline began. Used to backdate the root activity so the replayed
    /// phases nest beneath it rather than starting before it.
    /// </summary>
    public DateTime StartedAt { get; } = DateTime.UtcNow;

    /// <summary>
    /// Times the enclosing block. Nested calls produce nested spans; sequential calls
    /// produce siblings.
    /// </summary>
    public IDisposable Measure(string name)
    {
        // Pop any closed phases from the stack to handle out-of-order disposal.
        while (_open.Count > 0 && _open.Peek().IsClosed)
        {
            _open.Pop();
        }

        var phase = new Phase(name, this);
        (_open.Count > 0 ? _open.Peek().Children : _roots).Add(phase);
        _open.Push(phase);
        return phase;
    }

    /// <summary>
    /// Replays the recorded phases as spans beneath the current activity. Call once the
    /// root activity has started. No-op when nothing was measured, and safe when
    /// telemetry is disabled — every <c>StartActivity</c> then returns null.
    /// </summary>
    public void Replay(ITelemetryService telemetry)
    {
        if (_roots.Count == 0) return;

        using var enclosing = telemetry.StartActivity(_enclosingSpanName, ActivityKind.Internal, StartedAt);
        foreach (var root in _roots)
        {
            Emit(telemetry, root);
        }

        // End the enclosing span at the latest end across all top-level phases.
        enclosing?.SetEndTime(_roots.Max(p => p.End));
    }

    private static void Emit(ITelemetryService telemetry, Phase phase)
    {
        // The span stays current while its children are created, so the ambient
        // Activity.Current parenting reproduces the recorded nesting.
        using var span = telemetry.StartActivity(phase.Name, ActivityKind.Internal, phase.Start);
        foreach (var child in phase.Children)
        {
            Emit(telemetry, child);
        }

        span?.SetEndTime(phase.End);
    }

    private sealed class Phase : IDisposable
    {
        private readonly PerformanceTimeline _timeline;
        private bool _disposed;
        private DateTime _end;

        public Phase(string name, PerformanceTimeline timeline)
        {
            Name = name;
            _timeline = timeline;
        }

        public string Name { get; }

        public DateTime Start { get; } = DateTime.UtcNow;

        /// <summary>
        /// When the phase closed, or now if the handle was never disposed. A leaked
        /// Measure would otherwise leave this at DateTime.MinValue and emit a span
        /// whose end precedes its start.
        /// </summary>
        public DateTime End => _end == default ? DateTime.UtcNow : _end;

        public List<Phase> Children { get; } = new();

        internal bool IsClosed => _disposed;

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _end = DateTime.UtcNow;
            // Only pop if this phase is actually on top of the stack. If disposed out of order,
            // skip the pop to avoid corrupting the stack and silently nesting under the wrong parent.
            if (_timeline._open.Count > 0 && ReferenceEquals(_timeline._open.Peek(), this))
            {
                _timeline._open.Pop();
            }
        }
    }
}
```

Then delete the old file:

```bash
git rm src/CLI/Telemetry/PerformanceTimeline.cs
git rm tests/Cli.Tests/Telemetry/PerformanceTimelineTests.cs
```

- [ ] **Step 5: Update the CLI's construction site**

In `src/CLI/Program.cs`, line 42, supply the name explicitly:

```csharp
        var performance = new PerformanceTimeline("CLI.Performance");
```

No `using` changes are needed: `Program.cs` already imports both `Core.Cli.Telemetry` (still required for `CLITelemetryProvider`) and `Core.Telemetry`.

- [ ] **Step 6: Run the tests to verify they pass**

```bash
dotnet test tests/Core.Telemetry.Tests/Core.Telemetry.Tests.csproj --filter "FullyQualifiedName~PerformanceTimelineTests"
```

Expected: 11 passed.

- [ ] **Step 7: Verify the whole solution still builds and the CLI suite is unaffected**

```bash
dotnet build azclips.slnx -p:CopilotNpmRegistryUrl=https://packagefeedproxy.microsoft.io/npm/
dotnet test tests/Cli.Tests/Cli.Tests.csproj -p:CopilotNpmRegistryUrl=https://packagefeedproxy.microsoft.io/npm/
```

Expected: build succeeds; `Cli.Tests` shows only the known `ManagedIdentityE2ETests` failure.

- [ ] **Step 8: Verify the CLI trace is unchanged**

The move must not alter CLI behaviour. Run the CLI through `dotnet` rather than `az.exe` so `Environment.ProcessPath` is not `az`, `SpawnTelemetryUploader` skips itself, and the trace file survives for inspection:

```bash
dotnet artifacts/Debug/Core.Cli/az.dll --version
```

```powershell
Get-ChildItem "$env:LOCALAPPDATA\.azclips\traces\traces-*.log" |
  Sort-Object LastWriteTime -Descending |
  Select-Object -First 1 |
  Get-Content |
  Select-String -Pattern '"OperationName"|"Duration"'
```

Expected: `CLI.Activity.Root`, `CLI.Performance`, and the `CLI.Performance.*` phases all present with non-zero durations, exactly as before this task. Note the traces directory has a **leading dot** (`.azclips`).

- [ ] **Step 9: Commit**

```bash
git add src/Core/Core.Telemetry/PerformanceTimeline.cs src/CLI/Program.cs tests/Core.Telemetry.Tests/PerformanceTimelineTests.cs
git commit -m "{Telemetry} Share PerformanceTimeline between CLI and PowerShell"
```

---

### Task 2: Allow the PowerShell span names and the `importKind` tag

Both filters are deny-by-default, so without this the import spans are created and then silently dropped. Ships before the emit code so the emit is testable end to end.

**Files:**
- Modify: `src/Core/Core.Abstractions/TelemetryTags.cs`
- Modify: `src/Core/Core.Telemetry/DefaultTelemetryPropertyFilter.cs`
- Modify: `src/Core/Core.Telemetry/DefaultTelemetryActivityNameFilter.cs`
- Test: `tests/Core.Telemetry.Tests/ActivityNameFilterTests.cs`
- Test: `tests/Core.Telemetry.Tests/TelemetryPropertyFilterTests.cs`

**Interfaces:**
- Consumes: nothing from Task 1.
- Produces: `TelemetryTags.ImportKind` — a `public const string` with the value `"importKind"`.

- [ ] **Step 1: Write the failing tests**

In `tests/Core.Telemetry.Tests/ActivityNameFilterTests.cs`, add one line to the exact-name theory, immediately after the existing `[InlineData("CLI.Performance")]`:

```csharp
    [InlineData("PowerShell.Performance")]
```

and one line to the prefix theory, immediately after the existing `[InlineData("CLI.Performance.Container")]`:

```csharp
    [InlineData("PowerShell.Performance.Container")]
```

In `tests/Core.Telemetry.Tests/TelemetryPropertyFilterTests.cs`, add one entry to the `AllowedTelemetryTags()` theory data, after `TelemetryTags.Mode`:

```csharp
        TelemetryTags.ImportKind,
```

- [ ] **Step 2: Run the tests to verify they fail**

```bash
dotnet test tests/Core.Telemetry.Tests/Core.Telemetry.Tests.csproj --filter "FullyQualifiedName~ActivityNameFilterTests|FullyQualifiedName~TelemetryPropertyFilterTests"
```

Expected: the property filter test fails to compile (`TelemetryTags.ImportKind` does not exist); the two activity-name cases fail because the names are not allow-listed.

- [ ] **Step 3: Add the tag constant**

In `src/Core/Core.Abstractions/TelemetryTags.cs`, add to the per-command tag block, after `Mode`:

```csharp
    /// <summary>cold or warm — whether this was the first module import in the process.</summary>
    public const string ImportKind = "importKind";
```

- [ ] **Step 4: Allow the tag key**

In `src/Core/Core.Telemetry/DefaultTelemetryPropertyFilter.cs`, add to `AllowedKeys` after `TelemetryTags.Mode`:

```csharp
        TelemetryTags.ImportKind,
```

- [ ] **Step 5: Allow the span names**

In `src/Core/Core.Telemetry/DefaultTelemetryActivityNameFilter.cs`, add to `AllowedExactNames` immediately after the existing `"CLI.Performance",` entry:

```csharp
        // PowerShell module import phases, replayed by ImportTelemetry on a background
        // thread once the telemetry stack is built. Encloses PowerShell.Performance.*.
        "PowerShell.Performance",
```

and add to `AllowedNamePrefixes` immediately after the existing `"CLI.Performance.",` entry:

```csharp
        // Individual import phases: PowerShell.Performance.Container.
        "PowerShell.Performance.",
```

- [ ] **Step 6: Run the tests to verify they pass**

```bash
dotnet test tests/Core.Telemetry.Tests/Core.Telemetry.Tests.csproj
```

Expected: all pass.

- [ ] **Step 7: Commit**

```bash
git add src/Core/Core.Abstractions/TelemetryTags.cs src/Core/Core.Telemetry/DefaultTelemetryPropertyFilter.cs src/Core/Core.Telemetry/DefaultTelemetryActivityNameFilter.cs tests/Core.Telemetry.Tests/ActivityNameFilterTests.cs tests/Core.Telemetry.Tests/TelemetryPropertyFilterTests.cs
git commit -m "{Telemetry} Allow PowerShell import span names and the importKind tag"
```

---

### Task 3: `ImportTelemetry` — cold/warm detection and the pure emit

The testable core. `Emit` is synchronous and takes its dependencies as parameters, so the emitted shape can be proven correct without a PowerShell host or a background thread.

**Files:**
- Create: `src/PS/Login/Telemetry/ImportTelemetry.cs`
- Modify: `src/PS/Login/Cmdlets.Login.csproj`
- Modify: `tests/Cmdlets.Tests/Cmdlets.Tests.csproj`
- Test: `tests/Cmdlets.Tests/ImportTelemetryTests.cs` (create)

**Interfaces:**
- Consumes: `PerformanceTimeline(string enclosingSpanName)`, `StartedAt`, `Measure`, `Replay` from Task 1; `TelemetryTags.ImportKind` from Task 2.
- Produces, in namespace `Microsoft.Azure.Azclips.Cmdlets.Login.Telemetry`:
  - `internal static class ImportTelemetry`
  - `internal const string PerformanceSpanName = "PowerShell.Performance"`
  - `internal const string ContainerSpanName = "PowerShell.Performance.Container"`
  - `internal const string Cold = "cold"` and `internal const string Warm = "warm"`
  - `internal static string NextImportKind()`
  - `internal static void Emit(ITelemetryService telemetry, PerformanceTimeline timeline, string importKind, DateTime importEnd)`
  - `internal static void EmitInBackground(PerformanceTimeline timeline, string importKind, DateTime importEnd)`

- [ ] **Step 1: Grant the test project access**

In `src/PS/Login/Cmdlets.Login.csproj`, add after the existing `PropertyGroup`, matching the pattern already used by `src/PS/Config/Cmdlets.Config.csproj`:

```xml
    <ItemGroup Condition="'$(Configuration)' == 'Release'">
        <InternalsVisibleTo Include="Cmdlets.Tests" Key="$(MSSharedLibPublicKey)" />
    </ItemGroup>
    <ItemGroup Condition="'$(Configuration)' != 'Release'">
        <InternalsVisibleTo Include="Cmdlets.Tests" />
    </ItemGroup>
```

In `tests/Cmdlets.Tests/Cmdlets.Tests.csproj`, add an explicit reference so the test project can name `PerformanceTimeline` directly rather than relying on transitive flow (`Cmdlets.Login` marks its references `ExcludeAssets=runtime`, so transitivity is not something to depend on):

```xml
    <ProjectReference Include="../../src/Core/Core.Telemetry/Core.Telemetry.csproj" />
```

- [ ] **Step 2: Write the failing tests**

Create `tests/Cmdlets.Tests/ImportTelemetryTests.cs`:

```csharp
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Diagnostics;
using Core.Abstractions;
using Core.Telemetry;
using Microsoft.Azure.Azclips.Cmdlets.Login.Telemetry;

namespace Cmdlets.Tests;

/// <summary>
/// ImportTelemetry turns timestamps recorded during module import into a trace, once the
/// telemetry stack exists. These tests pin the two things that make the number
/// trustworthy: the import is labelled cold or warm correctly, and the root span reports
/// the recorded import window rather than the moment it happened to be emitted.
/// </summary>
public class ImportTelemetryTests
{
    [Fact]
    public void Emit_TagsTheRootWithTheImportKind()
    {
        using var telemetry = new RecordingTelemetryService();
        var timeline = new PerformanceTimeline(ImportTelemetry.PerformanceSpanName);
        using (timeline.Measure(ImportTelemetry.ContainerSpanName))
        {
        }

        ImportTelemetry.Emit(telemetry, timeline, ImportTelemetry.Cold, DateTime.UtcNow);

        var root = Assert.Single(telemetry.Stopped, a => a.OperationName == "Test.Activity.Root");
        Assert.Equal(ImportTelemetry.Cold, root.GetTagItem(TelemetryTags.ImportKind));
    }

    [Fact]
    public void Emit_NestsTheContainerPhaseUnderThePerformanceSpan()
    {
        using var telemetry = new RecordingTelemetryService();
        var timeline = new PerformanceTimeline(ImportTelemetry.PerformanceSpanName);
        using (timeline.Measure(ImportTelemetry.ContainerSpanName))
        {
            Thread.Sleep(20);
        }

        ImportTelemetry.Emit(telemetry, timeline, ImportTelemetry.Cold, DateTime.UtcNow);

        var container = Assert.Single(telemetry.Stopped, a => a.OperationName == ImportTelemetry.ContainerSpanName);
        Assert.Equal(ImportTelemetry.PerformanceSpanName, container.Parent?.OperationName);
        // Sleep(20) can overshoot on Windows' ~15.6ms timer, so assert a floor, not equality.
        Assert.InRange(container.Duration.TotalMilliseconds, 15d, 5000d);
    }

    [Fact]
    public void Emit_ClosesTheRootAtTheRecordedImportEnd_NotAtEmitTime()
    {
        // The root is created on a background thread after import finished. Disposing it
        // normally would report a duration stretching to emit time, inflated by thread
        // scheduling and by the telemetry construction being measured against it.
        using var telemetry = new RecordingTelemetryService();
        var timeline = new PerformanceTimeline(ImportTelemetry.PerformanceSpanName);
        using (timeline.Measure(ImportTelemetry.ContainerSpanName))
        {
        }

        var importEnd = DateTime.UtcNow;
        Thread.Sleep(150); // stands in for the background scheduling + telemetry construction

        ImportTelemetry.Emit(telemetry, timeline, ImportTelemetry.Cold, importEnd);

        var root = Assert.Single(telemetry.Stopped, a => a.OperationName == "Test.Activity.Root");
        Assert.True(
            root.Duration.TotalMilliseconds < 100,
            $"root duration {root.Duration.TotalMilliseconds:N1}ms absorbed the 150ms emit delay");
    }

    [Fact]
    public void Emit_WhenTelemetryIsDisabled_DoesNotThrow()
    {
        var timeline = new PerformanceTimeline(ImportTelemetry.PerformanceSpanName);
        using (timeline.Measure(ImportTelemetry.ContainerSpanName))
        {
        }

        Assert.Null(Record.Exception(() =>
            ImportTelemetry.Emit(new NullTelemetryService(), timeline, ImportTelemetry.Cold, DateTime.UtcNow)));
    }

    [Fact]
    public void NextImportKind_IsColdOnceThenWarm()
    {
        // Static state survives Remove-Module / Import-Module, so the first import in a
        // process is cold and every later one is warm.
        var first = ImportTelemetry.NextImportKind();
        var second = ImportTelemetry.NextImportKind();
        var third = ImportTelemetry.NextImportKind();

        Assert.Equal(ImportTelemetry.Cold, first);
        Assert.Equal(ImportTelemetry.Warm, second);
        Assert.Equal(ImportTelemetry.Warm, third);
    }

    /// <summary>ITelemetryService backed by a real ActivitySource plus a listener, so
    /// spans are actually created and their stop events can be inspected.</summary>
    private sealed class RecordingTelemetryService : ITelemetryService
    {
        private readonly ActivitySource _source;
        private readonly ActivityListener _listener;

        public RecordingTelemetryService()
        {
            _source = new ActivitySource("import-telemetry-test-" + Guid.NewGuid().ToString("N"));
            _listener = new ActivityListener
            {
                ShouldListenTo = src => src == _source,
                Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
                SampleUsingParentId = (ref ActivityCreationOptions<string> _) => ActivitySamplingResult.AllData,
                ActivityStopped = Stopped.Add,
            };
            ActivitySource.AddActivityListener(_listener);
        }

        public List<Activity> Stopped { get; } = new();

        public Activity? StartActivity(string name, ActivityKind kind = ActivityKind.Internal, DateTimeOffset startTime = default) =>
            _source.StartActivity(name, kind, parentContext: default, tags: null, links: null, startTime: startTime);

        public Activity? StartRootActivity(DateTimeOffset startTime = default) =>
            StartActivity("Test.Activity.Root", ActivityKind.Server, startTime);

        public Activity? TagInvocation(CommandInvocation commandInvocation) => Activity.Current;

        public Activity? AddTag(string propertyName, object? value, Activity? activity = null) => activity;

        public void Dispose()
        {
            _listener.Dispose();
            _source.Dispose();
        }
    }

    /// <summary>Stands in for a telemetry service with telemetry.enabled=false.</summary>
    private sealed class NullTelemetryService : ITelemetryService
    {
        public Activity? StartActivity(string name, ActivityKind kind = ActivityKind.Internal, DateTimeOffset startTime = default) => null;
        public Activity? StartRootActivity(DateTimeOffset startTime = default) => null;
        public Activity? TagInvocation(CommandInvocation commandInvocation) => null;
        public Activity? AddTag(string propertyName, object? value, Activity? activity = null) => null;
        public void Dispose() { }
    }
}
```

Note `NextImportKind_IsColdOnceThenWarm` depends on process-wide static state, so it must be the only test that calls `NextImportKind()`. Do not add a second such test — the counter cannot be reset.

- [ ] **Step 3: Run the tests to verify they fail**

```bash
dotnet test tests/Cmdlets.Tests/Cmdlets.Tests.csproj --filter "FullyQualifiedName~ImportTelemetryTests"
```

Expected: compile error — `ImportTelemetry` does not exist.

- [ ] **Step 4: Write the implementation**

Create `src/PS/Login/Telemetry/ImportTelemetry.cs`:

```csharp
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using Core.DependencyInjection;
using Core.Telemetry;

namespace Microsoft.Azure.Azclips.Cmdlets.Login.Telemetry;

/// <summary>
/// Emits the module import trace.
///
/// Import cannot be instrumented directly: emitting a span requires ITelemetryService,
/// whose construction costs roughly twice the import it would be measuring, so doing it
/// inline would triple import time and report a figure inflated by its own
/// instrumentation. Instead OnImport records timestamps — effectively free — and hands
/// the emit to a background thread, which also pre-warms the telemetry stack so the
/// first cmdlet no longer pays for it.
/// </summary>
internal static class ImportTelemetry
{
    internal const string PerformanceSpanName = "PowerShell.Performance";
    internal const string ContainerSpanName = "PowerShell.Performance.Container";

    internal const string Cold = "cold";
    internal const string Warm = "warm";

    private static int _importCount;

    /// <summary>
    /// Returns <see cref="Cold"/> for the first import in this process and
    /// <see cref="Warm"/> thereafter. PowerShell does not unload the module assembly on
    /// Remove-Module, so this static survives a remove/re-import cycle — which is exactly
    /// what makes the distinction meaningful.
    /// </summary>
    internal static string NextImportKind() =>
        Interlocked.Increment(ref _importCount) == 1 ? Cold : Warm;

    /// <summary>
    /// Materialises the recorded timeline as its own trace. Synchronous and free of
    /// ambient state so it can be tested directly.
    /// </summary>
    /// <param name="importEnd">
    /// When import actually finished. Required because this runs after the fact: letting
    /// the root close on dispose would stretch its duration to emit time.
    /// </param>
    internal static void Emit(
        ITelemetryService telemetry,
        PerformanceTimeline timeline,
        string importKind,
        DateTime importEnd)
    {
        // Its own trace, not a child of any cmdlet: import precedes every cmdlet, so
        // attaching backdated phases to a cmdlet root would put a child before its parent.
        using var root = telemetry.StartRootActivity(timeline.StartedAt);
        root?.SetTag(TelemetryTags.ImportKind, importKind);
        timeline.Replay(telemetry);
        root?.SetEndTime(importEnd);
    }

    /// <summary>
    /// Runs <see cref="Emit"/> off the import path. Best effort: nothing is emitted if the
    /// process exits before the thread is scheduled, which is accepted because such a
    /// session runs no cmdlets and so produces no telemetry either way.
    /// </summary>
    internal static void EmitInBackground(PerformanceTimeline timeline, string importKind, DateTime importEnd)
    {
        _ = Task.Run(() =>
        {
            try
            {
                // Check the opt-out before resolving ITelemetryService: resolving it builds
                // the exporter, which a user who disabled telemetry should never get.
                var configuration = AzclipsServiceProvider.GetRequiredService<IConfigurationService>();
                if (!configuration.GetConfiguration<TelemetryOptions>().Enabled)
                {
                    return;
                }

                Emit(AzclipsServiceProvider.GetRequiredService<ITelemetryService>(), timeline, importKind, importEnd);
            }
            catch
            {
                // Best effort. Telemetry must never break module import.
            }
        });
    }
}
```

- [ ] **Step 5: Run the tests to verify they pass**

```bash
dotnet test tests/Cmdlets.Tests/Cmdlets.Tests.csproj --filter "FullyQualifiedName~ImportTelemetryTests"
```

Expected: 5 passed.

- [ ] **Step 6: Commit**

```bash
git add src/PS/Login/Telemetry/ImportTelemetry.cs src/PS/Login/Cmdlets.Login.csproj tests/Cmdlets.Tests/Cmdlets.Tests.csproj tests/Cmdlets.Tests/ImportTelemetryTests.cs
git commit -m "{PS} Add ImportTelemetry for module import tracing"
```

---

### Task 4: Wire into `OnImport`, verify end to end, and document

**Files:**
- Modify: `src/PS/Login/ModuleInitializer.cs`
- Modify: `wiki/02-deep-dive/telemetry.md`

**Interfaces:**
- Consumes: everything from Tasks 1–3.
- Produces: no API change. `OnImport` and `OnRemove` keep their signatures.

- [ ] **Step 1: Wire the recorder into `OnImport`**

Replace the body of `OnImport` in `src/PS/Login/ModuleInitializer.cs`:

```csharp
    public void OnImport()
    {
        // Starts the clock. Nothing is emitted here — see ImportTelemetry for why the
        // emit cannot happen on this thread.
        var timeline = new PerformanceTimeline(ImportTelemetry.PerformanceSpanName);
        var importKind = ImportTelemetry.NextImportKind();

        using (timeline.Measure(ImportTelemetry.ContainerSpanName))
        {
            AzclipsServiceProvider.OverrideRegistration(services =>
            {
                services.AddSingleton<IArgumentParsingProvider, PSArgumentParsingProvider>();
                services.AddSingleton<ITelemetryProvider, PSTelemetryProvider>();
                services.AddSingleton<IUserAgentHandler, PSUserAgentHandler>();
            });
            AzclipsServiceProvider.Initialize(ClientType.PowerShell);
        }

        ImportTelemetry.EmitInBackground(timeline, importKind, DateTime.UtcNow);
    }
```

`ModuleInitializer.cs` already imports `Core.Telemetry` and
`Microsoft.Azure.Azclips.Cmdlets.Login.Telemetry`, so no new `using` directives are
needed. Confirm both are present before assuming it.

- [ ] **Step 2: Build and run the affected suites**

```bash
dotnet build azclips.slnx -p:CopilotNpmRegistryUrl=https://packagefeedproxy.microsoft.io/npm/
dotnet test tests/Cmdlets.Tests/Cmdlets.Tests.csproj -p:CopilotNpmRegistryUrl=https://packagefeedproxy.microsoft.io/npm/
dotnet test tests/Core.Telemetry.Tests/Core.Telemetry.Tests.csproj -p:CopilotNpmRegistryUrl=https://packagefeedproxy.microsoft.io/npm/
```

Expected: build succeeds; both suites pass.

- [ ] **Step 3: Verify import did not get slower**

The whole point of the background emit is that import stays fast. Measure it:

```powershell
$r = "$PWD\src\PS\Login\Login.psd1"
1..3 | ForEach-Object { pwsh -NoProfile -Command "(Measure-Command { Import-Module '$r' }).TotalMilliseconds" }
```

Expected: roughly 20–25 ms per run, matching the pre-change baseline of 21.6 ms. If it has risen to 60 ms or more, the emit is running inline rather than in the background — stop and fix before continuing.

- [ ] **Step 4: Verify the trace end to end**

PowerShell exports directly rather than buffering to disk, and Debug builds resolve to the `Local` target, so the trace is observed through the local collector:

```bash
docker compose -f tools/observability/docker-compose.yml up -d
```

```powershell
pwsh -NoProfile -Command "Import-Module '$PWD\src\PS\Login\Login.psd1'; Start-Sleep -Seconds 8"
```

The `Start-Sleep` matters: the background thread is killed at process exit, and the batching exporter flushes on a timer, so exiting immediately loses the trace.

Open Jaeger at <http://localhost:16686>, select the PowerShell service, and confirm one trace shaped:

```
PowerShell.Activity.Root          importKind = cold
└── PowerShell.Performance
    └── PowerShell.Performance.Container
```

Check that the root's duration is in the tens of milliseconds — the import window — and has not absorbed the background delay. Then verify warm detection by importing twice in one process:

```powershell
pwsh -NoProfile -Command "Import-Module '$PWD\src\PS\Login\Login.psd1'; Remove-Module Login; Import-Module '$PWD\src\PS\Login\Login.psd1'; Start-Sleep -Seconds 8"
```

Expected: two import traces, the first tagged `importKind = cold` and the second `warm`.

- [ ] **Step 5: Document it**

In `wiki/02-deep-dive/telemetry.md`, add a subsection after the existing "Startup phases" section:

````markdown
### PowerShell module import

The PowerShell client is long-lived: one process serves many cmdlets, each opening its own
root activity. Module import happens once, before any of them, so it is emitted as its own
trace rather than nested under a cmdlet — attaching backdated phases to a cmdlet root would
put a child before its parent, which renders incorrectly in Jaeger and Application Insights.

```
PowerShell.Activity.Root          importKind = cold | warm
└── PowerShell.Performance
    └── PowerShell.Performance.Container
```

`ModuleInitializer.OnImport` records the phases with `PerformanceTimeline` and hands the
emit to a background thread (`ImportTelemetry.EmitInBackground`). It cannot emit inline:
resolving `ITelemetryService` builds the whole OpenTelemetry pipeline, which costs about
44 ms against a 21.6 ms import, so inline instrumentation would triple import time and
report a figure inflated by its own cost. Running it in the background also pre-warms the
stack, so the first cmdlet no longer pays for it.

`importKind` distinguishes the first import in a process (`cold`) from later ones (`warm`);
PowerShell does not unload the module assembly on `Remove-Module`, so a static counter
survives a remove/re-import cycle. `PowerShell.Performance.Container` measures the same
work as `CLI.Performance.Container`, so DI container build cost is comparable across
clients.

Two limits are worth knowing. `OnImport` runs near the end of PowerShell's module load
sequence, so assembly loading, manifest processing, and cmdlet export processing are not
captured. And because the emit is deferred, a process that exits immediately after import
emits nothing — such a session runs no cmdlets, so it produces no telemetry either way.
````

The outer fence above uses four backticks so the inner three-backtick trace diagram is
copied through intact; the text to paste into the wiki is everything between them.

- [ ] **Step 6: Commit**

```bash
git add src/PS/Login/ModuleInitializer.cs wiki/02-deep-dive/telemetry.md
git commit -m "{PS} Measure module import time in telemetry"
```

---

## Verification

After all four tasks:

```bash
dotnet build azclips.slnx -p:CopilotNpmRegistryUrl=https://packagefeedproxy.microsoft.io/npm/
dotnet test tests/Core.Telemetry.Tests/Core.Telemetry.Tests.csproj -p:CopilotNpmRegistryUrl=https://packagefeedproxy.microsoft.io/npm/
dotnet test tests/Cmdlets.Tests/Cmdlets.Tests.csproj -p:CopilotNpmRegistryUrl=https://packagefeedproxy.microsoft.io/npm/
dotnet test tests/Cli.Tests/Cli.Tests.csproj -p:CopilotNpmRegistryUrl=https://packagefeedproxy.microsoft.io/npm/
```

`Cli.Tests` is included because Task 1 moves a type it depends on; only the known
`ManagedIdentityE2ETests` failure should appear.
