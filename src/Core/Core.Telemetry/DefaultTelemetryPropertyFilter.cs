// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Core.Abstractions;

namespace Core.Telemetry;

public sealed class DefaultTelemetryPropertyFilter : ITelemetryPropertyFilter
{
    private static readonly HashSet<string> AllowedKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        // Per-process invariants (TelemetryTags.*)
        TelemetryTags.InstallationId,        
        TelemetryTags.ProductName,
        TelemetryTags.ProductVersion,
        TelemetryTags.SchemaVersion,
        TelemetryTags.TelemetryVersion,
        TelemetryTags.ClientType,
        TelemetryTags.EventName,
        TelemetryTags.CommandOutputType,
        TelemetryTags.OperatingSystemType,
        TelemetryTags.OperatingSystemVersion,
        TelemetryTags.DistributionName,
        TelemetryTags.DistributionId,
        TelemetryTags.ShellType,
        TelemetryTags.Locale,
        TelemetryLayerTags.Schema,
        TelemetryLayerTags.ServiceName,
        TelemetryTags.DotNetVersion,
        TelemetryTags.PowerShellVersion,

        // Per-command tags
        TelemetryTags.Command,
        TelemetryTags.ModuleName,
        TelemetryTags.ModuleVersion,
        TelemetryTags.Parameter,
        TelemetryTags.SubscriptionId,
        TelemetryTags.TenantId,
        TelemetryTags.EntraObjectId,
        TelemetryTags.AuthType,
        TelemetryTags.CloudName,
        TelemetryTags.Mode,
        TelemetryTags.ImportKind,
        TelemetryTags.DataBoundary,

        // HTTP standard attributes — method/status/host are safe; URL/path are NOT allowed
        "http.method",
        "http.status_code",
        "http.scheme",
        "http.host",
        "http.request.header.user-agent",
        "http.request_content_length",
        "http.response_content_length",

        // OTel status / exception shape — type and stacktrace only, NOT message
        "otel.status_code",
        "exception.type",
        "exception.stacktrace",
    };

    /// <summary>
    /// Immutable snapshot of the verbatim allowed keys. The
    /// <see cref="TelemetryTags.PropertyTagPrefix"/> prefix is additionally
    /// honored by <see cref="IsAllowed"/> but is not enumerated here.
    /// </summary>
    public static IReadOnlyCollection<string> Allowlist => Array.AsReadOnly(AllowedKeys.ToArray());

    public bool IsAllowed(string? key)
    {
        if (string.IsNullOrEmpty(key)) return false;
        if (AllowedKeys.Contains(key)) return true;
        // Reserved per-product properties channel: anything under
        // TelemetryTags.PropertyTagPrefix is product-vetted and allowed.
        return key.StartsWith(TelemetryTags.PropertyTagPrefix, StringComparison.OrdinalIgnoreCase);
    }
}
