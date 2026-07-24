# Local Observability Stack — Design (Phase ①)

- **Date:** 2026-07-24
- **Status:** Approved design, ready for implementation planning
- **Scope:** Phase ① — local Jaeger trace backend + hidden Debug/Release-aware target switch + OTLP export + a shared signal-exporter seam
- **Out of scope (future):** Phase ② — KQL/Kusto emulation + Grafana dashboards portable to production App Insights (tracked as a separate follow-up spec)

## 1. Goal

Let a developer spin up a local trace backend with a single command and have dev-time
commands — `az group list`, `az config get`, PowerShell cmdlets — send their traces/spans
to that local backend for **real-time viewing**, instead of remote App Insights. Debug builds
default to local, Release builds default to remote, and a developer-only override can force
either. The design leaves a clean seam so metrics and logs can be added later without rework.

## 2. Background — current telemetry architecture

The repo has four telemetry clients, each of which builds a `TracerProvider` and delegates
exporter wiring to an `ITelemetrySender`:

- **CLI / TUI (buffered):** `FileTelemetrySender` buffers spans to JSON files via `FileExporter`
  (`ConfigureExporter` → `SimpleActivityExportProcessor(FileExporter)`). After each command exits,
  a detached `az --upload-telemetry` process calls the static `FileTelemetrySender.SendPendingStatic`,
  which replays the buffered files to Azure Monitor via `AddAzureMonitorTraceExporter`.
- **PowerShell / Agent (inline):** `DirectTelemetrySender.ConfigureExporter` exports spans inline
  via `AddAzureMonitorTraceExporter`.

Configuration flows through `ConfigurationService`: defaults → JSON config file → environment-variable
overrides. The user-facing config surface (`config get` / `config list` / `config set` / tab-completion)
is gated by an **explicit hardcoded allowlist** in `ConfigurationSchema.Keys`. A property on an options
class is only visible on that surface if it also has a matching entry in that list. Today the only
telemetry key is `telemetry.enabled` (bound to `TelemetryOptions.Enabled`, default `true`).

Relevant facts that shaped the design:
- Jaeger `all-in-one` ingests **OTLP natively** (gRPC `:4317`, HTTP `:4318`) and serves its UI on `:16686`.
- `Enabled` is the master on/off and must remain **exactly as-is** and orthogonal to routing.
- The local/remote direction switch must **not** be surfaced to end users (no new config key,
  no completion entry, no `config get/list/set` visibility).
- The OTLP exporter NuGet is not yet referenced; it must be added.

## 3. Design

### 3.1 Architecture & data flow

One `docker compose up` starts a single container — Jaeger `all-in-one`. No collector; the app talks
OTLP straight to Jaeger. A single resolved **target** (`Local` | `Remote`) decides where each client's
spans land.

```
CLI / TUI (buffered):
  command runs → spans → local trace file → on exit, detached replay → target?
      target = local  → Jaeger :4317
      target = remote → App Insights (unchanged)

PowerShell / Agent (inline):
  cmdlet runs → spans → target?
      target = local  → Jaeger :4317
      target = remote → App Insights (unchanged)

Jaeger all-in-one (docker):
  OTLP in :4317 / :4318  →  UI :16686  (watch traces & spans live)
```

What stays untouched: the file-buffering mechanism, the remote App Insights path, and all four
clients' provider-build flow. We insert a target-aware exporter choice, not a telemetry rewrite.

### 3.2 The target switch — a hidden, telemetry-internal resolver

`Enabled` stays literally unchanged and remains the sole surfaced telemetry key. It gates *whether*
anything is emitted. A new resolver decides *where*, and is deliberately kept off the config surface.

- New enum `TelemetryTarget { Local, Remote }` (no `Off`; `Enabled=false` already means "don't emit").
- New `TelemetryTargetResolver` in `Core.Telemetry`. Resolution is a **pure function** for testability:

  ```csharp
  // pure, no ambient state — unit-testable
  internal static TelemetryTarget Resolve(TelemetryTarget compileDefault, Func<string, string?> envReader);

  // public wrapper passes the real compile-time default + Environment.GetEnvironmentVariable
  public static TelemetryTarget Resolve();
  ```

