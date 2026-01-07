using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using Core.Abstractions;

namespace Core.Telemetry;

/// <summary>
/// Proxy for TelemetryService that loads OpenTelemetry in an isolated AssemblyLoadContext
/// to avoid conflicts with PowerShell's built-in DiagnosticSource.
/// 
/// This proxy does NOT reference Core.Implementation or any OpenTelemetry packages directly,
/// allowing it to be safely registered in DI containers without triggering assembly loading conflicts.
/// </summary>
public class TelemetryServiceProxy : ITelemetryService
{
    private readonly object? _telemetryServiceInstance;
    private readonly MethodInfo? _startActivityMethod;
    private readonly MethodInfo? _flushMethod;
    private readonly PropertyInfo? _activitySourceProperty;
    private readonly TelemetryLoadContext? _loadContext;
    private bool _disposed;

    public ActivitySource ActivitySource { get; }

    public TelemetryServiceProxy(string serviceName = "AzureCLI", string serviceVersion = "1.0.0")
    {
        // Create a basic ActivitySource in the default context
        ActivitySource = new ActivitySource(serviceName, serviceVersion);

        try
        {
            // Get the path to Core.Telemetry.dll (where TelemetryService lives)
            var currentDir = Path.GetDirectoryName(typeof(TelemetryServiceProxy).Assembly.Location);
            
            if (string.IsNullOrEmpty(currentDir))
            {
                Console.WriteLine("Warning: Could not determine assembly location for telemetry initialization");
                return;
            }

            var telemetryPath = Path.Combine(currentDir, "Core.Telemetry.dll");
            
            if (!File.Exists(telemetryPath))
            {
                Console.WriteLine($"Warning: Core.Telemetry.dll not found at {telemetryPath}");
                return;
            }

            // Create isolated load context
            _loadContext = new TelemetryLoadContext(telemetryPath);

            // Load Core.Telemetry in the isolated context
            var isolatedAssembly = _loadContext.LoadFromAssemblyPath(telemetryPath);

            // Get the TelemetryService type from isolated context
            var telemetryServiceType = isolatedAssembly.GetType("Core.Telemetry.TelemetryService");
            
            if (telemetryServiceType == null)
            {
                Console.WriteLine("Warning: TelemetryService type not found in isolated context");
                return;
            }

            // Create instance using reflection
            _telemetryServiceInstance = Activator.CreateInstance(telemetryServiceType, serviceName, serviceVersion);

            if (_telemetryServiceInstance == null)
            {
                Console.WriteLine("Warning: Failed to create TelemetryService instance");
                return;
            }

            // Cache method/property info for performance
            _startActivityMethod = telemetryServiceType.GetMethod("StartActivity");
            _flushMethod = telemetryServiceType.GetMethod("Flush");
            _activitySourceProperty = telemetryServiceType.GetProperty("ActivitySource");

            if (_startActivityMethod == null || _flushMethod == null || _activitySourceProperty == null)
            {
                Console.WriteLine("Warning: Failed to get TelemetryService methods/properties");
                _telemetryServiceInstance = null;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Warning: Failed to initialize telemetry with isolated context: {ex.Message}");
            _telemetryServiceInstance = null;
            _loadContext = null;
        }
    }

    public Activity? StartActivity(string name, ActivityKind kind = ActivityKind.Internal)
    {
        if (_telemetryServiceInstance == null || _startActivityMethod == null)
        {
            // Fallback: use local ActivitySource without OpenTelemetry tracing
            return ActivitySource.StartActivity(name, kind);
        }

        try
        {
            // Call the isolated TelemetryService.StartActivity
            var activity = _startActivityMethod.Invoke(_telemetryServiceInstance, new object[] { name, kind }) as Activity;
            return activity;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Warning: Failed to start activity: {ex.Message}");
            return ActivitySource.StartActivity(name, kind);
        }
    }

    public void Flush()
    {
        if (_telemetryServiceInstance == null || _flushMethod == null)
        {
            return;
        }

        try
        {
            _flushMethod.Invoke(_telemetryServiceInstance, null);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Warning: Failed to flush telemetry: {ex.Message}");
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        try
        {
            Flush();

            if (_telemetryServiceInstance is IDisposable disposable)
            {
                disposable.Dispose();
            }

            ActivitySource.Dispose();

            _loadContext?.Unload();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Warning: Error during telemetry disposal: {ex.Message}");
        }
    }
}
