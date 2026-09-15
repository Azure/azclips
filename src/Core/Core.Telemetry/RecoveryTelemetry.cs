// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Diagnostics;
using Core.Abstractions;

namespace Core.Telemetry;

public static class RecoveryTelemetry
{
    public const string ConfigurationSource = "configuration";

    public static string? GetConfiguredModel(AgentOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var model = options.Builtin?.ModelId;
        return string.Equals(options.Provider, "builtin", StringComparison.OrdinalIgnoreCase) &&
            !string.IsNullOrWhiteSpace(model) ? model : null;
    }

    public static void RecordConfiguration(Activity? activity, AgentOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        activity?.SetTag(TelemetryTags.RecoveryProvider, options.Provider);
        activity?.SetTag(TelemetryTags.RecoverySessionStyle, options.SessionStyle);
        RecordModel(activity, GetConfiguredModel(options), ConfigurationSource);
    }

    public static void RecordModel(Activity? activity, string? model, string source)
    {
        var recordedModel = string.IsNullOrWhiteSpace(model) ? null : model;
        activity?.SetTag(TelemetryTags.AgentModel, recordedModel);
        activity?.SetTag(TelemetryTags.AgentModelSource, recordedModel is null ? "unavailable" : source);
    }
}
