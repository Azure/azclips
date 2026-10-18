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

The command span ends before AI recovery starts.
`CLI.Activity.AiRecovery` uses the saved command context as its parent and stays in CLI telemetry.
The Agent process creates `Agent.Session` under the recovery context.
`properties.recovery.invocationId` also correlates the CLI and Agent records.
Standalone Agent sessions without a recovery parent remain independent roots.
With `agent.enabled=false`, failed-command fallback instead emits a separate
`CLI.Activity.AiRecoverySkipped` root with `InstallationId` and the same optional,
safe command name and command trace/span links. It is not an executed recovery or
proof of explicit opt-out (default-off is included). Other fallback guards are
unchanged, and telemetry-disabled operation emits no skip span.

| Tag                                | Legacy OperationName        | Description                                     |
|------------------------------------|-----------------------------|-------------------------------------------------|
| `properties.command.traceId`       | `CLI.Activity.AiRecovery`   | Trace id of the command that triggered recovery |
| `properties.command.spanId`        | `CLI.Activity.AiRecovery`   | Root span id of that command                    |
| `properties.recovery.invocationId` | CLI acknowledgement / Agent layer | Correlates records from the same invocation |
| `schema` | Agent layer | Layer contract, currently `agent-v1`; distinct from common `schemaVersion` |
| `service.name` | Agent layer | `Agent`; also persisted in the Resource used by the uploader |
| `dataBoundary` | Agent layer | Captured route: EU, Global, Unknown, or an unrecognized value; only confirmed Global selects ROW |
| `properties.recovery.sessionStyle` | recovery / `Agent.Session` / `Agent.Create` | `oneshot` or `interactive`       |
| `properties.agent.provider`        | recovery / `Agent.Session` / `Agent.Create` | `builtin` or `github`            |
| `properties.recovery.exitCode`     | `CLI.Activity.AiRecovery`   | Agent process exit code                         |
| `properties.recovery.outcome`      | `Agent.Session`             | `ok`, `cancelled`, `error`, or `unknown` — `error` covers a terminal SDK failure on the session's last turn, not only an unhandled exception |
| `properties.recovery.failureReason` | `Agent.Session`             | Why it errored, from the closed list below. Classification only, never the message |
| `properties.recovery.skipReason`   | `CLI.Activity.AiRecoverySkipped` | `disabled`                                 |
| `properties.agent.reprompts`      | `Agent.Reprompt`            | Tool-enforcement retry number on that turn     |
| `properties.agent.model`           | recovery / `Agent.Session` / `Agent.Create` / `Agent.Send` | Configured, SDK-selected, or observed model |
| `properties.agent.modelSource`     | same as model               | `configuration`, `sdk`, `usage`, or `unavailable` |
| `properties.agent.tool.name`       | `tool.*`                    | Name of the tool the Agent called, for example `get_command` |
| `properties.agent.tool.request`    | `tool.*`                    | Redacted request. Each tool defines its own form: `get_command` records the help catalog name or `(not found)`; resource tools record only `kind=...`; `find_available_locations` records the SKU name; `propose_plan` records counts; `submit_response` records `status=...`. Never the raw tool input |
| `properties.agent.tool.response`   | `tool.*`                    | Short closed-set result: `found`, `not_found`, `ambiguous`, `ok`, or `error`. Never the tool output |
| `properties.agent.tokenElapsedMs` | `Agent.Session`           | Azure token acquisition, success or failure. One acquisition serves the model provider and the data boundary lookup; absent for any provider other than `builtin`, which is the only one needing an Azure token |

Agent layer records carry the `dataBoundary` the CLI handed over at launch; it fixes the
file's upload route. When the handover has no boundary, the builtin provider resolves it
through Core's `DataBoundaryResolver`, which reads the per-tenant cache and calls ARM only
on a miss, reusing the token acquired for the model. That lookup refreshes the cache for
later runs and does not change the route of the current file. It does not select the model
endpoint.

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
If no valid selection is available, any query failure — including failures outside the
experimental RPC's documented contract — malformed/empty results, or timeout use the
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

Session errors that arrive while no send is active are dropped, so a late error cannot
change a finished session's outcome. `error` on a session the user watched succeed is still
possible in one narrow case: an error queued during turn N-1 can arrive while turn N is
active and be attributed to it, because the SDK's error payload carries no turn identifier.

