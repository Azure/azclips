# Telemetry

Telemetry of Cirrus is implemented using [OpenTelemetry](https://opentelemetry.io/). It collects traces of operations performed by Cirrus and exports them to local files, Azure Application Insights.

## Quick Start

### Usage in Code

Telemetry is collected using `ActivitySource` instances defined in `ITelemetryService`. Core libraries and extensions can obtain the `ITelemetryService` from dependency injection:

Starting activities:

```csharp
// Option 1: Use ActivitySource directly (common in services)
var telemetry = serviceProvider.GetService<ITelemetryService>();
using var activity = telemetry?.ActivitySource?.StartActivity("OperationName");
activity?.SetTag("custom.tag", "value");

//Option 2: Use TelemetryExtenstion to emit auto-disposed single activity withh tags (common in processors)
TelemetryService.Emit("OperationName", new Dictionary<string, string> { { "custom.tag", "value" } });

// Option 3: Use StartActivity method on the interface
var telemetry = serviceProvider.GetRequiredService<ITelemetryService>();
using var activity = telemetry.StartActivity("MyOperation");

// Option 4: CLI-specific activity with command parsing (extension method)
using var cliActivity = telemetry.StartCLIActivity(args);

// Option 5: PowerShell-specific activity (extension method)
using var psActivity = telemetry.StartPowerShellActivity(commandName, parameters);
```

Tagging activities
```csharp
// Option 1: Tag the root activity from the telemetry service
var telemetryService = serviceProvider.GetService<ITelemetryService>();
telemetryService.AddProperty("custom.tag", "value");

//Option 2: Tag an arbitrary activity using the telemetry service
var telemetryService = serviceProvider.GetService<ITelemetryService>();
var activity = telemetryService.ActivitySource.StartActivity("OperationName");
telemetryService.AddProperty("custom.tag", "value", activity);
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

| Tag                   | Source | Description                                 |
|-----------------------|--------|---------------------------------------------|
| `command`             | CLI/PS | Command path, e.g., `az group list`         |
| `parameter`           | CLI/PS | Parameters with values masked as `***`      |
| `IsMicrosoftInternal` | CLI/PS | Whether the command is run by internal user |
| `totalDuration`       | CLI/PS | Total time taken to run command end to end  |
| `productName`         | CLI/PS | Product running the command (Cirrus)        |
| `productVersion`      | CLI/PS | Product version number                      |
| `schemaVersion`       | CLI/PS | Schema version number                       |
| `telemetryVersion`    | CLI/PS | Telemetry version number                    |


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
| `service.version` | Service version (`1.0.0`) |

### Events (within Span)

| Field | Description |
|-------|-------------|
| `Name` | Event name |
| `Timestamp` | When the event occurred |
| `Attributes` | Key-value pairs for event details |

---

## Export

| Exporter | Configuration | Enabled |
|----------|---------------|---------|
| **File** | `%LOCALAPPDATA%\AzureCLI\traces\` | Always |
| **Azure Monitor** | App Insights connection string | Unless user Disabled |

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
  "Tags": { "command": "az group list", ... }
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

