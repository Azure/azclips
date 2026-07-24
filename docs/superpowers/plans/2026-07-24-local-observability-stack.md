# Local Observability Stack (Phase ①) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Let a developer run one `docker compose up` to start a local Jaeger backend and have Debug-build traces (`az group list`, `az config get`, PowerShell cmdlets) export to it in real time, with a hidden Debug/Release-aware switch and a shared exporter seam for future signals.

**Architecture:** A single `TelemetryTarget` (`Local`/`Remote`) is resolved by a telemetry-internal `TelemetryTargetResolver` (compile-time `#if DEBUG` default, overridden only by a direct `AZCLIPS_TELEMETRY_TARGET` env read — never surfaced in `config`). One shared `TelemetryExporters.AddTraceExporter` seam picks OTLP→Jaeger (local) or Azure Monitor (remote). CLI/TUI keep their file-buffer + replay (replay routed through the seam); PowerShell/Agent export inline through the seam. Jaeger `all-in-one` runs from `tools/observability/docker-compose.yml`.

**Tech Stack:** .NET (net10.0), OpenTelemetry 1.15.3, `OpenTelemetry.Exporter.OpenTelemetryProtocol` 1.15.3 (OTLP gRPC), `Azure.Monitor.OpenTelemetry.Exporter` 1.8.0, xUnit, Docker Compose, Jaeger `all-in-one:1.76.0`.

**Reference spec:** `docs/superpowers/specs/2026-07-24-local-observability-stack-design.md`

---

## Background the engineer needs

- **Four telemetry clients, two senders.** CLI and TUI use `FileTelemetrySender`: spans are buffered to JSON files by `FileExporter`, and after the command exits a detached `az --upload-telemetry` process calls the static `FileTelemetrySender.SendPendingStatic`, which replays those files to a `TracerProvider`. PowerShell and the Agent use `DirectTelemetrySender`, which exports inline. Both currently hardcode `AddAzureMonitorTraceExporter`.
- **`Enabled` gates emission.** Each provider (e.g. `src/CLI/Telemetry/CLITelemetryProvider.cs`) reads `configurationService.GetConfiguration<TelemetryOptions>().Enabled` and returns early if false. So the exporter seam is only reached when telemetry is enabled. We do **not** change `Enabled` or any provider.
- **The config surface is an explicit allowlist.** `src/Core/Core.Configuration/Schema/ConfigurationSchema.cs` holds a hardcoded `Keys` list. `config get/list/set` and tab-completion only show keys in that list. We add **nothing** to it and **nothing** to `EnvironmentVariableMappings`, so the target switch stays invisible. We also do **not** add any property to `TelemetryOptions`.
- **Internals are test-visible.** `Core.Telemetry.csproj` already declares `InternalsVisibleTo Core.Telemetry.Tests`, so `internal` types (the enum, resolver, seam) are reachable from tests. xUnit (`[Fact]`/`[Theory]`) is a global using in the test project — do **not** add `using Xunit;`.
- **Windows shell.** Use `;` to sequence PowerShell commands; `&&` only chains external commands. Run `git` with `--no-pager`.

## File structure

Create:
- `src/Core/Core.Telemetry/TelemetryTarget.cs` — the `Local`/`Remote` enum (internal).
- `src/Core/Core.Telemetry/TelemetryTargetResolver.cs` — resolves the effective target (pure core + ambient wrapper).
- `src/Core/Core.Telemetry/TelemetryExporters.cs` — the single "target → trace exporter" seam.
- `tools/observability/docker-compose.yml` — Jaeger `all-in-one`.
- `tools/observability/README.md` — one-command usage + manual validation checklist.
- `tests/Core.Telemetry.Tests/TelemetryTargetResolverTests.cs`
- `tests/Core.Telemetry.Tests/TelemetryExportersTests.cs`
- `tests/Core.Telemetry.Tests/DirectTelemetrySenderTests.cs`

