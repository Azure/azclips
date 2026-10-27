// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
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
        TelemetryTags.EventName,
        TelemetryTags.CommandOutputType,
        TelemetryTags.ExceptionType,
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
        TelemetryTags.CommandName,
        TelemetryTags.CommandExtensionName,
        TelemetryTags.CommandExtensionVersion,
        TelemetryTags.CommandParameter,
        TelemetryTags.SubscriptionId,
        TelemetryTags.TenantId,
        TelemetryTags.EntraObjectId,
        TelemetryTags.AuthType,
        TelemetryTags.AuthSuccess,
        TelemetryTags.AuthStatusCode,
        TelemetryTags.AuthDurationTotalMs,
        TelemetryTags.ClientRequestId,
        TelemetryTags.Mode,
        TelemetryTags.ImportKind,
        TelemetryTags.DataBoundary,

        // HTTP standard attributes — URL/path attributes are intentionally not allowed.
        "server.port",
        "http.request.method",
        "http.response.status_code",
        "network.protocol.version",

        // Exception messages and formatted stacktraces can contain private URLs and inner exceptions.
        "otel.status_code",
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

    internal void MergeAllowedTags(Activity activity, IDictionary<string, object?> destination)
    {
        var chain = new Stack<Activity>();
        for (var current = activity; current is not null; current = current.Parent)
            chain.Push(current);

        while (chain.TryPop(out var current))
        {
            foreach (var tag in current.TagObjects)
            {
                if (IsAllowed(tag.Key))
                    destination[tag.Key] = tag.Value;
            }
        }
    }
}
