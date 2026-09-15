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
the command fails; a second root covers the agent. Its canonical hierarchy path is
`Agent`; `CLI.Activity.AiRecovery` remains its legacy `OperationName` for compatible
queries. The agent's own spans nest under it via a W3C traceparent and source-recorded
hierarchy handoff passed on the child environment block.

| Tag                                | Legacy OperationName        | Description                                     |
|------------------------------------|-----------------------------|-------------------------------------------------|
| `properties.command.traceId`       | `CLI.Activity.AiRecovery`   | Trace id of the command that triggered recovery |
| `properties.command.spanId`        | `CLI.Activity.AiRecovery`   | Root span id of that command                    |
| `properties.recovery.sessionStyle` | recovery / `Agent.Session`  | `oneshot` or `interactive`                      |
| `properties.recovery.provider`     | recovery / `Agent.Session`  | `builtin` or `github`                           |
| `properties.recovery.exitCode`     | `CLI.Activity.AiRecovery`   | Agent process exit code                         |
| `properties.recovery.reprompts`   | `Agent.Reprompt`            | Tool-enforcement retry number on that turn     |
| `properties.agent.model`           | `Agent.Send`                | Model name                                      |
| `properties.process`               | `Agent.Session`             | Emitting process, e.g., `azclips-agent`         |

The link is one-directional. The command trace closes before the recovery trace exists, so
correlation goes recovery → command:

```kusto
requests
| where name == "CLI.Activity.AiRecovery"
| where customDimensions["properties.command.traceId"] == "<command operation_Id>"
```

The agent's `ActivitySource` is named `CLI`, matching the CLI's. `FileExporter` derives
`service.name` from `ActivitySource.Name`. `TraceUploader` groups by this name and can
remap exporter parent IDs only within the current upload batch. The source-recorded
identity below, not the Azure exporter's generated span ID, is the durable tree contract.

### Source-Recorded Hierarchy (version 1)

The five vetted properties are recorded when an activity starts, before child work:

| Tag | Type | Meaning |
|-----|------|---------|
| `properties.hierarchy.version` | integer | `1` only when this source's complete lineage is known |
| `properties.hierarchy.spanId` | string | Original W3C `Activity.SpanId`, stable across buffering and replay |
| `properties.hierarchy.parentSpanId` | string | Original parent span ID; empty string for a root, not an all-zero ID |
| `properties.hierarchy.depth` | integer | Recorded depth, with root `0` |
| `properties.hierarchy.path` | string | Canonical dot-separated full path; recovery trees have a single leading `Agent` namespace |

Identity is scoped by `TraceId` (`operation_Id` in Application Insights). Consumers join
`(operation_Id, properties.hierarchy.parentSpanId)` to
`(operation_Id, properties.hierarchy.spanId)` and use the recorded depth/path as the
primary hierarchy display, not the legacy operation names.
Repeated or concurrent calls may have identical names and paths, but have distinct span
IDs. Legacy `OperationName` values stay unchanged for compatible queries, metrics and
allowlists. Paths never use `DisplayName`, prompts, tool arguments, error text, or model
responses.

An ordinary recovery path is:

```text
Agent                                                   depth 0
  Agent.Session                                         depth 1
    Agent.Session.Create                                depth 2
      Agent.Session.Create.Tool.get_command              depth 3 (initialization)
    Agent.Session.Turn                                   depth 2
      Agent.Session.Turn.Send                            depth 3
        Agent.Session.Turn.Send.Tool.get_command          depth 4 (execution)
      Agent.Session.Turn.Reprompt                        depth 3
        Agent.Session.Turn.Reprompt.Send                 depth 4
          Agent.Session.Turn.Reprompt.Send.Tool.propose_plan  depth 5
```

The shared producer maps the legacy recovery root to `Agent`, appends `Session`,
`Create`, `Turn`, `Send`, or `Reprompt` for the corresponding agent child operations,
and appends `Tool.<operation>` for a legacy `tool.<operation>` span. Generic instrumented
children append their stable code operation name to the known parent path, for example
`Agent.Session.Turn.Send.Tool.get_command.MsalAuthService.AcquireTokenSilentlyAsync`.

**Depth counts actual parent-span edges, never dots.** `Tool.get_command` is one span,
not a `Tool` span plus a `get_command` span; the dotted generic code name above also adds
only one edge. No missing Session or Turn span is invented: a send directly under a
standalone session has path `Agent.Session.Send` and depth `1`. Repeated turns/tools
reuse canonical paths but retain unique source span IDs and their actual parent IDs.
Non-agent traces retain their own code-named roots rather than becoming recovery traces.

#### Producer and process handoff

[`TelemetryHierarchy.StartActivity/Record`](../../src/Core/Core.Abstractions/TelemetryHierarchy.cs)
are the shared producer helpers. They store immutable in-process parent metadata and
stamp the actual activity's identity; they do not reconstruct ancestors from exported
tags. [`TelemetryService.StartActivity`](../../src/Core/Core.Telemetry/TelemetryService.cs)
and [`AgentTracing.StartActivity`](../../Agent/Tools/AgentTracing.cs) use them.
[`SourceHierarchyProcessor.OnStart`](../../src/Core/Core.Telemetry/SourceHierarchyProcessor.cs)
also includes nested instrumentation whose operation names pass the product allowlist.
An unknown ancestor is not skipped or silently replaced.

[`Program.BuildAgentStartInfo`](../../src/CLI/Program.cs) passes
`AZCLIPS_RECOVERY_TRACEPARENT` plus `AZCLIPS_RECOVERY_HIERARCHY`. The latter is a bounded
JSON handoff containing `Version`, `TraceId`, `SpanId`, `Depth`, and `Path` from the recorded
recovery root (`Depth = 0`, `Path = "Agent"`). Both inherited environment values are
removed before setting this invocation's values, including when telemetry is disabled.
[`RecoveryTraceContext.StartSession`](../../Agent/Telemetry/RecoveryTraceContext.cs)
validates the version, root depth/path, and matching W3C identity together:

- A valid handoff makes `Agent.Session` a recorded child of the recovery root.
- A legacy traceparent without valid hierarchy metadata retains W3C correlation, but
  neither the session nor its descendants is labelled version 1.
- Missing/invalid traceparent starts an independent `Agent.Session` root at depth `0`. No recovery
  parent or ambient caller is invented. Invalid supplied values produce a diagnostic
  without echoing the handoff contents.

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
The existing telemetry opt-out remains in effect; no listener is created by these helpers.

#### Buffering, replay, and boundaries

[`FileExporter.Export`](../../src/Core/Core.Telemetry/FileExporter.cs) already buffers all
source tags. [`TraceReplayer.ReplayEntries`](../../src/Core/Core.Telemetry/TraceReplayer.cs)
restores them, including numeric version/depth values, without rewriting them with replay
IDs. Replayed `OperationName` and `DisplayName` remain separate. Exporter ID remapping is
scoped by trace and batch, but a child uploaded before its parent still retains its exact
source parent ID, depth, and path for a later join.

Missing parents remain explicitly missing until their records arrive. Consumers must not
infer edges from timestamps, durations, upload order, or matching names. Legacy buffers
are not upgraded or assigned fabricated version-1 paths. This contract covers instrumented
managed work and the explicit CLI-to-agent handoff; it does not synthesize spans for
uninstrumented SDK/native-daemon internals or subprocesses without a hierarchy handoff.


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
