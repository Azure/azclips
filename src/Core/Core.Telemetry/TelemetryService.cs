// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using Core.Abstractions;

namespace Core.Telemetry;

public class TelemetryService : ITelemetryService
{
    private readonly ITelemetryProvider _telemetryProvider;
    private readonly IConfigurationService _configurationService;
    private readonly ClientType _clientType;
    private readonly KeyValuePair<string, object?>[] _commonActivityTags;
    private readonly TelemetryServiceSettings? _settings;
    private bool _disposed;
    private string ProductName => _settings?.ClientName ?? _clientType.ToString();

    public TelemetryService(
        ITelemetryProvider telemetryProvider,
        IConfigurationService configurationService,
        ClientMetadata clientMetadata)
        : this(telemetryProvider, configurationService, clientMetadata.ClientType, null)
    {
    }

    internal TelemetryService(
        ITelemetryProvider telemetryProvider,
        IConfigurationService configurationService,
        ClientType clientType,
        TelemetryServiceSettings? settings)
    {
        _telemetryProvider = telemetryProvider;
        _configurationService = configurationService;
        _clientType = clientType;
        _settings = settings;
        _commonActivityTags =
        [
            new(TelemetryTags.InstallationId, TelemetryPropertyResolver.InstallationId),
            new(TelemetryTags.ProductName, ProductName),
            new(TelemetryTags.ProductVersion, TelemetryPropertyResolver.ProductVersion),
            new(TelemetryTags.SchemaVersion, TelemetryTags.Version),
            new(TelemetryTags.TelemetryVersion, TelemetryPropertyResolver.TelemetryVersion),
            new(TelemetryTags.OperatingSystemType, TelemetryPropertyResolver.OperatingSystemType),
            new(TelemetryTags.OperatingSystemVersion, TelemetryPropertyResolver.OperatingSystemVersion),
            .. TelemetryPropertyResolver.DistributionName is { } distributionName
                ? new KeyValuePair<string, object?>[] { new(TelemetryTags.DistributionName, distributionName) }
                : Array.Empty<KeyValuePair<string, object?>>(),
            .. TelemetryPropertyResolver.DistributionId is { } distributionId
                ? new KeyValuePair<string, object?>[] { new(TelemetryTags.DistributionId, distributionId) }
                : Array.Empty<KeyValuePair<string, object?>>(),
            new(TelemetryTags.ShellType, telemetryProvider.ShellType ?? TelemetryPropertyResolver.ShellType),
            new(TelemetryTags.Locale, CultureInfo.CurrentCulture.Name),
            new(TelemetryTags.DotNetVersion, RuntimeInformation.FrameworkDescription),
            .. telemetryProvider.ActivityTags
        ];
    }

    // Read live on every call so Set-AzConfig / az config set take effect on
    // the very next cmdlet without requiring a module reload.
    // ConfigurationService.GetConfiguration<T>() re-parses ~/.azclips/config.json
    // per call — cheap enough for the interactive command rate we ship at.
    private bool IsTelemetryEnabled() =>
        (_settings is null || !_disposed) && (_settings?.Enabled ?? true) &&
        _configurationService.GetConfiguration<TelemetryOptions>().Enabled;

    public Activity? StartActivity(string name, ActivityKind kind = ActivityKind.Internal, DateTimeOffset startTime = default)
    {
        return StartCore(name, kind, startTime);
    }

    public Activity? StartRootActivity(DateTimeOffset startTime = default, string? name = null)
    {
        // No redundant IsTelemetryEnabled() here — StartActivity below is the single gate.
        return StartCore(name ?? $"{_clientType}.Activity.Root", ActivityKind.Server, startTime, initializeRoot: true);
    }