Modify:
- `src/Core/Core.Telemetry/Core.Telemetry.csproj` — add the OTLP exporter package.
- `src/Core/Core.Telemetry/WellKnownEndpoints.cs` — add `LocalOtlp`.
- `src/Core/Core.Telemetry/DirectTelemetrySender.cs` — route through the seam.
- `src/Core/Core.Telemetry/FileTelemetrySender.cs` — route the replay through the seam.

Explicitly **not** modified: `TelemetryOptions.cs`, `ConfigurationSchema.cs`, `EnvironmentVariableMappings.cs`, the four telemetry providers, `.devcontainer/docker-compose.yml`.

---

## Task 1: Add the OTLP exporter package and local endpoint constant

**Files:**
- Modify: `src/Core/Core.Telemetry/Core.Telemetry.csproj`
- Modify: `src/Core/Core.Telemetry/WellKnownEndpoints.cs`

- [ ] **Step 1: Add the OTLP exporter package reference**

In `src/Core/Core.Telemetry/Core.Telemetry.csproj`, add the OTLP package to the existing `PackageReference` group so it reads:

```xml
  <ItemGroup>
    <PackageReference Include="Azure.Monitor.OpenTelemetry.Exporter" Version="1.8.0" />
    <PackageReference Include="OpenTelemetry" Version="1.15.3" />
    <PackageReference Include="OpenTelemetry.Exporter.OpenTelemetryProtocol" Version="1.15.3" />
    <PackageReference Include="OpenTelemetry.Instrumentation.Http" Version="1.15.1" />
  </ItemGroup>
```

- [ ] **Step 2: Add the local OTLP endpoint constant**

Replace the body of `src/Core/Core.Telemetry/WellKnownEndpoints.cs` with:

```csharp
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Core.Telemetry;

public static class WellKnownEndpoints
{
    public const string AzureMonitorConnectionString =
        "InstrumentationKey=75095e49-0ed7-43bf-9a9b-145ce55963c5;IngestionEndpoint=https://eastus-8.in.applicationinsights.azure.com/;LiveEndpoint=https://eastus.livediagnostics.monitor.azure.com/;ApplicationId=c4b7bab3-d4b6-4f80-8440-0f529a622b2f";

    /// <summary>
    /// Default local OTLP/gRPC endpoint for the Jaeger all-in-one dev stack
    /// (tools/observability/docker-compose.yml). Used when the resolved telemetry
    /// target is Local.
    /// </summary>
    public const string LocalOtlp = "http://localhost:4317";
}
```

- [ ] **Step 3: Restore + build the project to verify the package resolves**

Run: `dotnet build src/Core/Core.Telemetry/Core.Telemetry.csproj -c Debug`
Expected: `Build succeeded` with 0 errors. (Building this single project avoids the unrelated Core.Agent npm restore issue.)

- [ ] **Step 4: Commit**

```bash
git add src/Core/Core.Telemetry/Core.Telemetry.csproj src/Core/Core.Telemetry/WellKnownEndpoints.cs
git commit -m "feat(telemetry): add OTLP exporter package and local endpoint constant"
```

---

## Task 2: TelemetryTarget enum + TelemetryTargetResolver (TDD)

**Files:**
- Test: `tests/Core.Telemetry.Tests/TelemetryTargetResolverTests.cs`
- Create: `src/Core/Core.Telemetry/TelemetryTarget.cs`
- Create: `src/Core/Core.Telemetry/TelemetryTargetResolver.cs`

- [ ] **Step 1: Write the failing tests**

Create `tests/Core.Telemetry.Tests/TelemetryTargetResolverTests.cs` (no `using Xunit;` — it is a global using):

