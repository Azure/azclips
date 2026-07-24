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