The link is one-directional. The command trace closes before the recovery trace exists, so
correlation goes recovery → command:

```kusto
requests
| where cloud_RoleName == "Agent"
| where name == "CLI.Activity.AiRecovery"
| where customDimensions["properties.command.traceId"] == "<command operation_Id>"
```

The Agent source is named `Agent`. Each layer owns its provider, processors, and file;
the source name and Resource service name must agree. Role is determined at upload from
the Resource, not merely from a span tag. New layer files are validated as homogeneous
before they are replayed.

### Native hierarchy and invocation correlation

Join `(operation_Id, operation_ParentId)` to `(operation_Id, id)`.
Include CLI acknowledgement records as well as Agent records when displaying the full tree.
Do not infer parentage from names or time containment.

```text
CLI.Activity.Root
  CLI.Activity.AiRecovery
    Agent.Session
      Agent.Create
      Agent.Send
        tool / hook callbacks
```

Nested callbacks use their immediate active parent. Standalone Agent sessions may be independent roots.

All application-owned activity creation goes through
[`TelemetryService`](../../src/Core/Core.Telemetry/TelemetryService.cs).
Its existing APIs retain ambient CLI/PS behavior; explicit-parent and independent-root
APIs make the new boundary intentional. An Agent Session becomes current in its scope;
a direct CLI tool's independent timing root restores the CLI ambient immediately.
The two new APIs return an owned `TelemetryActivityScope`; use its `Activity` for tags
and correlation and dispose the handle in the caller's flow. Root creation isolates
ambient state through sampling and Start, not only while allocating the activity. The
handle restores the caller after Stop, including when completion already stopped the
underlying activity. This is required for ordinary parent-based samplers, not just an
AlwaysOn-only test listener.
The retained CLI graph is the original graph with migrated Agent nodes removed and
their retained descendants attached to the nearest retained ancestor.

Only one live provider may own a layer source. Sequential tool/recovery scopes
first use the previous scope's bounded completion to wait for provider disposal.
If release is still unconfirmed, the factory immediately returns a diagnosed
non-recording scope rather than stacking another wait or attaching a second
listener. Direct concurrent contention is also refused immediately. This failure
does not become user opt-out or change the child Agent's actual telemetry consent.

[`TelemetryLayerEnvironment`](../../src/Core/Core.Abstractions/TelemetryLayerEnvironment.cs)
passes file/invocation IDs, routing, consent and the recovery W3C parent context.
It removes legacy traceparent/hierarchy variables.
CLI and Agent keep separate files; their shared invocation ID associates the files for coordinated replay.

#### Callback concurrency and lifetime

[`AgentInvocationContext`](../../Agent/Telemetry/AgentInvocationContext.cs) belongs to
one `ChatAgent`, not to a process-global mutable parent. Initialization and each send
publish their active operation through a scoped lease. Registered `AIFunction` wrappers
and the configured hooks/permission/event callbacks rebind `Activity.Current` when they
execute, even if the SDK retained the completed Create's `ExecutionContext`.
`DelegatingAIFunction` preserves tool metadata and schemas; `GetService<AIFunction>()`
resolves the wrapper rather than bypassing it.

Parallel tool callbacks have independent ambient scopes under the same send; independent
sessions keep separate holders. One session supports one in-flight send; an overlapping
send is rejected before its mutable turn state is changed. An async execution-context
boundary restores the previous ambient state even for synchronous callbacks retaining a
stopped Create (the `Activity.Current` setter cannot restore a stopped activity).
Cancellation leases are disposed in `finally`, and tools receive the send cancellation token
(linked with an incoming callback token when necessary). A per-holder async-local callback
snapshot lets in-flight nested work retain its original parent and cancellation token
even if a later send starts. Idle callbacks use the live session, never a
completed Create. Disposal rejects new operations and clears the holder after SDK cleanup.
The real callback execution, including hooks without an Activity yet, is registered
until its Task and finally have actually completed. Stopping the operation does not
prove that its callbacks have stopped. All creation APIs observe the live telemetry
opt-out and the launch consent.

#### Buffering, replay, and boundaries