- **Compile-time default** via `#if DEBUG`:

  ```csharp
  private static TelemetryTarget CompileDefault =>
  #if DEBUG
      TelemetryTarget.Local;
  #else
      TelemetryTarget.Remote;
  #endif
  ```

- **Override** via a **direct** environment-variable read: `AZCLIPS_TELEMETRY_TARGET`, values
  `local` / `remote` (case- and whitespace-insensitive). Unrecognized/empty values fall back to the
  compile default and never throw.

**Why hidden works (verified):** the config surface reads the hardcoded `ConfigurationSchema.Keys`
allowlist. Because we add **no** `telemetry.target` schema entry and **do not** register the env var
in `EnvironmentVariableMappings`, the switch never appears in `config get/list/set` or completion.
`TelemetryOptions` is not modified at all, so no reflected/serialized default can leak it either.
The env var is a developer-only lever that bypasses the surfaced configuration pipeline entirely.

**Precedence:** compile-time default (`#if DEBUG`) → overridden by `AZCLIPS_TELEMETRY_TARGET` when set.
`Enabled=false` short-circuits emission before the target is consulted.

### 3.3 Target-aware senders + the shared signal seam

The exporter choice lives in exactly one place — the seam every future signal reuses:

```csharp
// Core.Telemetry — the single "target → exporter" decision point
internal static class TelemetryExporters
{
    public static void AddTraceExporter(TracerProviderBuilder b, TelemetryTarget target)
    {
        if (target == TelemetryTarget.Local)
            b.AddOtlpExporter(o => o.Endpoint = WellKnownEndpoints.LocalOtlp);   // http://localhost:4317 → Jaeger
        else
            b.AddAzureMonitorTraceExporter(o => o.ConnectionString = WellKnownEndpoints.AzureMonitorConnectionString);
    }
    // Future: AddMetricExporter(MeterProviderBuilder, target), AddLogExporter(...) slot in here — nothing else moves.
}
```

Two call sites change; nothing else:

- **`DirectTelemetrySender.ConfigureExporter`** (PS / Agent, inline): replace the hardcoded
  `AddAzureMonitorTraceExporter` with `TelemetryExporters.AddTraceExporter(builder, TelemetryTargetResolver.Resolve())`.
- **`FileTelemetrySender.SendPendingStatic`** (CLI / TUI replay): the file-buffering half
  (`ConfigureExporter` → `FileExporter`) stays untouched; only the replay's exporter (currently the
  `AddAzureMonitorTraceExporter` block) routes through the seam. The detached `--upload-telemetry`
  process resolves the target itself — it is the same `#if DEBUG` binary and inherits
  `AZCLIPS_TELEMETRY_TARGET` from its parent process, so a CLI local replay lands in Jaeger.

Supporting bits:
- Add NuGet `OpenTelemetry.Exporter.OpenTelemetryProtocol` to `Core.Telemetry` (OTLP gRPC, `:4317`).
- Add `WellKnownEndpoints.LocalOtlp = "http://localhost:4317"` beside the existing remote connection string.
- The four telemetry providers (CLI / TUI / PS / Agent) are **not** touched — they already delegate to
  `sender.ConfigureExporter`.

### 3.4 The docker stack (single command)

Home: `tools/observability/` — a `docker-compose.yml` plus a short `README.md`. No wrapper scripts.

```yaml
# tools/observability/docker-compose.yml
services:
  jaeger:
    image: jaegertracing/all-in-one:1.76.0   # pinned; latest multi-arch stable (verified on Docker Hub)
    environment:
      - COLLECTOR_OTLP_ENABLED=true
    ports:
      - "16686:16686"   # Jaeger UI  → open in a browser, watch traces live
      - "4317:4317"     # OTLP gRPC  → the app exports here
      - "4318:4318"     # OTLP HTTP  → alternate ingest
    restart: unless-stopped
    healthcheck:
      test: ["CMD", "wget", "--no-verbose", "--tries=1", "--spider", "http://localhost:16686"]
      interval: 5s
      timeout: 3s
      retries: 5
      start_period: 10s
```

Single command (from repo root):

```
docker compose -f tools/observability/docker-compose.yml up -d
```

Then browse **http://localhost:16686**.

