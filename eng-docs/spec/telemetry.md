# Telemetry

Telemetry of Azclips is implemented using [OpenTelemetry](https://opentelemetry.io/). It collects traces of operations and exports them to local files, Azure Application Insights.

## Glossary

| Term                     | Definition                                                                                                                                                                                                                                                                      |
|--------------------------|---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| **OpenTelemetry (OTel)** | A vendor-neutral, open-source observability framework (CNCF project) that provides APIs, SDKs, and tools for generating, collecting, and exporting telemetry data (traces, metrics, and logs) to backends such as Azure Application Insights, Jaeger, or Prometheus.            |
| **Trace**                | An end-to-end record of a single logical operation as it propagates through a system. A trace is a tree of spans that share the same trace ID and together represent the full lifecycle of a request or workflow.                                                               |
| **Span**                 | The fundamental building block of a trace in OpenTelemetry. A span represents one unit of work (e.g., a function call, an HTTP request, a database query) with a name, start/end timestamps, status, attributes, and parent/child relationships to other spans.                 |
| **Activity**             | The .NET equivalent of a span. The `System.Diagnostics.Activity` API is what OpenTelemetry for .NET builds on top of: starting an `Activity` creates a span, and the activity's tags become span attributes. In this codebase, "activity" and "span" refer to the same concept. |
| **Tag**                  | A key/value pair attached to a span/activity to provide structured context about the operation (e.g., `http.method = "GET"`, `command.name = "group list"`). Tags are queryable in the telemetry backend and are the primary mechanism for enriching spans with dimensions.     |
| **Activity/Span Root**   | The greatest ancestor span opened at the beginning of a command execution where the majority of tag enrichment lives in.                                                                                                                                                        |

## Quick Start

### Usage in `Core`

Starting/tagging activities:

```csharp
var telemetry = serviceProvider.GetService<ITelemetryService>();

using var activity = telemetry?.StartActivity("OperationName");

// Add tags to the "root" activity by default
telemetry.AddTag("custom.tag", "value");

// Add tags to a specific activity
telemetry.AddTag("custom.tag", "value", activity);
```

### Usage in `Extensions`
Starting/tagging activities:

Follows the event source pattern where `Processors` / `Orchestrators` raise events with relevant data.
```csharp
// **Processor.cs / **Orchestrator.cs
public async Task Foo()
{
    await Publisher.Publish(Events.ActionStart, new EventData()
    {
        ClassName = nameof(FooProcessor|FooOrchestrator),
        FunctionName = nameof(Foo),
    });
        .... business logic ...
    await Publisher.Publish(Events.ActionEnd);
}
```

## Telemetry Data Schema

### Span (Activity) Fields

| Field | Type | Description |
|-------|------|-------------|
| `TraceId` | string | Unique ID for the entire trace (shared across spans) |
| `SpanId` | string | Unique ID for this span |
| `ParentSpanId` | string | Parent span ID (empty for root span) |
| `OperationName` | string | Span name, e.g., `AzureCLI.Main`, `HTTP GET` |
| `DisplayName` | string | Human-readable display name |
| `StartTime` | ISO 8601 | UTC timestamp when span started |
| `Duration` | number | Duration in milliseconds |
| `Status` | string | `Unset`, `Ok`, or `Error` |
| `StatusDescription` | string | Error message if status is Error |
| `Kind` | string | `Server`, `Client`, `Internal`, etc. |

### Custom Tags (CLI/PS)

| Tag                   | Source | Description                                   |
|-----------------------|--------|-----------------------------------------------|
| `Id`                  | CLI/PS | Stable per-machine identifier (hashed)        |
| `clientType`          | CLI/PS | Client type (e.g., CLI, PowerShell)           |
| `module.name`         | CLI/PS | Extension loaded to run command e.g `compute` |
| `module.version`      | CLI/PS | Version of extension loaded                   |
| `command`             | CLI/PS | Command name only, e.g., `group list`         |
| `parameter`           | CLI/PS | Parameters with values masked as `***`        |
| `IsMicrosoftInternal` | CLI/PS | Whether the command is run by internal user   |
| `productName`         | CLI/PS | Product running the command                   |
| `productVersion`      | CLI/PS | Product version number                        |
| `schemaVersion`       | CLI/PS | Schema version number                         |
| `telemetryVersion`    | CLI/PS | Telemetry version number                      |
| `eventName`           | CLI/PS | Event name triggering span                    |

### AI Error-Recovery Tags

AI error recovery runs as a blocking subprocess, so it is recorded as its own trace
rather than inside the command's root span (Azure/CLIPS#602). The command trace ends when
the command fails; `CLI.Activity.AiRecovery` is a second root covering the agent, and the
agent's own spans nest under it via a W3C traceparent passed on the child environment
block.

With `agent.enabled=false`, failed-command fallback instead emits a separate
`CLI.Activity.AiRecoverySkipped` root with `InstallationId` and the same optional,
safe command name and command trace/span links. It is not an executed recovery or
proof of explicit opt-out (default-off is included). Other fallback guards are
unchanged, and telemetry-disabled operation emits no skip span.

| Tag                                | Span                        | Description                                     |
|------------------------------------|-----------------------------|-------------------------------------------------|
| `properties.command.traceId`       | `CLI.Activity.AiRecovery`   | Trace id of the command that triggered recovery |
| `properties.command.spanId`        | `CLI.Activity.AiRecovery`   | Root span id of that command                    |
| `properties.recovery.sessionStyle` | recovery / `Agent.Session` / `Agent.Create` | `oneshot` or `interactive`       |
| `properties.recovery.provider`     | recovery / `Agent.Session` / `Agent.Create` | `builtin` or `github`            |
| `properties.recovery.exitCode`     | `CLI.Activity.AiRecovery`   | Agent process exit code                         |
| `properties.recovery.outcome`      | `Agent.Session`             | `ok`, `cancelled`, `error`, or `unknown` — `error` covers a terminal SDK failure on the session's last turn, not only an unhandled exception |
| `properties.recovery.failureReason` | `Agent.Session`             | Why it errored, from the closed list below. Classification only, never the message |
| `properties.recovery.skipReason`   | `CLI.Activity.AiRecoverySkipped` | `disabled`                                 |
| `properties.recovery.reprompts`   | `Agent.Turn`                | Tool-enforcement re-prompts on that turn        |
| `properties.agent.model`           | recovery / `Agent.Session` / `Agent.Create` / `Agent.Send` | Configured, SDK-selected, or observed model |
| `properties.agent.modelSource`     | same as model               | `configuration`, `sdk`, `usage`, or `unavailable` |
| `properties.process`               | `Agent.Session`             | Emitting process, e.g., `azclips-agent`         |
| `properties.recovery.dataBoundary` | `Agent.Session`, `Agent.DataBoundary` | The caller tenant's ARM data boundary, or why there is none — vocabulary below |
| `properties.recovery.tokenElapsedMs` | `Agent.Session`           | Azure token acquisition, success or failure. One acquisition serves the model provider and the boundary lookup; absent for any provider other than `builtin`, which is the only one needing an Azure token |
| `properties.recovery.dataBoundary.elapsedMs` | `Agent.DataBoundary` | The boundary lookup: profile read, ARM call, parse. Excludes the token — pair with `recovery.tokenElapsedMs` for the whole startup probe |

`recovery.dataBoundary` records one diagnostic value: ARM's answer or why it is unavailable.
Startup writes it on `Agent.Session`; a lookup also writes it on `Agent.DataBoundary`.
It does not select the model endpoint.

ARM's answer passes through, lowercased: `eu`, `global`, or any value ARM adds later. It is the only
untrusted string in this dimension, so it is the only one sanitised — it must match
`^[a-z0-9_-]{1,32}$` or it is recorded as `unexpected_value`. That is a check on shape, not on
recognition: an unfamiliar but well-shaped value is reported as itself, because discovering one is
the point of the probe. Every other value is closed by the compiler — the emitting code takes a
`DataBoundaryUnavailable` enum, so a message or a runtime failure class cannot be written here. The one
exception is `arm_status_<code>`, where the suffix is an HTTP status code.

**None of the no-answer values is a boundary**, and none of them may be read as `global`.

| Value | Meaning |
| ----- | ------- |
| `eu`, `global`, or another lowercase token | ARM's answer for the caller's tenant |
| `skipped_github` | Provider is `github`; it never calls ARM, so no lookup was attempted |
| `skipped_unsupported_provider` | Provider is neither `builtin` nor `github`. The boundary is recorded before startup rejects the value, so this is a misconfiguration, not a session that ran |
| `no_tenant` | Nothing to ask ARM about: signed out, or the profile names no tenant |
| `profile_unavailable` | The profile could not be read at all — a storage or permissions problem, not a signed-out user |
| `timeout_profile` | The profile read exceeded the lookup budget |
| `no_token` / `timeout_token` | Token acquisition ran and failed, or exceeded its budget |
| `arm_status_<code>` | ARM answered with a non-success status, e.g. `arm_status_429` |
| `timeout_arm` / `arm_failed` | The ARM call exceeded the lookup budget, or failed at the transport |
| `unreadable` | ARM answered with success but no usable boundary: empty, malformed, or absent |
| `tenant_mismatch` | ARM echoed a different tenant than the one asked about |
| `tenant_unconfirmed` | ARM echoed no tenant at all, so the answer proves nothing about whose boundary it is. Distinct from `tenant_mismatch`, which is ARM answering about someone else |
| `cancelled` | The caller's cancellation token was signalled — Ctrl+C during startup |
| `unexpected_value` | ARM's value failed the shape check above |
| `lookup_faulted` | The lookup threw despite contracting never to — our bug, not ARM's. The token was acquired and recovery continues; only the boundary is lost |
| `not_started` | Unreachable by design: the startup seam never throws, so an outcome is always classified. Seeing this means that contract broke |

`Agent.Session` records `ok` / `cancelled` / `error` before it stops and before provider
disposal, with statuses `Ok` / `Unset` / `Error`. Cancellation still returns 130;
CLI-side cancellation counts from `properties.recovery.exitCode` were already available.
The value is written once, in a `finally`, so every exit records one. `unknown` is the
pessimistic starting value and is not reachable from any exit path today — seeing it in
the data means an exit was added that classifies nothing.

#### `properties.recovery.failureReason` values

A closed list (`RecoveryFailure` in `Agent/Telemetry/RecoveryOutcome.cs`). The tag is
absent unless `properties.recovery.outcome` is `error`.

| Value            | Meaning                                                             |
|------------------|---------------------------------------------------------------------|
| `rate_limit`     | SDK error type, on the observed-values allowlist                    |
| `authentication` | SDK error type, on the observed-values allowlist                    |
| `sdk_other`      | The SDK reported an error type that is not on the allowlist         |
| `host_fault`     | The failure was ours rather than the model provider's               |
| `unknown`        | The SDK reported a terminal failure carrying no error type          |

Only allowlisted SDK error types have dedicated values; other types map to
`sdk_other`, which may be common. Host failures map to `host_fault`.
Add a new value only when telemetry shows a useful distinction.

`error` on a session the user watched succeed is possible in one narrow case. The SDK
delivers events on its own thread, which is not synchronised with the host's turn
boundaries, so a session error queued during turn N-1 can arrive after turn N cleared it
and be attributed to the wrong turn. The host reads and writes that state through
`Volatile`, which guarantees it observes the write at all; it cannot fix the attribution,
because the SDK's error payload carries no turn identifier to correlate against.

`modelSource` is a closed set: the four values are the wire forms of `AgentModelSource`,
mapped by an exhaustive `Wire()`, so a mistyped literal can no longer create a fifth value. A test pins the set itself, so a new member cannot ship without being
documented here. `agent.model` is bounded before export — 1–128 characters of
`A-Z a-z 0-9 . _ : -` — because the value arrives from the remote runtime; a name outside
those bounds is recorded as `unavailable` rather than exported.

For `builtin`, the effective `agent.builtin.modelId` comes from configuration,
falling back to the `AgentOptions` default when the key is absent. It is recorded
on the recovery root before launching the child, and on `Agent.Session` and
`Agent.Create` before startup work. Authentication failure or cancellation before
the first send therefore need not lose a known configured model.

For `github`, the SDK still chooses the model. Session start/resume/model-change
events track its selection. If no selection event has arrived, startup performs
one read-only `session.model.getCurrent` query with a five-second deadline.
If no valid selection is available, any query failure — including a runtime failure the
experimental RPC was not expected to raise — malformed/empty results, or timeout use the
existing warning channel and leave an unavailable model; they do not prevent recovery.
Caller cancellation still propagates. Delayed startup
events cannot overwrite a queried selection, while an explicit model-change
event wins over an in-flight query reply. No builtin default is substituted for
a GitHub model.

Some runtimes select their default model lazily: both `session.start` and
`getCurrent` can legitimately have no model until the first send triggers
`session.model_change`. Selecting the GitHub provider alone does not identify a
model in that case. Keep startup metadata unavailable rather than guessing from
the available-model list or issuing an extra LLM request to force selection.

Each GitHub `Agent.Send` records the last non-empty main-agent model reported by
`assistant.usage` before that send ends, or the SDK selection if no usage arrived.
Main-agent usage accepts an absent/empty initiator or the runtime's explicit
`initiator=user`; an event with `agentId` still belongs to a sub-agent and is ignored.
Other auxiliary usage is ignored, and usage observations are not cached as the
selection for the next send. Collection is gated by the serialized main-agent
turn-start/turn-end events, so usage arriving before the next turn or after its
end does not overwrite its model. Missing SDK event data is ignored safely.
`Agent.Create` and the session retain available startup
metadata even when there is no send. `modelSource` distinguishes a configured or
selected model from a model observed in usage; none proves that an absent send ran.

Dashboards should prefer named `Agent.Send` models, falling back to `Agent.Session`,
then `Agent.Create`, then the recovery root only when higher-priority metadata is
missing. Do not infer historical models from today's configuration. Telemetry
opt-out, termination before spans stop, and storage/upload failures can still
prevent delivery; model metadata does not override those boundaries.

The link is one-directional. The command trace closes before the recovery trace exists, so
correlation goes recovery → command:

```kusto
requests
| where name == "CLI.Activity.AiRecovery"
| where customDimensions["properties.command.traceId"] == "<command operation_Id>"
```

The agent's `ActivitySource` is named `CLI`, matching the CLI's. `FileExporter` derives
`service.name` from `ActivitySource.Name` and `TraceUploader` rebuilds parent/child edges
only within a service-name group, so any other name would detach the agent's spans from
their parent on upload.


### HTTP Tags (Auto-instrumented)

| Tag | Description |
|-----|-------------|
| `http.method` | HTTP method (`GET`, `PUT`, `POST`, etc.) |
| `http.url` | Full request URL |
| `http.status_code` | Response status code (200, 404, etc.) |
| `http.request.header.user-agent` | User-Agent header value |
| `http.response_content_length` | Response body size |

### Resource Tags

| Tag | Description |
|-----|-------------|
| `service.name` | Service name (`AzureCLI`) |

### Events (within Span)

| Field | Description |
|-------|-------------|
| `Name` | Event name |
| `Timestamp` | When the event occurred |
---

## Export

| Exporter | Configuration | Enabled |
|----------|---------------|---------|
| **File** | `%LOCALAPPDATA%\AzureCLI\traces\` | Always (CLI and TUI) |
| **Azure Monitor** | App Insights connection string | Unless user disabled |
| **OTLP (local)** | `http://localhost:4317`, port via `AZCLIPS_OTLP_PORT` | Debug builds only |

A Debug build exports to the local OTLP collector and Azure Monitor simultaneously; a Release build
exports to Azure Monitor only. See [`wiki/02-deep-dive/telemetry.md#export-targets`](../../wiki/02-deep-dive/telemetry.md#export-targets).

### Local File Export

Traces are saved to:
```
%LOCALAPPDATA%\AzureCLI\traces\traces-{traceId}.log
```

Each file contains JSON-formatted trace data:
```json
{
  "TraceId": "abc123...",
  "SpanId": "def456...",
  "OperationName": "AzureCLI.Main",
  "Duration": 1234.56,
  "Tags": { "command": "group list", ... }
}
```

### Azure Application Insights

Telemetry is exported to Azure Application Insights using the `AzureMonitorTraceExporter`.

For now the connection string is hardcoded to a specific Application Insights resource. In the future, this can be made configurable.

To visit the Application Insights resource, go to: [nori-test2](https://ms.portal.azure.com/#@microsoft.onmicrosoft.com/resource/subscriptions/9e223dbe-3399-4e19-88eb-0975f02ac87f/resourceGroups/nori-app-insights/providers/microsoft.insights/components/nori-test2/overview)

[Link to Simple Query](https://ms.portal.azure.com#@72f988bf-86f1-41af-91ab-2d7cd011db47/blade/Microsoft_OperationsManagementSuite_Workspace/Logs.ReactView/resourceId/%2Fsubscriptions%2F9e223dbe-3399-4e19-88eb-0975f02ac87f%2FresourceGroups%2Fnori-app-insights%2Fproviders%2Fmicrosoft.insights%2Fcomponents%2Fnori-test2/source/LogsBlade.AnalyticsShareLinkToQuery/q/H4sIAAAAAAAAAytKLSxNLS4p5qpRKEnMTlUwNAAA1mhqPhIAAAA%253D/timespan/P1D/limit/1000)

## Dependencies

- [OpenTelemetry](https://opentelemetry.io/) - Distributed tracing framework
- [OpenTelemetry.Instrumentation.Http](https://www.nuget.org/packages/OpenTelemetry.Instrumentation.Http) - Auto-instrument HttpClient
- [Azure.Monitor.OpenTelemetry.Exporter](https://www.nuget.org/packages/Azure.Monitor.OpenTelemetry.Exporter) - App Insights export

---

## Performance Considerations

- **Flush on Dispose**: `TelemetryService.Dispose()` calls `ForceFlush()` which can block CLI exit
- **Sync file writes**: `FileExporter` writes synchronously to avoid data loss
- **Azure Monitor disabled**: Initialization overhead (~seconds) impacts CLI startup time