```csharp
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Core.Telemetry.Tests;

public class TelemetryTargetResolverTests
{
    [Theory]
    [InlineData(TelemetryTarget.Local)]
    [InlineData(TelemetryTarget.Remote)]
    public void Resolve_ReturnsCompileDefault_WhenEnvUnset(TelemetryTarget compileDefault)
    {
        var result = TelemetryTargetResolver.Resolve(compileDefault, _ => null);

        Assert.Equal(compileDefault, result);
    }

    [Theory]
    [InlineData("local", TelemetryTarget.Local)]
    [InlineData("LOCAL", TelemetryTarget.Local)]
    [InlineData("  local  ", TelemetryTarget.Local)]
    [InlineData("remote", TelemetryTarget.Remote)]
    [InlineData("REMOTE", TelemetryTarget.Remote)]
    public void Resolve_HonorsEnvOverride_RegardlessOfDefault(string value, TelemetryTarget expected)
    {
        // Start from the opposite compile default to prove the override always wins.
        var opposite = expected == TelemetryTarget.Local ? TelemetryTarget.Remote : TelemetryTarget.Local;

        var result = TelemetryTargetResolver.Resolve(opposite, _ => value);

        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("bogus")]
    [InlineData("locale")]
    public void Resolve_FallsBackToCompileDefault_OnUnrecognized(string value)
    {
        Assert.Equal(TelemetryTarget.Remote, TelemetryTargetResolver.Resolve(TelemetryTarget.Remote, _ => value));
        Assert.Equal(TelemetryTarget.Local, TelemetryTargetResolver.Resolve(TelemetryTarget.Local, _ => value));
    }
}
```

- [ ] **Step 2: Run tests to verify they fail (do not compile)**

Run: `dotnet test tests/Core.Telemetry.Tests/Core.Telemetry.Tests.csproj --filter "FullyQualifiedName~TelemetryTargetResolverTests"`
Expected: FAIL — compile error, `TelemetryTarget` / `TelemetryTargetResolver` do not exist.

- [ ] **Step 3: Create the enum**

Create `src/Core/Core.Telemetry/TelemetryTarget.cs`:

```csharp
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Core.Telemetry;

/// <summary>
/// Where telemetry is exported when it is enabled. Internal by design — the
/// direction switch is a developer-only concern and is never surfaced in the
/// user-facing configuration (no schema key, no env-var mapping).
/// </summary>
internal enum TelemetryTarget
{
    /// <summary>Local dev stack (OTLP → Jaeger).</summary>
    Local,

    /// <summary>Remote Azure Monitor / Application Insights.</summary>
    Remote,
}
```

- [ ] **Step 4: Create the resolver**

Create `src/Core/Core.Telemetry/TelemetryTargetResolver.cs`:

```csharp
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;

namespace Core.Telemetry;

/// <summary>
/// Resolves the effective <see cref="TelemetryTarget"/>. The default is chosen at
/// compile time (Debug → Local, Release → Remote) and can be overridden only by a
/// direct read of the <c>AZCLIPS_TELEMETRY_TARGET</c> environment variable. This
/// deliberately bypasses the surfaced configuration pipeline so the switch never
/// appears in <c>config get/list/set</c> or tab-completion.
/// </summary>
internal static class TelemetryTargetResolver
{
    internal const string TargetEnvironmentVariable = "AZCLIPS_TELEMETRY_TARGET";

    /// <summary>Resolves the target using the compile-time default and the process environment.</summary>
    internal static TelemetryTarget Resolve() =>
        Resolve(CompileTimeDefault, Environment.GetEnvironmentVariable);

    /// <summary>
    /// Pure resolution logic. <paramref name="environmentReader"/> is injected so this
    /// can be unit-tested independently of the ambient process environment and build config.
    /// </summary>
    internal static TelemetryTarget Resolve(TelemetryTarget compileDefault, Func<string, string?> environmentReader)
    {
        var raw = environmentReader(TargetEnvironmentVariable)?.Trim();
        if (string.IsNullOrEmpty(raw))
        {
            return compileDefault;
        }

        return raw.ToLowerInvariant() switch
        {
            "local" => TelemetryTarget.Local,
            "remote" => TelemetryTarget.Remote,
            _ => compileDefault,
        };
    }

    private static TelemetryTarget CompileTimeDefault =>
#if DEBUG
        TelemetryTarget.Local;
#else
        TelemetryTarget.Remote;
#endif
}
```

