# Design

## Context

See proposal.md for scope. Remote currently combines Azure Monitor and Aria, whereas Local uses OTLP. Direct PowerShell export and detached CLI replay share the exporter configuration. Benchmark has a separate App Insights SDK integration.

## Goals / Non-Goals

**Goals:** Remove both App Insights paths without changing telemetry collection, target selection, regional routing, or benchmark artifacts.

**Non-Goals:** Remove Aria, change telemetry defaults or opt-out, rewrite Agent instrumentation, delete CI benchmarking, or manually regenerate third-party notices.

## Decisions

- Configure each target in one shared method: Local registers OTLP; Remote registers Aria. Pass an optional recorded upload region through that method so replay retains fixed routing and live providers retain per-activity routing. Deleting Remote would also remove Aria and is rejected.
- Remove the Azure Monitor references from both Core.Telemetry and PowerShell Accounts, and the direct App Insights reference from Benchmark. Preserve other OpenTelemetry packages and PowerShell's native indirect App Insights dependency. Dependency inspection confirmed System.Management.Automation requires it; the user explicitly approved retaining this third-party dependency rather than risking the host.
- Remove the benchmark upload path and failure contribution. Retain `--local`/`-l` as compatibility aliases with an explicit no-effect message; preserve CI artifact selection. Reject removing existing accepted flags merely because upload is gone.
- Verify Remote's actual processor/exporter shape, not only that a provider builds. Preserve existing regional and replay tests.
- Official CG output remains the source of truth for NOTICE; its existing release refresh will remove obsolete SDK attribution.

## Risks / Trade-offs

- Dropping Remote accidentally disables Aria -> Keep its target and assert that its sole exporter is Aria.
- Replay loses recorded EU/ROW routing -> Forward the optional region and run routing/replay tests.
- Benchmark flags or outputs change -> Preserve compatibility flags, charts, telemetry files and CI paths; run discovery smoke checks.
- Existing generated NOTICE lists the removed Azure Monitor package -> Refresh via the official CG workflow, not hand-edited license blocks. Microsoft.ApplicationInsights attribution remains relevant to PowerShell's native dependency.
