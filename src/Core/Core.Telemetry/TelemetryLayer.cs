// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Frozen;
using Core.Abstractions;

namespace Core.Telemetry;

/// <summary>The immutable source, wire vocabulary and file identity of a telemetry layer.</summary>
public sealed class TelemetryLayer
{
    public TelemetryLayer(
        string serviceName,
        string fileToken,
        string schema,
        IEnumerable<string> activityNames,
        IEnumerable<string>? activityPrefixes = null,
        IEnumerable<string>? propertyNames = null,
        IEnumerable<string>? propertyPrefixes = null)
        : this(serviceName, fileToken, schema,
            new LayerNameFilter(activityNames, activityPrefixes), propertyNames, propertyPrefixes)
    {
    }

    public TelemetryLayer(
        string serviceName,
        string fileToken,
        string schema,
        ITelemetryActivityNameFilter nameFilter,
        IEnumerable<string>? propertyNames = null,
        IEnumerable<string>? propertyPrefixes = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(serviceName);
        ArgumentException.ThrowIfNullOrWhiteSpace(schema);
        ArgumentNullException.ThrowIfNull(nameFilter);
        if (serviceName.Any(c => !char.IsAsciiLetterOrDigit(c) && c is not ('.' or '-' or '_')) ||
            new[] { "CLI", "PowerShell", "TUI", "Default", "AzclipsTelemetryUploader" }
                .Contains(serviceName, StringComparer.OrdinalIgnoreCase))
            throw new ArgumentException("A layer requires its own canonical source name.", nameof(serviceName));
        ValidateFileToken(fileToken);

        ServiceName = serviceName;
        FileToken = fileToken;
        Schema = schema;
        NameFilter = nameFilter;
        PropertyFilter = new LayerPropertyFilter(propertyNames, propertyPrefixes);
    }

    public string ServiceName { get; }
    public string SourceName => ServiceName;
    public string FileToken { get; }
    public string Schema { get; }
    public ITelemetryActivityNameFilter NameFilter { get; }
    public ITelemetryPropertyFilter PropertyFilter { get; }

    internal static void ValidateFileToken(string fileToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileToken);
        if (fileToken.Length > 64 ||
            fileToken.Any(c => !char.IsAsciiLetterLower(c) && !char.IsAsciiDigit(c)))
            throw new ArgumentException("A file token must contain only lowercase ASCII letters or digits.", nameof(fileToken));
    }

    private sealed class LayerNameFilter : ITelemetryActivityNameFilter
    {
        private readonly FrozenSet<string> _names;
        private readonly string[] _prefixes;

        internal LayerNameFilter(IEnumerable<string> names, IEnumerable<string>? prefixes)
        {
            ArgumentNullException.ThrowIfNull(names);
            _names = names.ToFrozenSet(StringComparer.Ordinal);
            _prefixes = prefixes?.ToArray() ?? [];
            if (_names.Any(string.IsNullOrWhiteSpace) || _prefixes.Any(string.IsNullOrWhiteSpace))
                throw new ArgumentException("Activity names and prefixes must not be empty.");
        }

        public bool IsAllowed(string? activityName) =>
            !string.IsNullOrEmpty(activityName) &&
            (_names.Contains(activityName) || _prefixes.Any(prefix =>
                activityName.StartsWith(prefix, StringComparison.Ordinal)));
    }

    private sealed class LayerPropertyFilter : ITelemetryPropertyFilter
    {
        private static readonly DefaultTelemetryPropertyFilter WireFilter = new();
        private readonly FrozenSet<string>? _names;
        private readonly string[] _prefixes;

        internal LayerPropertyFilter(IEnumerable<string>? names, IEnumerable<string>? prefixes)
        {
            var declaredNames = names?.ToArray();
            _prefixes = prefixes?.ToArray() ?? [];
            if (declaredNames?.Any(name => !WireFilter.IsAllowed(name)) == true ||
                _prefixes.Any(prefix => string.IsNullOrEmpty(prefix) ||
                    !prefix.StartsWith(TelemetryTags.PropertyTagPrefix, StringComparison.OrdinalIgnoreCase)))
                throw new ArgumentException("Layer properties may only narrow the common telemetry wire vocabulary.");

            if (declaredNames is not null || prefixes is not null)
            {
                _names = (declaredNames ?? []).Concat(LayerTelemetryRecord.RequiredMetadataKeys)
                    .ToFrozenSet(StringComparer.OrdinalIgnoreCase);
            }
        }

        public bool IsAllowed(string? key) =>
            WireFilter.IsAllowed(key) &&
            (_names is null || _names.Contains(key!) || _prefixes.Any(prefix =>
                key!.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)));
    }
}

public static class TelemetryLayers
{
    public static TelemetryLayer Agent { get; } = new(
        "Agent", "agent", "agent-v1",
        [
            "Agent.Session", "Agent.Create", "Agent.Turn", "Agent.Send", "Agent.Reprompt",
            "CLI.Activity.AiRecovery", "CLI.Activity.AiRecoverySkipped",
            "SessionCreated", "AuthBeforeTokenAcquisition",
        ],
        ["tool.", "MsalAuthService.", "ManagedIdentityAuthenticator.", "ProfileService."]);

    public static IReadOnlyList<TelemetryLayer> All { get; } = Array.AsReadOnly(new[] { Agent });
}

public sealed class TelemetryLayerCatalog
{
    private readonly FrozenDictionary<string, TelemetryLayer> _sources;

    public TelemetryLayerCatalog(IEnumerable<TelemetryLayer>? layers = null)
    {
        var entries = (layers ?? TelemetryLayers.All).ToArray();
        if (entries.Select(layer => layer.FileToken).Distinct(StringComparer.Ordinal).Count() != entries.Length)
            throw new ArgumentException("Each layer must have a distinct file token.", nameof(layers));
        _sources = entries.ToFrozenDictionary(layer => layer.ServiceName, StringComparer.OrdinalIgnoreCase);
        Layers = Array.AsReadOnly(entries);
    }

    public IReadOnlyList<TelemetryLayer> Layers { get; }

    public TelemetryLayer Get(string serviceName) =>
        _sources.TryGetValue(serviceName, out var layer)
            ? layer
            : throw new ArgumentException("The telemetry layer is not registered.", nameof(serviceName));
}
