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

- **Debug builds** default to **both** Local and Remote (traces go to Jaeger on
  `localhost:4317` **and** Azure Monitor simultaneously).
- **Release builds** default to **Remote only** (Azure Monitor / App Insights).
- Override a Debug build with the environment variable `AZCLIPS_TELEMETRY_TARGET`,
  which accepts `local`, `remote`, or `both`. **Release builds ignore this variable
  entirely** — the suppression is deliberate and cannot be overridden at runtime.

This switch is intentionally **not** part of `az config` — it never appears in
`config get/list/set` or tab-completion. `telemetry.enabled` still controls
whether telemetry is emitted at all.

> CLI/TUI buffer spans to disk and replay them after each command exits, so local
> traces appear within the per-command replay cadence (near-real-time).
> PowerShell/Agent export inline (immediate).

## Overriding ports

Two host ports can be overridden if they're already in use:

| Variable | Default | What it changes |
|---|---|---|
| `AZCLIPS_OTLP_PORT` | `4317` | The gRPC port the app exports to **and** the stack publishes — one var drives both sides so they can't drift. |
| `AZCLIPS_JAEGER_UI_PORT` | `16686` | The host port the rebranded UI is served on. |

```bash
export AZCLIPS_OTLP_PORT=14317
export AZCLIPS_JAEGER_UI_PORT=26686
docker compose -f tools/observability/docker-compose.yml up -d
az group list                 # app exports to http://localhost:14317
# then open the UI at http://localhost:26686
```

Both default to their standard values and, like the telemetry switch, are
developer-only — they never appear in `az config`. The stack always talks to
`localhost`; the OTLP HTTP ingest port stays fixed at `4318`.

## Branding

The UI on `localhost:16686` is served through a small nginx reverse proxy (`ui`
service) that rebrands the stock Jaeger UI **without forking it**: it injects a
CSS overlay that swaps the logo and renames the top-left wordmark to **Azclips**.
Jaeger itself is unchanged and no longer publishes `16686` directly.

To change the logo, replace the SVG (or PNG) at:

```
tools/observability/branding/logo.svg
```

then restart the `ui` service (`docker compose ... up -d`). No rebuild needed —
the file is served live from the mounted `branding/` folder. `logo.svg` currently
holds the Azure logo; replace that file to use a different mark. To adjust the
wordmark or styling, edit the `sub_filter` rule in `nginx.conf`.

## Manual validation

1. `docker compose -f tools/observability/docker-compose.yml up -d`
2. Confirm readiness: `docker compose -f tools/observability/docker-compose.yml ps` shows `healthy`.
3. Run a Debug-build command, e.g. `az group list`.
4. Open http://localhost:16686, pick the service, and confirm the trace/spans appear.
5. Set `AZCLIPS_TELEMETRY_TARGET=remote`, re-run, and confirm the trace does **not** appear locally.