- [ ] **Step 5: Run tests to verify they pass**

Run: `dotnet test tests/Core.Telemetry.Tests/Core.Telemetry.Tests.csproj --filter "FullyQualifiedName~TelemetryTargetResolverTests"`
Expected: PASS — all cases green.

- [ ] **Step 6: Commit**

```bash
git add src/Core/Core.Telemetry/TelemetryTarget.cs src/Core/Core.Telemetry/TelemetryTargetResolver.cs tests/Core.Telemetry.Tests/TelemetryTargetResolverTests.cs
git commit -m "feat(telemetry): add hidden target resolver with env override"
```

---

## Task 3: TelemetryExporters seam (TDD)

**Files:**
- Test: `tests/Core.Telemetry.Tests/TelemetryExportersTests.cs`
- Create: `src/Core/Core.Telemetry/TelemetryExporters.cs`

- [ ] **Step 1: Write the failing smoke test**

Create `tests/Core.Telemetry.Tests/TelemetryExportersTests.cs`:

```csharp
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using OpenTelemetry;
using OpenTelemetry.Trace;

namespace Core.Telemetry.Tests;

public class TelemetryExportersTests
{
    [Theory]
    [InlineData(TelemetryTarget.Local)]
    [InlineData(TelemetryTarget.Remote)]
    public void AddTraceExporter_BuildsProvider_WithoutThrowing(TelemetryTarget target)
    {
        var builder = Sdk.CreateTracerProviderBuilder().AddSource("test-source");

        TelemetryExporters.AddTraceExporter(builder, target);

        using var provider = builder.Build();
        Assert.NotNull(provider);
    }
}
```

- [ ] **Step 2: Run test to verify it fails (does not compile)**

Run: `dotnet test tests/Core.Telemetry.Tests/Core.Telemetry.Tests.csproj --filter "FullyQualifiedName~TelemetryExportersTests"`
Expected: FAIL — compile error, `TelemetryExporters` does not exist.

- [ ] **Step 3: Create the seam**

Create `src/Core/Core.Telemetry/TelemetryExporters.cs`:

```csharp
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using Azure.Monitor.OpenTelemetry.Exporter;
using OpenTelemetry.Trace;

namespace Core.Telemetry;

/// <summary>
/// The single decision point that maps a resolved <see cref="TelemetryTarget"/> to
/// the trace exporter added to a <see cref="TracerProviderBuilder"/>. Future signals
/// (metrics, logs) add sibling methods here so the "given target → exporter" choice
/// lives in exactly one place.
/// </summary>
internal static class TelemetryExporters
{
    internal static void AddTraceExporter(TracerProviderBuilder builder, TelemetryTarget target)
    {
        if (target == TelemetryTarget.Local)
        {
            builder.AddOtlpExporter(options => options.Endpoint = new Uri(WellKnownEndpoints.LocalOtlp));
        }
        else
        {
            builder.AddAzureMonitorTraceExporter(options =>
            {
                options.ConnectionString = WellKnownEndpoints.AzureMonitorConnectionString;
            });
        }
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test tests/Core.Telemetry.Tests/Core.Telemetry.Tests.csproj --filter "FullyQualifiedName~TelemetryExportersTests"`
Expected: PASS — provider builds for both targets.

- [ ] **Step 5: Commit**

```bash
git add src/Core/Core.Telemetry/TelemetryExporters.cs tests/Core.Telemetry.Tests/TelemetryExportersTests.cs
git commit -m "feat(telemetry): add shared target-aware trace exporter seam"
```

---

## Task 4: Route DirectTelemetrySender through the seam (PS/Agent inline)