Deliberately minimal: no collector, no volumes (traces are ephemeral dev data; fine to lose on
`down`). The endpoint matches the `WellKnownEndpoints.LocalOtlp` constant. The healthcheck is kept
for now purely as a readiness signal (`docker compose ps` shows `healthy`); nothing gates on it since
the app runs on the host, not in compose. The dangling devcontainer Zipkin service stays untouched.

## 4. Testing

1. **Target resolution — the one logic-bearing unit.** Because `Resolve` is a pure function, the
   `#if DEBUG` default does not make it untestable. Unit tests in `tests/Core.Telemetry.Tests`:
   - env unset → returns `compileDefault` (covers both Debug→Local and Release→Remote via explicit inputs)
   - env `local` / `LOCAL` / `" local "` → Local (case- and whitespace-insensitive)
   - env `remote` → Remote
   - env garbage or empty → falls back to `compileDefault`, never throws
2. **Seam smoke test.** Building a `TracerProvider` through `TelemetryExporters.AddTraceExporter` for
   both Local and Remote completes without throwing (building the pipeline needs no network).
3. **Non-regression — proves the switch stays hidden.** Since no schema key and no env mapping are
   added, `ConfigurationSchemaTests`, the three `ListConfigurationAsync(includeDefaults:true)` tests,
   and `ConfigCliTests` must stay green unchanged. Running them is the evidence that `target` never surfaced.
4. **Manual validation (README checklist, not CI — no live Jaeger in CI):** `docker compose up` →
   run a Debug `az group list` → see the trace in Jaeger UI; then set `AZCLIPS_TELEMETRY_TARGET=remote`
   and confirm it does *not* land locally.

Gates: `dotnet build azclips.slnx` (warnings-as-errors) and
`dotnet test tests/Core.Telemetry.Tests/Core.Telemetry.Tests.csproj`.

## 5. Files touched / created

Created:
- `tools/observability/docker-compose.yml`
- `tools/observability/README.md`
- `src/Core/Core.Telemetry/TelemetryTarget.cs` (enum)
- `src/Core/Core.Telemetry/TelemetryTargetResolver.cs`
- `src/Core/Core.Telemetry/TelemetryExporters.cs` (the seam)
- Tests under `tests/Core.Telemetry.Tests/` for the resolver + seam smoke test

Modified:
- `src/Core/Core.Telemetry/DirectTelemetrySender.cs` (route through the seam)
- `src/Core/Core.Telemetry/FileTelemetrySender.cs` (replay routes through the seam)
- `src/Core/Core.Telemetry/WellKnownEndpoints.cs` (add `LocalOtlp`)
- `src/Core/Core.Telemetry/Core.Telemetry.csproj` (add OTLP exporter package)

Explicitly **not** modified: `TelemetryOptions.cs`, `ConfigurationSchema.cs`,
`EnvironmentVariableMappings.cs`, the four telemetry providers, the devcontainer compose.

## 6. Future scope (Phase ②, separate spec)

- A KQL/Kusto-emulation layer plus Grafana dashboards intended to be portable to production App Insights.
- Research already surfaced two blockers (no-auth Kusto ingestion bridge; Grafana ADX no-auth plugin gap)
  and a schema-mapping gap (OTEL traces vs App Insights `requests`/`dependencies`). When Phase ② lands,
  the natural insertion point is a collector between the app and Jaeger, fanning out to the Kusto emulator —
  which is exactly what the `TelemetryExporters` seam and the stable local OTLP endpoint are positioned for.

## 7. Decision log

- Topology: CLI/TUI keep file-buffer + replay (replay repointed to Jaeger); PS/Agent export to Jaeger
  inline. No collector in Phase ①.
- `Enabled` unchanged; new `TelemetryTarget { Local, Remote }`; no `Off`.
- Direction switch hidden: no schema key, no env mapping, `TelemetryOptions` untouched; override via a
  direct `AZCLIPS_TELEMETRY_TARGET` env read.
- Single shared `TelemetryExporters` seam for all signals; traces implemented now.
- Local-only when target is local (no dual-export to remote in Debug).
- Raw `docker-compose.yml`, no wrapper scripts; lives in `tools/observability/`.
- Healthcheck retained as a readiness signal only.
