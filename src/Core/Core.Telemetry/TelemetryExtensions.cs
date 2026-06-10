// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using Core.Abstractions;

namespace Core.Telemetry;

public static class TelemetryExtensions
{
    public static Activity? TagClientContext(this Activity? activity, ClientType clientType)
    {
        if (activity is null) return null;
        activity.SetTag(TelemetryTags.Id, TelemetryPropertyResolver.Id);
        activity.SetTag(TelemetryTags.IsMicrosoftInternal, TelemetryPropertyResolver.IsMicrosoftInternal);
        activity.SetTag(TelemetryTags.ProductName, TelemetryPropertyResolver.ProductName);
        activity.SetTag(TelemetryTags.ProductVersion, TelemetryPropertyResolver.ProductVersion);
        activity.SetTag(TelemetryTags.SchemaVersion, TelemetryTags.Version);
        activity.SetTag(TelemetryTags.TelemetryVersion, TelemetryPropertyResolver.TelemetryVersion);
        activity.SetTag(TelemetryTags.ClientType,  clientType.ToString());

        return activity;
    }

    public static Activity? TagInvocation(this Activity? activity, CommandInvocation invocation)
    {
        if (activity is null) return null;
        activity.SetTag(TelemetryTags.Command, invocation.CommandName);
        if (!string.IsNullOrEmpty(invocation.ModuleName))
            activity.SetTag(TelemetryTags.ModuleName, invocation.ModuleName);
        if (!string.IsNullOrEmpty(invocation.ModuleVersion))
            activity.SetTag(TelemetryTags.ModuleVersion, invocation.ModuleVersion);
        activity.SetTag(TelemetryTags.Parameter, FormatParameters(invocation.Parameters));

        return activity;
    }

    private static string FormatParameters(IDictionary<string, object>? parameters)
    {
        if (parameters is null || parameters.Count == 0) return string.Empty;

        const string MaskedValue = "***";
        var sb = new StringBuilder();
        foreach (var kvp in parameters)
        {
            if (sb.Length > 0) sb.Append(' ');
            sb.Append('-').Append(kvp.Key).Append(' ').Append(MaskedValue);
        }
        return sb.ToString();
    }
}
