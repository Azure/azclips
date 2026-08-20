# Telemetry Export Targets Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make a Debug build export telemetry to local Jaeger and Azure Monitor simultaneously, with `AZCLIPS_TELEMETRY_TARGET=local` as the opt-out, while Release stays remote-only and stops reading the environment variable entirely.

**Architecture:** `TelemetryTargetResolver.Resolve()` changes from returning a single `TelemetryTarget` to returning a never-empty `IReadOnlyList<TelemetryTarget>`. A new `TelemetryExporters.AddTraceExporters` takes that list, attaches one exporter per target, and applies the sampler once. The detached uploader's `TracerProviderExportSink` builds one `TracerProvider` per target so each flush is judged independently, and a `SelectAuthoritative` rule decides which target's verdict controls trace-file deletion (Remote outranks Local).

**Tech Stack:** C# / .NET 10 (`net10.0`), OpenTelemetry .NET 1.15.3, Azure.Monitor.OpenTelemetry.Exporter 1.8.0, xUnit.

## Global Constraints

- Target framework is `net10.0`; nullable and implicit usings are **enabled** (`src/Directory.Build.props`).
- `TreatWarningsAsErrors` is **true** and `EnforceCodeStyleInBuild` is **true**. An unused variable, an unused `using`, or a style violation **fails the build**. Do not leave dead code behind.
- **Do not run `dotnet build azclips.slnx`.** A bare solution build can fail restoring an npm package for `Core.Agent`, which is unrelated to this work. Every task in this plan builds and tests only `tests/Core.Telemetry.Tests/Core.Telemetry.Tests.csproj`, which transitively builds `Core.Telemetry` and `Core.Abstractions` and nothing else.
- `TelemetryTarget` is an **internal** enum. A test method whose *signature* mentions it (parameter or return type) must be declared `internal`, not `public` — this is why existing tests such as `TelemetryExportersTests.AddTraceExporter_BuildsProvider_WithoutThrowing` are `internal`. A `public` test method may still reference `TelemetryTarget` freely inside its **body**. xUnit discovers non-public test methods, so `internal` tests run normally.
- `Core.Telemetry.csproj` already grants `InternalsVisibleTo` to `Core.Telemetry.Tests` in both Debug and Release. No project file changes are needed.
- Every file in `src/` starts with the two-line copyright header. Preserve it.
- Collection expressions (`[TelemetryTarget.Local]`) target-type to `IReadOnlyList<T>`. Use them; they are the idiom the spec is written in.
- Spec: `docs/superpowers/specs/2026-08-21-telemetry-export-targets-design.md`.

**Task ordering is load-bearing.** Tasks 1 and 2 are behaviour-preserving refactors that leave the code compiling and green at every commit. Task 3 is the single commit that changes runtime behaviour. Do not reorder them — Task 3 alone would break the compile graph in two files.

---

### Task 1: Plural exporter entry point and sampler hoist

Adds `AddTraceExporters`, which attaches one exporter per target and sets the sampler once instead of only inside the `Remote` branch. Both existing callers move onto it, passing a single-element list. **No behaviour changes** — every call site still resolves exactly one target.

**Files:**
- Modify: `src/Core/Core.Telemetry/TelemetryExporters.cs`
- Modify: `src/Core/Core.Telemetry/DirectTelemetrySender.cs:15`
- Modify: `src/Core/Core.Telemetry/TracerProviderExportSink.cs:30`
- Test: `tests/Core.Telemetry.Tests/TelemetryExportersTests.cs`

**Interfaces:**
- Consumes: `TelemetryTarget` (internal enum, `Local` / `Remote`), `TelemetryExporters.AddTraceExporter(TracerProviderBuilder, TelemetryTarget)` (existing, internal static, stays).
- Produces: `internal static void TelemetryExporters.AddTraceExporters(TracerProviderBuilder builder, IReadOnlyList<TelemetryTarget> targets)` — used by Tasks 2 and 3.

- [ ] **Step 1: Write the failing tests**

Append these two tests to `tests/Core.Telemetry.Tests/TelemetryExportersTests.cs`, inside the existing `TelemetryExportersTests` class. Leave the two existing tests exactly as they are — the spec requires them to survive unchanged.

