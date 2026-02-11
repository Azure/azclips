# Telemetry

Telemetry of Cirrus is implemented using [OpenTelemetry](https://opentelemetry.io/). It collects traces of operations performed by Cirrus and exports them to local files, Azure Application Insights, and Zipkin (debug mode).

## Quick Start

### Usage in Code

Telemetry is collected using `ActivitySource` instances defined in `ITelemetryService`. Core libraries and extensions can obtain the `ITelemetryService` from dependency injection:

```csharp
// Option 1: Use ActivitySource directly (common in services)
var telemetry = serviceProvider.GetService<ITelemetryService>();
using var activity = telemetry?.ActivitySource?.StartActivity("OperationName");
activity?.SetTag("custom.tag", "value");

// Option 2: Use StartActivity method on the interface
var telemetry = serviceProvider.GetRequiredService<ITelemetryService>();
using var activity = telemetry.StartActivity("MyOperation");

// Option 3: CLI-specific activity with command parsing (extension method)
using var cliActivity = telemetry.StartCLIActivity(args);

// Option 4: PowerShell-specific activity (extension method)
using var psActivity = telemetry.StartPowerShellActivity(commandName, parameters);
```

---

## Architecture

### Components

| File | Description |
|------|-------------|
| `TelemetryService.cs` | Main service implementing `ITelemetryService` |
| `FileExporter.cs` | Custom exporter writing traces to local JSON files |
| `TelemetryExtensions.cs` | Helper methods for CLI-specific activities |

### Export Destinations

| Exporter | Configuration | Enabled |
|----------|---------------|---------|
| **File** | `%LOCALAPPDATA%\AzureCLI\traces\` | Always |
| **Azure Monitor** | App Insights connection string | Disabled* |
| **Zipkin** | `ZIPKIN_ENDPOINT` env var or `localhost:9411` | Debug only |

> *Azure Monitor exporter is temporarily disabled due to initialization performance impact.

### Trace Structure

```
AzureCLI.Main (root span)
├── command: "az group list"
├── parameters: "--subscription ***"
└── http.request (auto-instrumented)
    ├── http.method: "GET"
    ├── http.url: "https://management.azure.com/..."
    └── http.status_code: 200
```

---

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

| Tag | Source | Description |
|-----|--------|-------------|
| `command` | CLI/PS | Command path, e.g., `az group list` |
| `parameter` | CLI/PS | Parameters with values masked as `***` |

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

### Zipkin (Debug Build Only)

For development and testing purposes, telemetry can also be exported to a local Zipkin instance using the `ZipkinExporter`. This is useful for visualizing traces during development.

#### Using Zipkin in the Dev Container

If you're using the provided dev container, Zipkin is automatically started for you! Simply:

1. Open the repository in the dev container (VS Code will prompt you)

2. Build and run the application in DEBUG mode:
   ```bash
   dotnet build -c Debug cirrus.slnx
   ./artifacts/Debug/Core.Cli/az group create --Name mygroup --Location eastus --SubscriptionId <your-subscription-id>
   ```

3. Open Zipkin UI at http://localhost:9411

4. Click "Run Query" to see traces

The dev container automatically:
- Starts Zipkin in a separate container
- Configures the `ZIPKIN_ENDPOINT` environment variable
- Forwards port 9411 to your host machine

#### Manual Zipkin Setup

If you're not using the dev container, you can start Zipkin manually:

1. Start Zipkin using Docker:
   ```bash
   docker run -d -p 9411:9411 openzipkin/zipkin
   ```

2. Run the application in DEBUG mode:
   ```bash
   dotnet build -c Debug cirrus.slnx
   ./artifacts/Debug/Core.Cli/az group create --Name mygroup --Location eastus --SubscriptionId <your-subscription-id>
   ```

3. Open Zipkin UI at http://localhost:9411

4. Click "Run Query" to see traces

#### Custom Zipkin Endpoint

You can customize the Zipkin endpoint by setting the `ZIPKIN_ENDPOINT` environment variable:

```bash
export ZIPKIN_ENDPOINT=http://my-zipkin-server:9411/api/v2/spans
./artifacts/Debug/Core.Cli/az group create --Name mygroup --Location eastus
```

If not set, the default endpoint is `http://localhost:9411/api/v2/spans`.

---

## Dependencies

- [OpenTelemetry](https://opentelemetry.io/) - Distributed tracing framework
- [OpenTelemetry.Instrumentation.Http](https://www.nuget.org/packages/OpenTelemetry.Instrumentation.Http) - Auto-instrument HttpClient
- [Azure.Monitor.OpenTelemetry.Exporter](https://www.nuget.org/packages/Azure.Monitor.OpenTelemetry.Exporter) - App Insights export
- [OpenTelemetry.Exporter.Zipkin](https://www.nuget.org/packages/OpenTelemetry.Exporter.Zipkin) - Zipkin export (Debug only)

---

## Performance Considerations

- **Flush on Dispose**: `TelemetryService.Dispose()` calls `ForceFlush()` which can block CLI exit
- **Sync file writes**: `FileExporter` writes synchronously to avoid data loss
- **Azure Monitor disabled**: Initialization overhead (~seconds) impacts CLI startup time

