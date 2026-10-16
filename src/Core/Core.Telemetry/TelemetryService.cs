// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Text;
using Core.Abstractions;

namespace Core.Telemetry;

public class TelemetryService : ITelemetryService
{
    private readonly ITelemetryProvider _telemetryProvider;
    private readonly IConfigurationService _configurationService;
    private readonly ClientType _clientType;
    private bool _disposed;

    public TelemetryService(
        ITelemetryProvider telemetryProvider,
        IConfigurationService configurationService,
        ClientMetadata clientMetadata)
    {
        _telemetryProvider = telemetryProvider;
        _configurationService = configurationService;
        _clientType = clientMetadata.ClientType;
    }

    // Read live on every call so Set-AzConfig / az config set take effect on
    // the very next cmdlet without requiring a module reload.
    // ConfigurationService.GetConfiguration<T>() re-parses ~/.azclips/config.json
    // per call — cheap enough for the interactive command rate we ship at.
    private bool IsTelemetryEnabled() =>
        _configurationService.GetConfiguration<TelemetryOptions>().Enabled;

    public Activity? StartActivity(string name, ActivityKind kind = ActivityKind.Internal, DateTimeOffset startTime = default)
    {
        if (!IsTelemetryEnabled()) return null;

        // parentContext: default keeps the ambient Activity.Current parenting the
        // two-argument overload gives us; only the start timestamp is being added.
        return _telemetryProvider.ActivitySource
            .StartActivity(name, kind, parentContext: default, tags: null, links: null, startTime: startTime)
            ?.SetTag(TelemetryTags.EventName, name);
    }

    public Activity? StartRootActivity(DateTimeOffset startTime = default, string? name = null)
    {
        // No redundant IsTelemetryEnabled() here — StartActivity below is the single gate.
        var activity = StartActivity(
            name ?? $"{_clientType.ToString()}.Activity.Root", ActivityKind.Server, startTime);
        if (activity is null) return null;
        activity.SetTag(TelemetryTags.InstallationId, TelemetryPropertyResolver.InstallationId);
        activity.SetTag(TelemetryTags.ProductName, TelemetryPropertyResolver.ProductName);
        activity.SetTag(TelemetryTags.ProductVersion, TelemetryPropertyResolver.ProductVersion);
        activity.SetTag(TelemetryTags.SchemaVersion, TelemetryTags.Version);
        activity.SetTag(TelemetryTags.TelemetryVersion, TelemetryPropertyResolver.TelemetryVersion);
        activity.SetTag(TelemetryTags.ClientType, _clientType.ToString());
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