**Files:**
- Test: `tests/Core.Telemetry.Tests/DirectTelemetrySenderTests.cs`
- Modify: `src/Core/Core.Telemetry/DirectTelemetrySender.cs`

- [ ] **Step 1: Write the failing test**

Create `tests/Core.Telemetry.Tests/DirectTelemetrySenderTests.cs`:

```csharp
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using OpenTelemetry;
using OpenTelemetry.Trace;

namespace Core.Telemetry.Tests;

public class DirectTelemetrySenderTests
{
    [Fact]
    public void ConfigureExporter_BuildsProvider_WithoutThrowing()
    {
        var sender = new DirectTelemetrySender();
        var builder = Sdk.CreateTracerProviderBuilder().AddSource("test-source");

        sender.ConfigureExporter(builder, traceDirectory: string.Empty);

        using var provider = builder.Build();
        Assert.NotNull(provider);
    }
}
```

- [ ] **Step 2: Run test to verify current behavior still builds (baseline PASS)**

Run: `dotnet test tests/Core.Telemetry.Tests/Core.Telemetry.Tests.csproj --filter "FullyQualifiedName~DirectTelemetrySenderTests"`
Expected: PASS — the current `AddAzureMonitorTraceExporter` implementation also builds without throwing. (This test guards the refactor in Step 3; it must stay green afterward.)

- [ ] **Step 3: Route the exporter through the seam**

Replace the entire contents of `src/Core/Core.Telemetry/DirectTelemetrySender.cs` with (note the `Azure.Monitor.OpenTelemetry.Exporter` using is removed — it is now unused here):

```csharp
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Threading.Tasks;
using OpenTelemetry.Trace;

namespace Core.Telemetry;

/// <summary>
/// Telemetry sender for PowerShell/Agent: exports traces inline via the
/// TracerProvider pipeline. The exporter is chosen by the shared target seam
/// (local OTLP → Jaeger, or remote Azure Monitor). No file buffering or replay.
/// </summary>
public class DirectTelemetrySender : ITelemetrySender
{
    public void ConfigureExporter(TracerProviderBuilder builder, string traceDirectory)
    {
        TelemetryExporters.AddTraceExporter(builder, TelemetryTargetResolver.Resolve());
    }

    /// <summary>
    /// No-op: telemetry is already exported inline by the TracerProvider.
    /// </summary>
    public Task SendPendingAsync() => Task.CompletedTask;
}
```

- [ ] **Step 4: Run test to verify it still passes**