New layer files remain `traces-{layer}-{guid}.partial` until their owner confirms
recording and file finalization. The caller-supplied child GUID does not authorize the
caller to rename the child's file. Success, handled failure, and cancellation all take
the same completion path; exit code zero is not a publication condition.

For these layers, Activity.Stop captures an immutable record and queues it without
waiting for disk IO. One instrumentation-suppressed writer drains accepted records
in order without inheriting the caller's ambient context. Admission is not
persistence: completion waits for actual writes, handle closure and writer idle
before sealing or disposing the provider. Pending-record memory grows with the
queued records; the shutdown budgets are not a memory cap. Legacy senders keep
their existing behavior.

Agent owns callback admission, cleanup and one 10-second shutdown drain budget.
This budget does not limit normal recovery execution.
After draining, Agent asks Core.Telemetry to complete accepted writes and publish the file within a separate 2-second budget.
If producers remain active at the deadline, Agent disallows publication.
Core retains the unpublished file; late work cannot restart publication.
An already admitted rename may finish later: `PublicationUnconfirmed` reports that distinction.
Timer setup consumes the original budget; the 1 ms correction tolerance is not a real-time guarantee.
Completion failures do not change the command's business result.

[`TraceReplayer`](../../src/Core/Core.Telemetry/TraceReplayer.cs) replays parents before children.
One upload run shares old-to-new SpanId mappings across service/profile groups, isolated by export target and data boundary.
The uploader holds related CLI and Agent files until the invocation is ready and retains the reserved set on export failure.
Managed records preserve unresolved nonzero parents and primitive tag types; legacy fallback behavior remains unchanged.
Retries may duplicate records; mappings are not persisted across upload runs.

Records preserve the captured boundary: EU, Global, Unknown, or an unrecognized value.
Only confirmed Global selects the Aria ROW collector; unconfirmed values select EU.
Standalone routing reads the existing cache without an extra authentication call.
The diagnostic startup probe remains separate from telemetry routing.
This does not change the fixed Azure Monitor destination or claim product-wide residency compliance.

The uploader retains invalid managed files as `.rejected` with local diagnostics.
Reservations are written to the same directory as the trace files they hold and track process identity for abandoned-owner recovery; uploader locks protect active claims.
A ready reservation whose files are still missing 5 minutes after its last write is deleted by the uploader; any of its files that do exist then upload on their own.
There is no partial-file garbage collection or exactly-once delivery guarantee.


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
| `service.name` | Owning telemetry layer (`CLI`, `PowerShell`, `TUI`, `Agent`) |

### Events (within Span)

| Field | Description |
|-------|-------------|
| `Name` | Event name |
| `Timestamp` | When the event occurred |
---

## Export

| Exporter | Configuration | Enabled |
|----------|---------------|---------|
| **File** | `HomePaths.GetDefaultTraceDirectory()` | CLI/TUI and opted-in layers |
| **Azure Monitor** | App Insights connection string | Unless user disabled |
| **OTLP (local)** | `http://localhost:4317`, port via `AZCLIPS_OTLP_PORT` | Debug builds only |

A Debug build exports to the local OTLP collector and Azure Monitor simultaneously; a Release build
exports to Azure Monitor only. See [`wiki/02-deep-dive/telemetry.md#export-targets`](../../wiki/02-deep-dive/telemetry.md#export-targets).

### Local File Export

Traces are saved to:
```
%LOCALAPPDATA%\.azclips\traces\traces-{traceId}.log
%LOCALAPPDATA%\.azclips\traces\traces-agent-{guid}.partial
```

`AZCLIPS_HOME` overrides the home root. Each file contains timestamp headers followed
by JSON arrays of trace records, not one JSON object or JSONL per file:
```json
[
  {
    "TraceId": "0123456789abcdef0123456789abcdef",
    "SpanId": "1111111111111111",
    "OperationName": "Agent.Session",
    "Duration": 1234.56,
    "Tags": [{"Key": "schema", "Value": "agent-v1"}]
  }
]
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

- **Layer completion**: Agent drains producers; Core completes accepted writes and publication. Borrowed telemetry views do not dispose the provider.
- **Sync file writes**: `FileExporter` writes synchronously to avoid data loss
- **Azure Monitor disabled**: Initialization overhead (~seconds) impacts CLI startup time
