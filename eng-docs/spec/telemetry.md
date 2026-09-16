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

| Tag                                | Span                        | Description                                     |
|------------------------------------|-----------------------------|-------------------------------------------------|
| `properties.command.traceId`       | `CLI.Activity.AiRecovery`   | Trace id of the command that triggered recovery |
| `properties.command.spanId`        | `CLI.Activity.AiRecovery`   | Root span id of that command                    |
| `properties.recovery.sessionStyle` | recovery / `Agent.Session`  | `oneshot` or `interactive`                      |
| `properties.recovery.provider`     | recovery / `Agent.Session`  | `builtin` or `github`                           |
| `properties.recovery.exitCode`     | `CLI.Activity.AiRecovery`   | Agent process exit code                         |
| `properties.recovery.reprompts`   | `Agent.Turn`                | Tool-enforcement re-prompts on that turn        |
| `properties.agent.model`           | `Agent.Send`                | Model name                                      |
| `properties.process`               | `Agent.Session`             | Emitting process, e.g., `azclips-agent`         |
| `properties.recovery.dataBoundary` | `Agent.Session`             | `eu`, `global`, `unknown`, or `not_applicable` — the caller tenant's ARM data boundary. `not_applicable` means the lookup was skipped because the provider is not `builtin`; neither it nor `unknown` may be read as `global` |
| `properties.recovery.tokenElapsedMs` | `Agent.Session`           | Azure token acquisition, success or failure. One acquisition serves the model provider and the boundary lookup; absent for the `github` provider |
| `properties.recovery.dataBoundary.elapsedMs` | `Agent.DataBoundary` | The boundary lookup: profile read, ARM call, parse. Excludes the token — pair with `recovery.tokenElapsedMs` for the whole startup probe |
| `properties.recovery.dataBoundary.armElapsedMs` | `Agent.DataBoundary` | The ARM round trip, success or failure: request, status, and body on the wire. Excludes the local work that follows — decoding and parsing the response. Absent when the lookup returned before reaching ARM |
| `properties.recovery.dataBoundary.failureReason` | `Agent.DataBoundary` | Why the boundary is not resolved; absent otherwise. Fixed vocabulary, never a message — see below |

`failureReason` is closed: the emitting code takes a `DataBoundaryFailure` enum, so a value outside
this set cannot be written. The one exception is `arm_status_<code>`, where the suffix is an HTTP
status code.

| Value | Meaning |
| ----- | ------- |
| `provider_github` | Provider is `github`; it never calls ARM, so no lookup was attempted |
| `provider_unsupported` | Provider is neither `builtin` nor `github`. The tag is written before startup rejects the value, so this is a misconfiguration, not a session that ran |
| `timeout_reading_profile` / `profile_unavailable` / `no_active_tenant` | Never reached the token: the profile read timed out, failed, or named no tenant |
| `timeout_acquiring_token` / `token_unavailable` | Never reached ARM: no Azure token |
| `arm_status_<code>` | ARM answered with a non-success status, e.g. `arm_status_429` |
| `timeout` / `arm_request_failed` | The ARM call exceeded the lookup budget, or failed at the transport |
| `empty_response` / `malformed_response` / `missing_data_boundary` | ARM answered, but not with a readable boundary |
| `tenant_mismatch` | ARM answered for a different tenant than the one asked about |
| `unrecognized_value` | ARM reported a boundary this build does not know; the raw value is kept in `recovery.dataBoundary.rawValue` |

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

