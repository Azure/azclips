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

The SDK's error-type set is not enumerable — it is a required string passed through from
the service with no constants behind it — so the first three are simply the values this
repository has observed. **`sdk_other` is expected to be common, and that is the point:**
it measures how much of a vocabulary we do not control we have not yet seen, not how rare
errors are. Promote a value out of `sdk_other` by adding a member when the data shows it
is worth separating. A host failure exports `host_fault` and nothing finer: the runtime
class of the failure is unbounded, an implementation detail, and tells an operator nothing
that `host_fault` plus the printed error does not.

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
