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

    /// <param name="effectiveSessionStyle">The style the session uses when a troubleshoot --style override applies.</param>
    public static void RecordConfiguration(Activity? activity, AgentOptions options, string? effectiveSessionStyle = null)
    {
        ArgumentNullException.ThrowIfNull(options);
        activity?.SetTag(TelemetryTags.RecoveryProvider, options.Provider);
        activity?.SetTag(TelemetryTags.RecoverySessionStyle, effectiveSessionStyle ?? (options.Recovery.IsInteractive ? "interactive" : "oneshot"));
        RecordModel(activity, GetConfiguredModel(options), AgentModelSource.Configuration);
    }

    public static void RecordModel(Activity? activity, string? model, AgentModelSource source)
    {
        var recordedModel = IsExportableModel(model) ? model : null;
        activity?.SetTag(TelemetryTags.AgentModel, recordedModel);
        activity?.SetTag(TelemetryTags.AgentModelSource,
            (recordedModel is null ? AgentModelSource.Unavailable : source).Wire());
    }

    public static void TagRecoveryContext(Activity? activity, TelemetryInvocationContext context)
    {
        // An explicit W3C parent carries IDs, not the stopped command's routing tags.
        activity?.SetTag(TelemetryTags.UploadRegion, context.UploadRegion.ToString());
        activity?.SetTag(TelemetryTags.TenantId, context.TenantId);
        activity?.SetTag(TelemetryTags.SubscriptionId, context.SubscriptionId);
        activity?.SetTag(TelemetryTags.CommandTraceId, context.CommandTraceId);
        activity?.SetTag(TelemetryTags.CommandSpanId, context.CommandSpanId);
        activity?.SetTag(TelemetryLayerTags.InvocationId, context.InvocationId);
        if (!string.IsNullOrEmpty(context.Command))
            activity?.SetTag(TelemetryTags.CommandName, context.Command);
    }
}