```csharp
    [Fact]
    public void AddTraceExporters_BuildsProvider_ForASingleTarget()
    {
        var builder = Sdk.CreateTracerProviderBuilder().AddSource("test-source");

        TelemetryExporters.AddTraceExporters(builder, [TelemetryTarget.Local]);

        using var provider = builder.Build();
        Assert.NotNull(provider);
    }

    [Fact]
    public void AddTraceExporters_BuildsProvider_ForBothTargets()
    {
        var builder = Sdk.CreateTracerProviderBuilder().AddSource("test-source");

        TelemetryExporters.AddTraceExporters(builder, [TelemetryTarget.Local, TelemetryTarget.Remote]);

        using var provider = builder.Build();
        Assert.NotNull(provider);
    }
```

These are `public` because neither signature mentions `TelemetryTarget` — only their bodies do.

Building an Azure Monitor exporter here does no network I/O; the existing
`AddTraceExporter_BuildsProvider_WithoutThrowing(TelemetryTarget.Remote)` case already proves
construction is safe in a Debug test run.

- [ ] **Step 2: Run the tests to verify they fail**

```bash
dotnet test tests/Core.Telemetry.Tests/Core.Telemetry.Tests.csproj --filter "FullyQualifiedName~TelemetryExportersTests"
```

Expected: **build failure**, `CS0117: 'TelemetryExporters' does not contain a definition for 'AddTraceExporters'`.

- [ ] **Step 3: Add the plural entry point and hoist the sampler**

Replace the whole body of `src/Core/Core.Telemetry/TelemetryExporters.cs` with:

```csharp
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using Azure.Monitor.OpenTelemetry.Exporter;
using OpenTelemetry.Trace;

namespace Core.Telemetry;

/// <summary>
/// Maps resolved <see cref="TelemetryTarget"/> values to the trace exporters added to a
/// <see cref="TracerProviderBuilder"/>.
/// </summary>
internal static class TelemetryExporters
{
    /// <summary>
    /// Attaches one exporter per target. The sampler is provider-wide, so it is applied
    /// once for the whole set.
    /// </summary>
    internal static void AddTraceExporters(TracerProviderBuilder builder, IReadOnlyList<TelemetryTarget> targets)
    {
        foreach (var target in targets)
        {
            AddTraceExporter(builder, target);
        }

        builder.SetSampler(new AlwaysOnSampler());
    }

    internal static void AddTraceExporter(TracerProviderBuilder builder, TelemetryTarget target)
    {
        if (target == TelemetryTarget.Local)
        {
            builder.AddOtlpExporter(options => options.Endpoint = LocalOtlpEndpointResolver.Resolve());
        }
        else if (target == TelemetryTarget.Remote)
        {
            builder.AddAzureMonitorTraceExporter(options =>
            {
                options.ConnectionString = WellKnownEndpoints.AzureMonitorConnectionString;
            });
        }
        else
        {
            throw new ArgumentOutOfRangeException(nameof(target), target, null);
        }
    }
}
```

The only change to `AddTraceExporter` is the removal of `builder.SetSampler(new AlwaysOnSampler());` from the `Remote` branch.

- [ ] **Step 4: Move both callers onto the plural entry point**

In `src/Core/Core.Telemetry/DirectTelemetrySender.cs`, change line 15 from:

```csharp
        TelemetryExporters.AddTraceExporter(builder, TelemetryTargetResolver.Resolve());
```

to:

```csharp
        TelemetryExporters.AddTraceExporters(builder, [TelemetryTargetResolver.Resolve()]);
```

In `src/Core/Core.Telemetry/TracerProviderExportSink.cs`, change line 30 from:

```csharp
        TelemetryExporters.AddTraceExporter(builder, _target);
```

to:

```csharp
        TelemetryExporters.AddTraceExporters(builder, [_target]);
```

Both call sites still resolve exactly one target, so the sampler behaviour is identical to before this task.

- [ ] **Step 5: Run the full telemetry test project to verify everything passes**

```bash
dotnet test tests/Core.Telemetry.Tests/Core.Telemetry.Tests.csproj
```

Expected: **PASS**, including the two pre-existing `AddTraceExporter_*` tests and `DirectTelemetrySenderTests` / `TracerProviderExportSinkTests`.

- [ ] **Step 6: Commit**

