# OpenTelemetry Tracing Architecture

## Overview

The project uses OpenTelemetry for distributed tracing and diagnostics. The implementation follows a clean architecture pattern with interface definitions in `Core.Abstractions`, implementations in `Core.Implementation`, and a proxy layer in `Core.TelemetryProxy` to handle dependency isolation.

## Architecture

### Core Components

1. **ITelemetryService** (`Core.Abstractions/ITelemetryService.cs`)
   - Interface for telemetry operations
   - Provides `ActivitySource` for creating activities
   - Defines methods for starting activities and flushing data

2. **TelemetryService** (`Core.Implementation/TelemetryService.cs`)
   - Implements `ITelemetryService`
   - Configures OpenTelemetry `TracerProvider`
   - Uses `FileExporter` for synchronous file output
   - Contains OpenTelemetry dependencies (DiagnosticSource 10.0)

3. **TelemetryServiceProxy** (`Core.TelemetryProxy/TelemetryServiceProxy.cs`)
   - Proxy implementation that loads `TelemetryService` in an isolated `AssemblyLoadContext`
   - Does NOT reference OpenTelemetry packages directly
   - Avoids dependency conflicts with PowerShell 7's built-in DiagnosticSource 4.0
   - Uses reflection to call isolated `TelemetryService` methods
   - Gracefully degrades to local `ActivitySource` if initialization fails

4. **TelemetryLoadContext** (`Core.TelemetryProxy/TelemetryLoadContext.cs`)
   - Isolated `AssemblyLoadContext` for loading OpenTelemetry dependencies
   - Isolates `OpenTelemetry`, `OpenTelemetry.Api`, and `DiagnosticSource` assemblies
   - Prevents version conflicts with host process (especially PowerShell)

5. **FileExporter** (`Core.Implementation/FileExporter.cs`)
   - Custom `BaseExporter<Activity>` implementation
   - Writes trace data synchronously to local JSON file
   - Thread-safe file operations with locking

### Registration

The service is registered in `Core.Engine/Bootstrapper.cs`:

```csharp
services.AddSingleton<ITelemetryService, TelemetryServiceProxy>();
```

**Why TelemetryServiceProxy?**
- PowerShell 7 includes `System.Diagnostics.DiagnosticSource` version 4.0
- OpenTelemetry requires `DiagnosticSource` version 10.0
- Direct registration of `TelemetryService` causes assembly loading conflicts in PowerShell
- `TelemetryServiceProxy` isolates OpenTelemetry dependencies in a separate context
- This allows both CLI and PowerShell to use the same `Bootstrapper` configuration

### Usage in Program.cs

Telemetry is initialized at the beginning of `Main`:

```csharp
_telemetryService = new TelemetryServiceProxy();

try
{
    using var activity = _telemetryService.StartActivity("AzureCLI.Main", ActivityKind.Internal);
    activity?.SetTag("args.count", args.Length);
    activity?.SetTag("args.command", args.Length > 0 ? args[0] : "none");
    
    // ... application logic ...
}
finally
{
    _telemetryService?.Dispose();
}
```

## Trace File Location

Traces are written to:
```
%LOCALAPPDATA%\AzureCLI\traces\traces-yyyyMMdd.log
```

Example on Windows:
```
C:\Users\<username>\AppData\Local\AzureCLI\traces\traces-20251229.log
```

## Trace Data Format

Each trace record includes:

- **TraceId**: Unique trace identifier
- **SpanId**: Current span identifier
- **ParentSpanId**: Parent span identifier (for nested operations)
- **OperationName**: Name of the operation
- **DisplayName**: Human-readable display name
- **StartTime**: Operation start time (UTC ISO 8601)
- **Duration**: Duration in milliseconds
- **Status**: Execution status (Ok/Error)
- **StatusDescription**: Optional status description
- **Kind**: Activity kind (Internal/Client/Server)
- **Tags**: Custom key-value metadata
- **Events**: List of events that occurred during the activity
- **Resource**: Service metadata (name, version)

## Example Output

```json
--- Batch at 2025-12-29T10:30:45.1234567Z ---
[
  {
    "TraceId": "4bf92f3577b34da6a3ce929d0e0e4736",
    "SpanId": "00f067aa0ba902b7",
    "ParentSpanId": "0000000000000000",
    "OperationName": "AzureCLI.Main",
    "DisplayName": "AzureCLI.Main",
    "StartTime": "2025-12-29T10:30:45.0000000Z",
    "Duration": 123.45,
    "Status": "Ok",
    "StatusDescription": null,
    "Kind": "Internal",
    "Tags": {
      "args.count": 2,
      "args.command": "group"
    },
    "Events": [],
    "Resource": {
      "service.name": "AzureCLI",
      "service.version": "1.0.0"
    }
  }
]
```

## Adding Custom Tracing

### In Extensions or Commands

Extensions and commands can inject `ITelemetryService` via DI:

```csharp
public class MyCommand : ICommand
{
    private readonly ITelemetryService _telemetry;

    public MyCommand(ITelemetryService telemetry)
    {
        _telemetry = telemetry;
    }

    public string Execute()
    {
        using var activity = _telemetry.StartActivity("MyCommand.Execute", ActivityKind.Internal);
        activity?.SetTag("custom.tag", "value");
        
        try
        {
            // Command logic
            activity?.SetStatus(ActivityStatusCode.Ok);
            return "Success";
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.AddException(ex);
            throw;
        }
    }
}
```

