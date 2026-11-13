# Proposal

## Why

App Insights receives both product traces and benchmark summaries through separate SDK integrations. Remove both integrations while retaining Aria, regional routing, and local tracing.

## What Changes

- Remove runtime Azure Monitor trace export and its connection string.
- Remove benchmark App Insights export, its SDK, and upload-related status messages.
- Preserve Remote as Aria/OneCollector, including live per-activity and recorded EU/ROW routing.
- Preserve Local OTLP/Jaeger, instrumentation, privacy filtering, file buffering, replay, and benchmark charts.
- Preserve benchmark `--local`/`-l` compatibility and `--ci` output paths.
- Update related tests and documentation. Leave generated third-party notices to the existing official CG refresh workflow.
- Retain PowerShell's native indirect Microsoft.ApplicationInsights dependency, explicitly approved by the user; do not modify the third-party host.

## Capabilities

### New Capabilities

- `telemetry-export`: Supported product and benchmark export destinations and preservation of local artifacts.

### Modified Capabilities

None.

## Impact

Core.Telemetry, PowerShell Accounts package references, Benchmark, telemetry tests, and telemetry/benchmark documentation. Remove Azure.Monitor.OpenTelemetry.Exporter and the direct Microsoft.ApplicationInsights reference. The latter remains indirectly required by System.Management.Automation for PowerShell and Benchmark. Existing telemetry configuration and target selection remain unchanged.