```bash
git add src/Core/Core.Telemetry/TelemetryExporters.cs src/Core/Core.Telemetry/DirectTelemetrySender.cs src/Core/Core.Telemetry/TracerProviderExportSink.cs tests/Core.Telemetry.Tests/TelemetryExportersTests.cs
git commit -m "{Telemetry} Add a plural exporter entry point and hoist the sampler"
```

---

### Task 2: Sink takes a target set with per-target providers

`TracerProviderExportSink` holds a list instead of a single target, builds one provider per target, and uses `SelectAuthoritative` to decide which verdict controls trace-file deletion. `TelemetryUpload` adapts by wrapping the still-singular resolver result in a list. **No behaviour changes** — the list still has exactly one element at runtime.

**Files:**
- Modify: `src/Core/Core.Telemetry/TracerProviderExportSink.cs`
- Modify: `src/Core/Core.Telemetry/TelemetryUpload.cs:18`
- Test: `tests/Core.Telemetry.Tests/TracerProviderExportSinkTests.cs`

**Interfaces:**
- Consumes: `TelemetryExporters.AddTraceExporters(TracerProviderBuilder, IReadOnlyList<TelemetryTarget>)` from Task 1; `ITraceExportSink.TryExport(string, IReadOnlyList<TraceEntry>)`; `TraceReplayer.ReplayEntries(ActivitySource, IReadOnlyList<TraceEntry>)`.
- Produces: `internal TracerProviderExportSink(IReadOnlyList<TelemetryTarget> targets)` and `internal static TelemetryTarget TracerProviderExportSink.SelectAuthoritative(IReadOnlyList<TelemetryTarget> targets)` — used by Task 3.

- [ ] **Step 1: Write the failing tests**

Replace the entire contents of `tests/Core.Telemetry.Tests/TracerProviderExportSinkTests.cs` with:

```csharp
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Core.Telemetry.Tests;

public class TracerProviderExportSinkTests
{
    // Construction only: TryExport would build a live provider and block on ForceFlush.
    // TraceUploaderTests covers the behaviour through a fake.
    [Theory]
    [InlineData(TelemetryTarget.Local)]
    [InlineData(TelemetryTarget.Remote)]
    internal void Constructor_CreatesSink_ForASingleTarget(TelemetryTarget target)
    {
        ITraceExportSink sink = new TracerProviderExportSink([target]);

        Assert.NotNull(sink);
    }

    [Fact]
    public void Constructor_CreatesSink_ForBothTargets()
    {
        ITraceExportSink sink = new TracerProviderExportSink(
            [TelemetryTarget.Local, TelemetryTarget.Remote]);

        Assert.NotNull(sink);
    }

    [Fact]
    public void SelectAuthoritative_PrefersRemote_WhenBothConfigured()
    {
        // Remote owns retention, so a stopped local collector never pins trace files.
        Assert.Equal(
            TelemetryTarget.Remote,
            TracerProviderExportSink.SelectAuthoritative(
                [TelemetryTarget.Local, TelemetryTarget.Remote]));
    }

    [Fact]
    public void SelectAuthoritative_PrefersRemote_RegardlessOfOrder()
    {
        Assert.Equal(
            TelemetryTarget.Remote,
            TracerProviderExportSink.SelectAuthoritative(
                [TelemetryTarget.Remote, TelemetryTarget.Local]));
    }

    [Fact]
    public void SelectAuthoritative_ReturnsRemote_WhenRemoteOnly()
    {
        Assert.Equal(
            TelemetryTarget.Remote,
            TracerProviderExportSink.SelectAuthoritative([TelemetryTarget.Remote]));
    }

    [Fact]
    public void SelectAuthoritative_ReturnsLocal_WhenLocalOnly()
    {
        // Local-only is an explicit choice, so a failed export is retried.
        Assert.Equal(
            TelemetryTarget.Local,
            TracerProviderExportSink.SelectAuthoritative([TelemetryTarget.Local]));
    }
}
```

`Constructor_CreatesSink_ForASingleTarget` stays `internal` because its parameter is a `TelemetryTarget`. The rest are `public` — they mention the enum only in their bodies.

- [ ] **Step 2: Run the tests to verify they fail**

```bash
dotnet test tests/Core.Telemetry.Tests/Core.Telemetry.Tests.csproj --filter "FullyQualifiedName~TracerProviderExportSinkTests"
```