### Activity Helpers

Common patterns for working with activities:

```csharp
// Start an activity
using var activity = telemetryService.StartActivity("OperationName", ActivityKind.Internal);

// Add tags (metadata)
activity?.SetTag("key", "value");
activity?.SetTag("user.id", userId);

// Set status
activity?.SetStatus(ActivityStatusCode.Ok);
activity?.SetStatus(ActivityStatusCode.Error, "Error message");

// Record exceptions
activity?.AddException(exception);

// Add events
activity?.AddEvent(new ActivityEvent("EventName", 
    tags: new ActivityTagsCollection { { "key", "value" } }));
```

## Configuration

### Change Service Name or Version

Edit `TelemetryService.cs` constructor:

```csharp
public TelemetryService(string serviceName = "AzureCLI", string serviceVersion = "1.0.0")
```

### Change Log Location

Modify the log path calculation in `TelemetryService.cs`:

```csharp
var logDirectory = Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
    serviceName,
    "traces"
);
var logFilePath = Path.Combine(logDirectory, $"traces-{DateTime.UtcNow:yyyyMMdd}.log");
```

### Switch to OTLP Exporter

To export to an OTLP endpoint instead of a file:

```csharp
_tracerProvider = Sdk.CreateTracerProviderBuilder()
    .AddSource(serviceName)
    .ConfigureResource(resource =>
        resource.AddService(
            serviceName: serviceName,
            serviceVersion: serviceVersion))
    .AddOtlpExporter(options =>
    {
        options.Endpoint = new Uri("http://localhost:4317");
    })
    .Build()!;
```

## Design Principles

1. **Interface-Based**: `ITelemetryService` defined in `Core.Abstractions` allows for easy testing and alternative implementations

2. **Dependency Injection**: Service is registered in Bootstrapper and available throughout the application

3. **Isolated Loading**: OpenTelemetry dependencies are loaded in a separate `AssemblyLoadContext` to avoid version conflicts with PowerShell

4. **Graceful Degradation**: If telemetry initialization fails, the proxy falls back to a local `ActivitySource` without crashing the application

## Architecture Decisions

### Why Core.TelemetryProxy?

**Problem**: PowerShell 7 ships with `System.Diagnostics.DiagnosticSource` version 4.0, but OpenTelemetry 1.14.0 requires version 10.0. When `Core.Engine.Bootstrapper` registers `TelemetryService` directly, the DI container attempts to load the type metadata, which triggers loading of `Core.Implementation.dll` and all its dependencies including `DiagnosticSource 10.0`. This causes an assembly loading failure in PowerShell.

**Solution**: Create a separate `Core.TelemetryProxy` project that:
- Does NOT reference OpenTelemetry or DiagnosticSource packages
- Can be safely registered in the DI container without triggering dependency loading
- Uses `AssemblyLoadContext` to load `Core.Implementation.dll` in an isolated context at runtime
- Uses reflection to invoke `TelemetryService` methods across context boundaries

**Benefits**:
- ✅ PowerShell modules can load and initialize `Bootstrapper` successfully
- ✅ C# CLI continues to work with full OpenTelemetry tracing
- ✅ Same `Bootstrapper` configuration for both CLI and PowerShell
- ✅ Graceful degradation if telemetry fails to initialize

**Trade-offs**:
- ⚠️ Slight performance overhead from reflection calls (acceptable for tracing)
- ⚠️ Telemetry initialization warnings if isolated context loading fails (non-fatal)
- ⚠️ Additional project to maintain (`Core.TelemetryProxy`)

### Project Structure

```
src/Core/
├── Core.Abstractions/         # ITelemetryService interface
├── Core.TelemetryProxy/        # TelemetryServiceProxy (no OpenTelemetry refs)
│   ├── TelemetryServiceProxy.cs
│   └── TelemetryLoadContext.cs
└── Core.Implementation/        # TelemetryService (with OpenTelemetry refs)
    ├── TelemetryService.cs
    └── FileExporter.cs
```

### Dependency Graph

```
Core.Engine
├── Core.TelemetryProxy (safe for PowerShell)
│   └── Core.Abstractions
└── Core.Implementation (isolated at runtime)
    ├── Core.Abstractions
    ├── OpenTelemetry (v10.0)
    └── DiagnosticSource (v10.0)

PowerShell 7 (DiagnosticSource v4.0)
├── Loads Core.Engine successfully
└── TelemetryServiceProxy isolates OpenTelemetry at runtime
```

3. **Synchronous Export**: Uses `SimpleActivityExportProcessor` to ensure all traces are written immediately, not batched

4. **Thread-Safe**: `FileExporter` uses locking to ensure safe concurrent writes

5. **Disposable**: Proper resource cleanup via `IDisposable` pattern ensures all traces are flushed on application exit

6. **Internal Implementation**: `FileExporter` is internal; only `TelemetryService` is exposed as public implementation

## Dependencies

- `OpenTelemetry` (v1.14.0) - Core OpenTelemetry SDK
- `OpenTelemetry.Exporter.OpenTelemetryProtocol` (v1.14.0) - Optional, for OTLP export capability

## Notes

- Traces are appended to the daily log file, not overwritten
- On application exit, `Dispose()` is called to flush any pending traces
- The `SimpleActivityExportProcessor` ensures synchronous export without batching
- Error handling in `FileExporter` writes errors to stderr but doesn't crash the application
