# Core.Telemetry

OpenTelemetry-based distributed tracing for Cirrus CLI/PowerShell commands.

## Overview

This module provides telemetry infrastructure for tracking command execution, HTTP requests, and performance metrics. Data is exported to local files, Azure Monitor (Application Insights), and Zipkin (debug mode).

## Quick Start

### Usage in Code

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

### View Local Traces

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

### View in Zipkin (Debug Mode)

1. Start Zipkin:
   ```bash
   docker run -d -p 9411:9411 openzipkin/zipkin
   ```

2. Run CLI in Debug build:
   ```bash
   dotnet run --project src/CLI/Core.Cli.csproj -- group list
   ```

3. Open http://localhost:9411 to view traces

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

## Configuration

### Environment Variables

| Variable | Description | Default |
|----------|-------------|---------|
| `ZIPKIN_ENDPOINT` | Zipkin collector URL | `http://localhost:9411/api/v2/spans` |

### App Insights Log
[Link to Simple Query](https://ms.portal.azure.com#@72f988bf-86f1-41af-91ab-2d7cd011db47/blade/Microsoft_OperationsManagementSuite_Workspace/Logs.ReactView/resourceId/%2Fsubscriptions%2F9e223dbe-3399-4e19-88eb-0975f02ac87f%2FresourceGroups%2Fnori-app-insights%2Fproviders%2Fmicrosoft.insights%2Fcomponents%2Fnori-test2/source/LogsBlade.AnalyticsShareLinkToQuery/q/H4sIAAAAAAAAAytKLSxNLS4p5qpRKEnMTlUwNAAA1mhqPhIAAAA%253D/timespan/P1D/limit/1000)

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

---

## See Also

- [Core.Abstractions](../Core.Abstractions/) - `ITelemetryService` interface
- [Benchmark Suite](../../../tests/Benchmark/) - Performance testing
- [Grafana Dashboard](https://ms.portal.azure.com#view/Microsoft_Azure_PinToGrafana/AzureGrafana.ReactView) - Live telemetry visualization