Expected: **build failure** — a collection-expression conversion error (`CS9174` or `CS1503`, since the constructor still takes a single `TelemetryTarget`) and `CS0117: 'TracerProviderExportSink' does not contain a definition for 'SelectAuthoritative'`.

- [ ] **Step 3: Rewrite the sink**

Replace the entire contents of `src/Core/Core.Telemetry/TracerProviderExportSink.cs` with:

```csharp
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Diagnostics;
using OpenTelemetry;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Core.Telemetry;

/// <summary>Exports through one TracerProvider per configured telemetry target.</summary>
internal sealed class TracerProviderExportSink : ITraceExportSink
{
    private const int FlushTimeoutMilliseconds = 10_000;

    private readonly IReadOnlyList<TelemetryTarget> _targets;

    internal TracerProviderExportSink(IReadOnlyList<TelemetryTarget> targets)
    {
        _targets = targets;
    }

    /// <summary>
    /// Picks the target whose verdict decides whether a trace file is deleted. Remote
    /// outranks Local, so a failing local collector never blocks deletion.
    /// </summary>
    internal static TelemetryTarget SelectAuthoritative(IReadOnlyList<TelemetryTarget> targets) =>
        targets.Contains(TelemetryTarget.Remote) ? TelemetryTarget.Remote : TelemetryTarget.Local;

    public bool TryExport(string serviceName, IReadOnlyList<TraceEntry> entries)
    {
        var authoritative = SelectAuthoritative(_targets);
        var delivered = true;

        foreach (var target in _targets)
        {
            var exported = ExportTo(target, serviceName, entries);
            if (target == authoritative)
            {
                delivered = exported;
            }
        }

        return delivered;
    }

    private static bool ExportTo(
        TelemetryTarget target,
        string serviceName,
        IReadOnlyList<TraceEntry> entries)
    {
        using var source = new ActivitySource(serviceName);

        var builder = Sdk.CreateTracerProviderBuilder()
            .AddSource(serviceName)
            .ConfigureResource(r => r.AddService(serviceName: serviceName));
        TelemetryExporters.AddTraceExporters(builder, [target]);

        using var provider = builder.Build()!;

        TraceReplayer.ReplayEntries(source, entries);

        return provider.ForceFlush(FlushTimeoutMilliseconds);
    }
}
```

`Contains` is the LINQ extension method; `System.Linq` is already covered by implicit usings, so no extra `using` is needed.

- [ ] **Step 4: Update the uploader composition root**

In `src/Core/Core.Telemetry/TelemetryUpload.cs`, change line 18 from:

```csharp
            new TracerProviderExportSink(TelemetryTargetResolver.Resolve()))
```

to:

```csharp
            new TracerProviderExportSink([TelemetryTargetResolver.Resolve()]))
```

This is temporary scaffolding: Task 3 changes `Resolve()` to return the list directly and removes the brackets.

- [ ] **Step 5: Run the full telemetry test project to verify everything passes**

```bash
dotnet test tests/Core.Telemetry.Tests/Core.Telemetry.Tests.csproj
```

Expected: **PASS**, including `TraceUploaderTests`, which drives `ITraceExportSink` through its own fake and must be unaffected.

- [ ] **Step 6: Commit**

```bash
git add src/Core/Core.Telemetry/TracerProviderExportSink.cs src/Core/Core.Telemetry/TelemetryUpload.cs tests/Core.Telemetry.Tests/TracerProviderExportSinkTests.cs
git commit -m "{Telemetry} Export through one provider per target, Remote owns retention"
```

---

### Task 3: Resolver returns a target set and Release stops reading the environment

The behaviour change. `Resolve()` returns a never-empty `IReadOnlyList<TelemetryTarget>`; the Debug default becomes both sinks; Release is hard-wired to Remote and its environment reader returns `null` unconditionally.

**Files:**
- Modify: `src/Core/Core.Telemetry/TelemetryTargetResolver.cs`
- Modify: `src/Core/Core.Telemetry/DirectTelemetrySender.cs:15`
- Modify: `src/Core/Core.Telemetry/TelemetryUpload.cs:18`
- Test: `tests/Core.Telemetry.Tests/TelemetryTargetResolverTests.cs`