    public TelemetryActivityScope? StartActivityWithParent(
        string name,
        ActivityContext parentContext,
        ActivityKind kind = ActivityKind.Internal,
        DateTimeOffset startTime = default)
    {
        if (parentContext != default && (parentContext.TraceId == default || parentContext.SpanId == default))
            throw new ArgumentException("An explicit parent must have both a trace ID and span ID.", nameof(parentContext));

        if (parentContext == default)
            return StartIndependentRootActivity(name, kind, startTime);

        var previous = Activity.Current;
        var activity = StartCore(name, kind, startTime, parentContext);
        return activity is null ? null : new TelemetryActivityScope(activity, previous, restoreAmbient: true);
    }

    public TelemetryActivityScope? StartIndependentRootActivity(
        string name,
        ActivityKind kind = ActivityKind.Server,
        DateTimeOffset startTime = default,
        bool activate = true)
    {
        var previous = Activity.Current;
        var activity = StartCore(name, kind, startTime,
            independentRoot: true, initializeRoot: true, activate: activate);
        return activity is null ? null :
            new TelemetryActivityScope(activity, previous, restoreAmbient: true, activated: activate);
    }

    private Activity? StartCore(
        string name,
        ActivityKind kind,
        DateTimeOffset startTime,
        ActivityContext parent = default,
        bool independentRoot = false,
        bool initializeRoot = false,
        bool activate = true)
    {
        if (!IsTelemetryEnabled() || (_settings is not null && !_settings.NameFilter.IsAllowed(name)))
            return null;

        using var creation = _settings?.Lifecycle.TryBeginActivity();
        if (_settings is not null && creation is null)
            return null;

        var previous = Activity.Current;
        var completed = false;
        try
        {
            Activity? activity;
            if (independentRoot)
            {
                Activity.Current = null;
                activity = _telemetryProvider.ActivitySource.CreateActivity(
                    name, kind, parentId: string.Empty, tags: _commonActivityTags, idFormat: ActivityIdFormat.W3C);
                if (startTime != default)
                    activity?.SetStartTime(startTime.UtcDateTime);
                activity?.Start();
            }
            else
            {
                activity = _telemetryProvider.ActivitySource.StartActivity(name, kind, parent, tags: _commonActivityTags, startTime: startTime);
            }

            if (activity is not null)
            {
                activity.SetTag(TelemetryTags.EventName, name);
                if (initializeRoot)
                    InitializeRoot(activity);
            }
            _settings?.Lifecycle.OnActivityCreated(activity);
            completed = true;
            return activity;
        }
        finally
        {
            if (!activate || !completed || (independentRoot && Activity.Current is null))
                Activity.Current = previous;
        }
    }

    private void InitializeRoot(Activity activity)
    {
        activity.SetTag(TelemetryTags.InstallationId, TelemetryPropertyResolver.InstallationId);
        activity.SetTag(TelemetryTags.ProductName, ProductName);
        activity.SetTag(TelemetryTags.ProductVersion, TelemetryPropertyResolver.ProductVersion);
        activity.SetTag(TelemetryTags.SchemaVersion, TelemetryTags.Version);
        activity.SetTag(TelemetryTags.TelemetryVersion, TelemetryPropertyResolver.TelemetryVersion);
    }

    public Activity? TagInvocation(CommandInvocation commandInvocation)
    {
        var rootActivity = GetRoot(Activity.Current);

        rootActivity?.SetTag(TelemetryTags.CommandName, commandInvocation.CommandName);
        if (!string.IsNullOrEmpty(commandInvocation.ModuleName))
            rootActivity?.SetTag(TelemetryTags.CommandExtensionName, commandInvocation.ModuleName);
        if (!string.IsNullOrEmpty(commandInvocation.ModuleVersion))
            rootActivity?.SetTag(TelemetryTags.CommandExtensionVersion, commandInvocation.ModuleVersion);
        rootActivity?.SetTag(TelemetryTags.CommandParameter, FormatParameters(commandInvocation.Parameters));

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

        if (_settings is { OwnsProvider: false })
        {
            _disposed = true;
            GC.SuppressFinalize(this);
            return;
        }

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
