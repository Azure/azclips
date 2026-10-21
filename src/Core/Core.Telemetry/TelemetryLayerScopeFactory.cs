// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;

namespace Core.Telemetry;

/// <summary>Creates isolated layer pipelines without replacing a host's default telemetry registration.</summary>
public sealed class TelemetryLayerScopeFactory : ITelemetryLayerScopeFactory
{
    private static readonly object SourcesGate = new();
    private static readonly Dictionary<string, object> LiveSources = new(StringComparer.OrdinalIgnoreCase);
    private readonly ITraceDirectoryProvider _traceDirectory;
    private readonly IConfigurationService _configuration;
    private readonly TelemetryLayerCatalog _catalog;
    private readonly TelemetryLayerScopeOptions _options;

    public TelemetryLayerScopeFactory(
        ITraceDirectoryProvider traceDirectory,
        IConfigurationService configuration,
        TelemetryLayerCatalog? catalog = null,
        TimeProvider? timeProvider = null)
        : this(traceDirectory, configuration, catalog,
            new TelemetryLayerScopeOptions { TimeProvider = timeProvider ?? TimeProvider.System })
    {
    }

    internal TelemetryLayerScopeFactory(
        ITraceDirectoryProvider traceDirectory,
        IConfigurationService configuration,
        TelemetryLayerCatalog? catalog,
        TelemetryLayerScopeOptions options)
    {
        _traceDirectory = traceDirectory ?? throw new ArgumentNullException(nameof(traceDirectory));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _catalog = catalog ?? new TelemetryLayerCatalog();
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    public LayerTelemetryScope CreateScope(
        string layer,
        TelemetryInvocationContext context,
        string? fileId = null)
    {
        ArgumentNullException.ThrowIfNull(context);
        var descriptor = _catalog.Get(layer);
        LayerTraceFileSession.ValidateId(context.InvocationId, nameof(context.InvocationId));
        var id = fileId ?? Guid.NewGuid().ToString("N");
        LayerTraceFileSession.ValidateId(id, nameof(fileId));
        var enabled = context.Enabled && _configuration.GetConfiguration<TelemetryOptions>().Enabled;
        object? owner = null;
        if (enabled)
        {
            lock (SourcesGate)
            {
                if (!LiveSources.ContainsKey(descriptor.SourceName))
                {
                    owner = new object();
                    LiveSources.Add(descriptor.SourceName, owner);
                }
            }
        }

        return new LayerTelemetryScope(
            descriptor, context, id, _traceDirectory, _configuration, _options,
            disabled: !enabled, unavailable: enabled && owner is null,
            releaseSource: owner is null ? null : () => Release(descriptor.SourceName, owner));
    }

    ITelemetryLayerScope ITelemetryLayerScopeFactory.CreateScope(
        string layer, TelemetryInvocationContext context, string? fileId) =>
        CreateScope(layer, context, fileId);

    private static void Release(string source, object owner)
    {
        lock (SourcesGate)
        {
            if (LiveSources.TryGetValue(source, out var registered) && ReferenceEquals(registered, owner))
                LiveSources.Remove(source);
        }
    }
}