**Interfaces:**
- Consumes: `TelemetryExporters.AddTraceExporters(TracerProviderBuilder, IReadOnlyList<TelemetryTarget>)` (Task 1); `TracerProviderExportSink(IReadOnlyList<TelemetryTarget>)` (Task 2).
- Produces: `internal static IReadOnlyList<TelemetryTarget> TelemetryTargetResolver.Resolve()`, `internal static IReadOnlyList<TelemetryTarget> TelemetryTargetResolver.Resolve(IReadOnlyList<TelemetryTarget> compileDefault, Func<string, string?> environmentReader)`, `internal static IReadOnlyList<TelemetryTarget> TelemetryTargetResolver.CompileTimeDefault`, `internal static Func<string, string?> TelemetryTargetResolver.EnvironmentReader`.

- [ ] **Step 1: Write the failing tests**

Replace the entire contents of `tests/Core.Telemetry.Tests/TelemetryTargetResolverTests.cs` with:

```csharp
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Core.Telemetry.Tests;

public class TelemetryTargetResolverTests
{
    private static IReadOnlyList<TelemetryTarget> LocalOnly => [TelemetryTarget.Local];
    private static IReadOnlyList<TelemetryTarget> RemoteOnly => [TelemetryTarget.Remote];
    private static IReadOnlyList<TelemetryTarget> Both => [TelemetryTarget.Local, TelemetryTarget.Remote];

    [Fact]
    public void Resolve_ReturnsCompileDefault_WhenEnvUnset()
    {
        Assert.Equal(Both, TelemetryTargetResolver.Resolve(Both, _ => null));
        Assert.Equal(RemoteOnly, TelemetryTargetResolver.Resolve(RemoteOnly, _ => null));
        Assert.Equal(LocalOnly, TelemetryTargetResolver.Resolve(LocalOnly, _ => null));
    }

    [Theory]
    [InlineData("local")]
    [InlineData("LOCAL")]
    [InlineData("  local  ")]
    [InlineData("LoCaL")]
    public void Resolve_LocalToken_ReturnsLocalOnly(string value)
    {
        // Start from a different default to prove the override wins.
        Assert.Equal(LocalOnly, TelemetryTargetResolver.Resolve(Both, _ => value));
    }

    [Theory]
    [InlineData("remote")]
    [InlineData("REMOTE")]
    [InlineData("  remote  ")]
    public void Resolve_RemoteToken_ReturnsRemoteOnly(string value)
    {
        Assert.Equal(RemoteOnly, TelemetryTargetResolver.Resolve(Both, _ => value));
    }

    [Theory]
    [InlineData("both")]
    [InlineData("BOTH")]
    [InlineData("  both  ")]
    public void Resolve_BothToken_ReturnsLocalAndRemote(string value)
    {
        Assert.Equal(Both, TelemetryTargetResolver.Resolve(RemoteOnly, _ => value));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("bogus")]
    [InlineData("locale")]
    [InlineData("none")]
    [InlineData("local,remote")]
    public void Resolve_FallsBackToCompileDefault_OnUnrecognized(string value)
    {
        // "none" is not a token: telemetry.enabled=false is the only opt-out.
        // "local,remote" is not a token: the accepted spelling is "both".
        Assert.Equal(Both, TelemetryTargetResolver.Resolve(Both, _ => value));
        Assert.Equal(RemoteOnly, TelemetryTargetResolver.Resolve(RemoteOnly, _ => value));
    }

    [Fact]
    public void Resolve_NeverReturnsAnEmptySet()
    {
        foreach (var value in new[] { "local", "remote", "both", "bogus", "", "   " })
        {
            Assert.NotEmpty(TelemetryTargetResolver.Resolve(Both, _ => value));
        }

        Assert.NotEmpty(TelemetryTargetResolver.Resolve(Both, _ => null));
    }

    [Fact]
    public void CompileTimeDefault_MatchesBuildConfiguration()
    {
#if DEBUG
        Assert.Equal(Both, TelemetryTargetResolver.CompileTimeDefault);
#else
        Assert.Equal(RemoteOnly, TelemetryTargetResolver.CompileTimeDefault);
#endif
    }

    [Fact]
    public void EnvironmentReader_IsLiveInDebug_AndInertInRelease()
    {
#if DEBUG
        // Uniquely named so it never collides with a real setting.
        const string ProbeVariable = "AZCLIPS_TELEMETRY_TARGET_READER_PROBE";
        try
        {
            Environment.SetEnvironmentVariable(ProbeVariable, "probe-value");
            Assert.Equal("probe-value", TelemetryTargetResolver.EnvironmentReader(ProbeVariable));
        }
        finally
        {
            Environment.SetEnvironmentVariable(ProbeVariable, null);
        }
#else
        // PATH is always set, so a null result proves the reader is inert.
        Assert.Null(TelemetryTargetResolver.EnvironmentReader("PATH"));
#endif
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

```bash
dotnet test tests/Core.Telemetry.Tests/Core.Telemetry.Tests.csproj --filter "FullyQualifiedName~TelemetryTargetResolverTests"
```

Expected: **build failure** — argument conversion errors on `Resolve` (it still takes a single `TelemetryTarget`), plus `CS0122: 'TelemetryTargetResolver.CompileTimeDefault' is inaccessible due to its protection level` and the same for `EnvironmentReader`, which are currently `private`.

- [ ] **Step 3: Rewrite the resolver**

Replace the entire contents of `src/Core/Core.Telemetry/TelemetryTargetResolver.cs` with:

```csharp
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;