Run: `dotnet test tests/Core.Telemetry.Tests/Core.Telemetry.Tests.csproj --filter "FullyQualifiedName~DirectTelemetrySenderTests"`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/Core/Core.Telemetry/DirectTelemetrySender.cs tests/Core.Telemetry.Tests/DirectTelemetrySenderTests.cs
git commit -m "feat(telemetry): route DirectTelemetrySender through target seam"
```

---

## Task 5: Route FileTelemetrySender replay through the seam (CLI/TUI)

**Files:**
- Modify: `src/Core/Core.Telemetry/FileTelemetrySender.cs`

Context: only the **replay** in `SendPendingStatic` changes. The buffering half (`ConfigureExporter` → `FileExporter`) is untouched — CLI/TUI always buffer to disk regardless of target; the target only decides where the detached replay ships them. The replay resolves the target once and applies the seam per service group. Existing `ReplayEntries`/ordering tests in `FileTelemetrySenderTests.cs` continue to cover replay correctness.

- [ ] **Step 1: Remove the now-unused Azure Monitor using**

In `src/Core/Core.Telemetry/FileTelemetrySender.cs`, delete this line from the top-of-file usings (the direct Azure Monitor call is being replaced by the seam):

```csharp
using Azure.Monitor.OpenTelemetry.Exporter;
```

- [ ] **Step 2: Route the replay provider through the seam**

In `SendPendingStatic`, replace this block:

```csharp
        var replayedFiles = new List<string>();

        foreach (var (serviceName, fileGroup) in tracesByService)
        {
            using var source = new ActivitySource(serviceName);
            using var provider = Sdk.CreateTracerProviderBuilder()
                .AddSource(serviceName)
                .ConfigureResource(r => r.AddService(serviceName: serviceName))
                .AddAzureMonitorTraceExporter(options =>
                {
                    options.ConnectionString = WellKnownEndpoints.AzureMonitorConnectionString;
                })
                .Build()!;
```

with:

```csharp
        var replayedFiles = new List<string>();

        // Resolve once so every service group in this replay uses the same destination.
        var target = TelemetryTargetResolver.Resolve();

        foreach (var (serviceName, fileGroup) in tracesByService)
        {
            using var source = new ActivitySource(serviceName);
            var builder = Sdk.CreateTracerProviderBuilder()
                .AddSource(serviceName)
                .ConfigureResource(r => r.AddService(serviceName: serviceName));
            TelemetryExporters.AddTraceExporter(builder, target);
            using var provider = builder.Build()!;
```

Leave the rest of the loop body (the `allEntries`/`ReplayEntries`/`ForceFlush` logic) exactly as-is.

- [ ] **Step 3: Build the project to verify it compiles**

Run: `dotnet build src/Core/Core.Telemetry/Core.Telemetry.csproj -c Debug`
Expected: `Build succeeded`, 0 warnings/errors (confirms no leftover unused `using`).

- [ ] **Step 4: Run the full telemetry test project to confirm no replay regression**

Run: `dotnet test tests/Core.Telemetry.Tests/Core.Telemetry.Tests.csproj`
Expected: PASS — all existing `FileTelemetrySenderTests` plus the new resolver/seam/direct tests are green.

- [ ] **Step 5: Commit**

```bash
git add src/Core/Core.Telemetry/FileTelemetrySender.cs
git commit -m "feat(telemetry): route CLI/TUI replay through target seam"
```

---

## Task 6: Docker compose stack + README

**Files:**
- Create: `tools/observability/docker-compose.yml`
- Create: `tools/observability/README.md`

- [ ] **Step 1: Create the compose file**

Create `tools/observability/docker-compose.yml`:

```yaml
services:
  jaeger:
    image: jaegertracing/all-in-one:1.76.0
    container_name: azclips-jaeger
    environment:
      - COLLECTOR_OTLP_ENABLED=true
    ports:
      - "16686:16686"   # Jaeger UI
      - "4317:4317"     # OTLP gRPC  (the app exports here)
      - "4318:4318"     # OTLP HTTP  (alternate ingest)
    restart: unless-stopped
    healthcheck:
      test: ["CMD", "wget", "--no-verbose", "--tries=1", "--spider", "http://localhost:16686"]
      interval: 5s
      timeout: 3s
      retries: 5
      start_period: 10s
```

- [ ] **Step 2: Create the README**

Create `tools/observability/README.md`:

````markdown
# Local Observability Stack

A one-command local trace backend (Jaeger) for viewing traces/spans from
Debug-build `az` and PowerShell runs in real time, instead of remote App Insights.

## Start / stop

```bash
# from the repo root
docker compose -f tools/observability/docker-compose.yml up -d
docker compose -f tools/observability/docker-compose.yml down
```

Open the UI at http://localhost:16686.

## How routing works

Telemetry direction is chosen by a hidden, developer-only switch:

- **Debug builds** default to **local** (traces go to Jaeger on `localhost:4317`).
- **Release builds** default to **remote** (Azure Monitor / App Insights).
- Override either build with the environment variable
  `AZCLIPS_TELEMETRY_TARGET=local` or `AZCLIPS_TELEMETRY_TARGET=remote`.

This switch is intentionally **not** part of `az config` — it never appears in
`config get/list/set` or tab-completion. `telemetry.enabled` still controls
whether telemetry is emitted at all.

> CLI/TUI buffer spans to disk and replay them after each command exits, so local
> traces appear within the per-command replay cadence (near-real-time).
> PowerShell/Agent export inline (immediate).

## Manual validation

1. `docker compose -f tools/observability/docker-compose.yml up -d`
2. Confirm readiness: `docker compose -f tools/observability/docker-compose.yml ps` shows `healthy`.
3. Run a Debug-build command, e.g. `az group list`.
4. Open http://localhost:16686, pick the service, and confirm the trace/spans appear.
5. Set `AZCLIPS_TELEMETRY_TARGET=remote`, re-run, and confirm the trace does **not** appear locally.
````

- [ ] **Step 3: Validate the compose file syntax**

Run: `docker compose -f tools/observability/docker-compose.yml config`
Expected: prints the normalized config with no error (validates YAML + schema). If Docker is unavailable in the environment, skip and note it — the file is verified against the compose spec by inspection.

- [ ] **Step 4: Commit**

```bash
git add tools/observability/docker-compose.yml tools/observability/README.md
git commit -m "feat(observability): add local Jaeger docker-compose stack"
```

---

## Task 7: Full-solution build + non-regression verification

**Files:** none (verification only)

- [ ] **Step 1: Build the whole solution**

Run: `dotnet build azclips.slnx -c Debug`
Expected: `Build succeeded`, 0 errors (warnings-as-errors is on, so 0 warnings too).
If the build fails while restoring the GitHub Copilot npm package for `Core.Agent` (TLS/SSL handshake to registry.npmjs.org), use the **building-the-solution** skill and retry per its guidance — that failure is unrelated to this change.

- [ ] **Step 2: Run the telemetry test project**

Run: `dotnet test tests/Core.Telemetry.Tests/Core.Telemetry.Tests.csproj`
Expected: PASS — resolver, seam, direct-sender, and existing replay tests all green.

- [ ] **Step 3: Run the config tests (proves the switch never surfaced)**

Run: `dotnet test tests/Cli.Tests/Cli.Tests.csproj --filter "FullyQualifiedName~Configuration"`
Expected: PASS unchanged. Because no schema key and no env mapping were added, `ConfigurationSchemaTests`, the `ListConfigurationAsync(includeDefaults:true)` tests, and `ConfigCliTests` behave exactly as before — evidence that `target` is invisible to the config surface.

- [ ] **Step 4: (Optional, requires Docker) End-to-end smoke**

Run:
```bash
docker compose -f tools/observability/docker-compose.yml up -d
```
Then run a Debug-build `az group list`, open http://localhost:16686, and confirm the trace appears. Tear down with `docker compose -f tools/observability/docker-compose.yml down`. This is manual (no live Jaeger in CI).

- [ ] **Step 5: Final commit (if anything was adjusted during verification)**

```bash
git add -A
git commit -m "chore(telemetry): finalize local observability stack (Phase 1)"
```

---

## Self-review (completed by plan author)

- **Spec coverage:** §3.1 architecture → Tasks 4/5/6; §3.2 hidden target switch → Task 2 (+ "not modified" list enforced across tasks); §3.3 senders + seam → Tasks 1/3/4/5; §3.4 docker stack → Task 6; §4 testing (resolver, seam smoke, non-regression, manual) → Tasks 2/3 + Task 7 Steps 2–4; §5 files touched → File structure section. No gaps.
- **Placeholder scan:** every code/YAML/command step contains complete content; no TBD/TODO/"handle edge cases".
- **Type consistency:** `TelemetryTarget {Local,Remote}`, `TelemetryTargetResolver.Resolve()` / `Resolve(TelemetryTarget, Func<string,string?>)`, `TelemetryExporters.AddTraceExporter(TracerProviderBuilder, TelemetryTarget)`, `WellKnownEndpoints.LocalOtlp` — used identically in every referencing task. Package version `1.15.3` and image `1.76.0` match the spec.
