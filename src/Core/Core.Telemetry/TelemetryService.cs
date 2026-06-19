// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Text;
using Core.Abstractions;

namespace Core.Telemetry;

public class TelemetryService : ITelemetryService
{
    private readonly ITelemetryProvider _telemetryProvider;
    private readonly ClientType _clientType;
    private bool _disposed;

    public TelemetryService(ITelemetryProvider telemetryProvider, ClientType clientType)
    {
        _telemetryProvider = telemetryProvider;
        _clientType = clientType;
    }

    public Activity? StartActivity(string name, ActivityKind kind = ActivityKind.Internal) =>
        _telemetryProvider.ActivitySource.StartActivity(name, kind)?.SetTag(TelemetryTags.EventName, name);

    public Activity? StartRootActivity()
    {
        var activity = StartActivity($"{_clientType.ToString()}.Activity.Root", ActivityKind.Server);
        if (activity is null) return null;
        activity.SetTag(TelemetryTags.Id, TelemetryPropertyResolver.Id);
        activity.SetTag(TelemetryTags.IsMicrosoftInternal, TelemetryPropertyResolver.IsMicrosoftInternal);
        activity.SetTag(TelemetryTags.ProductName, TelemetryPropertyResolver.ProductName);
        activity.SetTag(TelemetryTags.ProductVersion, TelemetryPropertyResolver.ProductVersion);
        activity.SetTag(TelemetryTags.SchemaVersion, TelemetryTags.Version);
        activity.SetTag(TelemetryTags.TelemetryVersion, TelemetryPropertyResolver.TelemetryVersion);
        activity.SetTag(TelemetryTags.ClientType,  _clientType.ToString());
        return activity;
    }

    public Activity? TagInvocation(CommandInvocation commandInvocation)
    {
        var rootActivity = GetRoot(Activity.Current);

        rootActivity?.SetTag(TelemetryTags.Command, commandInvocation.CommandName);
        if (!string.IsNullOrEmpty(commandInvocation.ModuleName))
            rootActivity?.SetTag(TelemetryTags.ModuleName, commandInvocation.ModuleName);
        if (!string.IsNullOrEmpty(commandInvocation.ModuleVersion))
            rootActivity?.SetTag(TelemetryTags.ModuleVersion, commandInvocation.ModuleVersion);
        rootActivity?.SetTag(TelemetryTags.Parameter, FormatParameters(commandInvocation.Parameters));

        return rootActivity;
    }

    private Activity? GetRoot(Activity? activity)
    {
        while (activity?.Parent is not null)
            activity = activity.Parent;

        return activity;
    }


    public Activity? AddTag(string propertyName, object? value, Activity? activity = null)
    {
        if (activity is not null)
        {
            activity.SetTag(propertyName, value);
            return activity;
        }
        var root = Activity.Current;
        while (root?.Parent is not null)
            root = root.Parent;

        root?.SetTag(propertyName, value);
        return root;
    }

    //todo: inject as dependency as paramformatter
    private static string FormatParameters(IDictionary<string, object>? parameters)
    {
        if (parameters is null || parameters.Count == 0) return string.Empty;

        const string MaskedValue = "***";
        var sb = new StringBuilder();
        foreach (var kvp in parameters)
        {
            if (string.IsNullOrEmpty(kvp.Key)) continue;
            if (sb.Length > 0) sb.Append(' ');
            sb.Append('-').Append(kvp.Key).Append(' ').Append(MaskedValue);
        }
        return sb.ToString();
    }

    public void Dispose()
    {
        if (_disposed) return;

        // Skip ForceFlush — FileExporter uses SimpleActivityExportProcessor
        // which exports synchronously inline, so there's nothing to flush.
        // TracerProvider.Dispose() also tries to flush; run it on a thread
        // with a short timeout to avoid blocking on unreachable exporters
        var disposeTask = Task.Run(() => _telemetryProvider.TracerProvider.Dispose());
        disposeTask.Wait(TimeSpan.FromMilliseconds(500));

        // ActivitySource lifetime is managed by the DI container; don't dispose it here.
        _disposed = true;

        GC.SuppressFinalize(this);
    }
}