namespace Core.Telemetry;

/// <summary>
/// Resolves the effective set of <see cref="TelemetryTarget"/> values. The default is chosen
/// at compile time (Debug → Local and Remote, Release → Remote). In Debug it can be overridden
/// with <c>AZCLIPS_TELEMETRY_TARGET</c>, which accepts <c>local</c>, <c>remote</c>, or
/// <c>both</c>; Release does not read the environment. The variable bypasses the surfaced
/// configuration pipeline, so it never appears in <c>config get/list/set</c> or tab-completion.
///
/// The resolved set is never empty. Telemetry is disabled with <c>telemetry.enabled=false</c>.
/// </summary>
internal static class TelemetryTargetResolver
{
    internal const string TargetEnvironmentVariable = "AZCLIPS_TELEMETRY_TARGET";

    /// <summary>Resolves the target set for this build.</summary>
    internal static IReadOnlyList<TelemetryTarget> Resolve() =>
        Resolve(CompileTimeDefault, EnvironmentReader);

    internal static IReadOnlyList<TelemetryTarget> Resolve(
        IReadOnlyList<TelemetryTarget> compileDefault,
        Func<string, string?> environmentReader)
    {
        var raw = environmentReader(TargetEnvironmentVariable)?.Trim();
        if (string.IsNullOrEmpty(raw))
        {
            return compileDefault;
        }

        return raw.ToLowerInvariant() switch
        {
            "local" => [TelemetryTarget.Local],
            "remote" => [TelemetryTarget.Remote],
            "both" => [TelemetryTarget.Local, TelemetryTarget.Remote],
            _ => compileDefault,
        };
    }

    /// <summary>Reads the process environment in Debug; always returns null in Release.</summary>
    internal static Func<string, string?> EnvironmentReader =>
#if DEBUG
        Environment.GetEnvironmentVariable;
#else
        _ => null;
#endif

    /// <summary>The target set compiled into this build.</summary>
    internal static IReadOnlyList<TelemetryTarget> CompileTimeDefault =>
#if DEBUG
        [TelemetryTarget.Local, TelemetryTarget.Remote];
#else
        [TelemetryTarget.Remote];
#endif
}
```

- [ ] **Step 4: Remove the scaffolding from both call sites**

In `src/Core/Core.Telemetry/DirectTelemetrySender.cs`, change line 15 from:

```csharp
        TelemetryExporters.AddTraceExporters(builder, [TelemetryTargetResolver.Resolve()]);
```

to:

```csharp
        TelemetryExporters.AddTraceExporters(builder, TelemetryTargetResolver.Resolve());
```

While you are in that file, update its class summary to reflect that it can now attach more than one exporter. Replace:

```csharp
/// <summary>
/// Exports traces inline via the TracerProvider pipeline. No file buffering or replay.
/// </summary>
```

with:

```csharp
/// <summary>
/// Exports traces inline via the TracerProvider pipeline, one exporter per resolved
/// target. No file buffering or replay.
/// </summary>
```

In `src/Core/Core.Telemetry/TelemetryUpload.cs`, change line 18 from:

```csharp
            new TracerProviderExportSink([TelemetryTargetResolver.Resolve()]))
