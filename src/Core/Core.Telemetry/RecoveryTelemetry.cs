// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;
using Core.Abstractions;

namespace Core.Telemetry;

public static partial class RecoveryTelemetry
{
    // Model names reach this dimension from the remote runtime — an SDK selection, a usage event —
    // so they are bounded rather than whitelisted: the legitimate set is open, but a model id is
    // short and mundane. Anything else is recorded as unavailable. The one local source is the
    // configured builtin modelId, which the same bound rejects silently; real deployment names are
    // alphanumeric with - and _, so that is a theoretical loss rather than an expected one.
    //
    // \z rather than $: in .NET, $ also matches immediately before a single trailing newline, so
    // the anchor this bound is named for would have admitted "gpt-5\n" — and a 128-character name
    // plus a newline, which is 129 characters with a control character in it.
    [GeneratedRegex(@"^[A-Za-z0-9._:\-]{1,128}\z", RegexOptions.CultureInvariant)]
    private static partial Regex ModelNamePattern();

    /// <summary>
    /// Returns true when <paramref name="model"/> is within the character and length bounds
    /// that <c>agent.model</c> may export.
    /// </summary>
    public static bool IsExportableModel([NotNullWhen(true)] string? model) =>
        model is not null && ModelNamePattern().IsMatch(model);

    public static string? GetConfiguredModel(AgentOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var model = options.Builtin?.ModelId;
        return string.Equals(options.Provider, "builtin", StringComparison.OrdinalIgnoreCase) &&
            IsExportableModel(model) ? model : null;
    }

    public static void RecordConfiguration(Activity? activity, AgentOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        activity?.SetTag(TelemetryTags.RecoveryProvider, options.Provider);
        activity?.SetTag(TelemetryTags.RecoverySessionStyle, options.SessionStyle);
        RecordModel(activity, GetConfiguredModel(options), AgentModelSource.Configuration);
    }

    public static void RecordModel(Activity? activity, string? model, AgentModelSource source)
    {
        var recordedModel = string.IsNullOrWhiteSpace(model) ? null : model;
        activity?.SetTag(TelemetryTags.AgentModel, recordedModel);
        activity?.SetTag(TelemetryTags.AgentModelSource,
            (recordedModel is null ? AgentModelSource.Unavailable : source).Wire());
    }
}
