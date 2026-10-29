// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using Microsoft.Azure.Azclips.Extensions.Resources.Models;

namespace Microsoft.Azure.Azclips.Extensions.Resources.Processors;

/// <summary>
/// Scope-neutral deployment debug log level, like <c>-DeploymentDebugLogLevel</c>: maps the level to the deployment's
/// <see cref="DebugSetting"/> and holds the warning shown when content logging is enabled.
/// </summary>
public static class DeploymentDebugSettings
{
    public const string Warning =
        "The deployment debug setting is enabled. The request or response content of the deployment operations, which can " +
        "include secrets such as passwords or listKeys results, can be read when you retrieve the deployment operations.";

    /// <summary>The debug setting for <paramref name="level"/>, or null for None or no level.</summary>
    public static DebugSetting? Create(string? level)
    {
        string? detailLevel = level?.Trim().ToLowerInvariant() switch
        {
            null or "" or "none" => null,
            "all" => "RequestContent,ResponseContent",
            "requestcontent" => "RequestContent",
            "responsecontent" => "ResponseContent",
            _ => throw AzclipsErrorException.InvalidInput($"DeploymentDebugLogLevel '{level}' is not valid. Use All, RequestContent, ResponseContent or None."),
        };
        return detailLevel is null ? null : new DebugSetting { DetailLevel = detailLevel };
    }
}