```

to:

```csharp
            new TracerProviderExportSink(TelemetryTargetResolver.Resolve()))
```

- [ ] **Step 5: Run the full telemetry test project to verify everything passes**

```bash
dotnet test tests/Core.Telemetry.Tests/Core.Telemetry.Tests.csproj
```

Expected: **PASS**. Note that `DirectTelemetrySenderTests.ConfigureExporter_BuildsProvider_WithoutThrowing` now builds a provider with *both* exporters attached under a Debug test run — that is the new intended behaviour and it must still pass.

- [ ] **Step 6: Verify the Release configuration compiles and passes too**

The `#if` branches are only exercised in a Release build, and this is the one task that changes them.

```bash
dotnet test tests/Core.Telemetry.Tests/Core.Telemetry.Tests.csproj -c Release
```

Expected: **PASS**, with `CompileTimeDefault_MatchesBuildConfiguration` and `EnvironmentReader_IsLiveInDebug_AndInertInRelease` taking their `#else` branches.

- [ ] **Step 7: Commit**

```bash
git add src/Core/Core.Telemetry/TelemetryTargetResolver.cs src/Core/Core.Telemetry/DirectTelemetrySender.cs src/Core/Core.Telemetry/TelemetryUpload.cs tests/Core.Telemetry.Tests/TelemetryTargetResolverTests.cs
git commit -m "{Telemetry} Debug exports to both sinks, Release ignores the target override"
```

---

### Task 4: Update the telemetry documentation

Both telemetry documents describe a single destination and must be corrected. No code changes, so no test cycle — verification is a read-back.

**Files:**
- Modify: `wiki/02-deep-dive/telemetry.md`
- Modify: `eng-docs/spec/telemetry.md`

**Interfaces:**
- Consumes: the final behaviour from Tasks 1–3. Produces nothing.

- [ ] **Step 1: Correct the per-client provider table**

In `wiki/02-deep-dive/telemetry.md`, find this row in the provider table (around line 105):

```markdown
| `DirectTelemetrySender` (PowerShell) | — | Azure Monitor inline, no buffering | `src/Core/Core.Telemetry/DirectTelemetrySender.cs:14-27` |
```

Replace it with:

```markdown
| `DirectTelemetrySender` (PowerShell) | — | Resolved targets inline, no buffering | `src/Core/Core.Telemetry/DirectTelemetrySender.cs` |
```

- [ ] **Step 2: Document the export targets**

In `wiki/02-deep-dive/telemetry.md`, insert this new section immediately **before** the `## Privacy model — the allow-list processors` heading:

```markdown
## Export targets

Where telemetry goes is resolved separately from whether it is collected. `TelemetryTargetResolver`
returns a never-empty set of targets:

| Build | Default targets | Notes |
| --- | --- | --- |
| Debug | Local **and** Remote | Jaeger for fast feedback, Azure Monitor to prove the remote path still works |
| Release | Remote | Unchanged from previous behaviour |

In a Debug build the default can be overridden with `AZCLIPS_TELEMETRY_TARGET`, which accepts
exactly `local`, `remote`, or `both`. Anything else — including an empty value — falls back to the
compiled default. There is no `none`: disabling remote export while developing is
`AZCLIPS_TELEMETRY_TARGET=local`, and disabling telemetry entirely is `telemetry.enabled=false`.

```bash
# Debug only: stop sending your local runs to Application Insights.
AZCLIPS_TELEMETRY_TARGET=local az group list
```

**Release ignores the variable completely.** The suppression lives in the resolver's environment
reader rather than in its parser, so a shipped binary has no override path at all. This is
deliberate: once the resolver can express more than one target, honouring the variable in Release
would let anything able to set an environment variable redirect a user's telemetry to a local
collector, on a port that `AZCLIPS_OTLP_PORT` also controls.

Because Debug spans go to the same Application Insights resource as production traffic, and no tag
distinguishes them, locating your own runs means correlating on `InstallationId`. Marking dev
traffic is deferred; see the design doc for the intended approach.
```

- [ ] **Step 3: Correct the delete-on-success bullet**

In `wiki/02-deep-dive/telemetry.md`, find this bullet in the `### The uploader (TraceUploader)` list:

```markdown
- **Delete only on success** — `TracerProviderExportSink.TryExport` returns false on a failed flush, leaving files on disk for `TraceUploader.Run` to retry on the next attempt.
```

Replace it with:

```markdown
- **Delete only on success** — `TracerProviderExportSink.TryExport` returns false on a failed flush, leaving files on disk for `TraceUploader.Run` to retry on the next attempt. With more than one target it builds **one provider per target** and flushes each independently, then reports the verdict of the highest-ranked target present (Remote outranks Local). A stopped Jaeger container therefore never pins trace files on a developer's machine, while a developer who asked for local-only still gets retries.
```

- [ ] **Step 4: Correct the Azure Monitor destination sentence**

In `wiki/02-deep-dive/telemetry.md`, find:

```markdown
The Azure Monitor destination is a hard-coded connection string in `WellKnownEndpoints` (`src/Core/Core.Telemetry/WellKnownEndpoints.cs:6-9`).
```

Replace it with:

```markdown
The Azure Monitor destination is a hard-coded connection string in `WellKnownEndpoints` (`src/Core/Core.Telemetry/WellKnownEndpoints.cs`), used by both Debug and Release builds.
```

- [ ] **Step 5: Correct the export table in the engineering spec**

In `eng-docs/spec/telemetry.md`, find the table at line 113:

```markdown
| Exporter | Configuration | Enabled |
|----------|---------------|---------|
| **File** | `%LOCALAPPDATA%\AzureCLI\traces\` | Always |
| **Azure Monitor** | App Insights connection string | Unless user Disabled |
```

Replace it with:

```markdown
| Exporter | Configuration | Enabled |
|----------|---------------|---------|
| **File** | `%LOCALAPPDATA%\AzureCLI\traces\` | Always (CLI and TUI) |
| **Azure Monitor** | App Insights connection string | Unless user disabled |
| **OTLP (local)** | `http://localhost:4317`, port via `AZCLIPS_OTLP_PORT` | Debug builds only |

A Debug build exports to the local OTLP collector and Azure Monitor simultaneously; a Release build
exports to Azure Monitor only. See `wiki/02-deep-dive/telemetry.md#export-targets`.
```

- [ ] **Step 6: Verify no stale single-target claims remain**

`grep` is not on PATH in this environment. Use PowerShell:

```powershell
Select-String -Path "wiki\02-deep-dive\telemetry.md","eng-docs\spec\telemetry.md" -Pattern "AZCLIPS_TELEMETRY_TARGET|single target|one destination|Azure Monitor inline"
```

Expected: every hit describes the set-based behaviour introduced by this plan. Any remaining line that implies exactly one destination at a time is a leftover from Steps 1–5 and must be corrected before committing.

- [ ] **Step 7: Commit**

```bash
git add wiki/02-deep-dive/telemetry.md eng-docs/spec/telemetry.md
git commit -m "{Telemetry} Document the Debug dual-sink export targets"
```

---

## Verification checklist

Run after Task 4. Every item must pass before the work is considered done. `grep` is not on PATH in this environment; the searches below use PowerShell `Select-String`.

- [ ] `dotnet test tests/Core.Telemetry.Tests/Core.Telemetry.Tests.csproj` passes.
- [ ] `dotnet test tests/Core.Telemetry.Tests/Core.Telemetry.Tests.csproj -c Release` passes.
- [ ] `git --no-pager log --oneline -4` shows four commits, one per task.
- [ ] `Select-String -Path "src\Core\Core.Telemetry\*.cs" -Pattern "AddTraceExporter\("` returns only the definition in `TelemetryExporters.cs` and its call inside `AddTraceExporters` — no other production caller uses the singular form.
- [ ] `Select-String -Path "src\Core\Core.Telemetry\*.cs" -Pattern "SetSampler"` returns exactly one hit, inside `AddTraceExporters`.

## Known deviations from the spec

- The spec's testing section asks for "one `#if DEBUG`-conditional test asserting that the real `Resolve()` honours the environment in Debug and ignores it in Release." This plan instead asserts `CompileTimeDefault` and `EnvironmentReader` separately (Task 3, Step 1). Calling the real `Resolve()` would read the developer's actual `AZCLIPS_TELEMETRY_TARGET`, making the test fail on any machine where it is set. Asserting the two `#if` members directly covers the same ground without depending on ambient state.
