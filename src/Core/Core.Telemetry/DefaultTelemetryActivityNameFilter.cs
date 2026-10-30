// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Core.Abstractions;

namespace Core.Telemetry;

public sealed class DefaultTelemetryActivityNameFilter : ITelemetryActivityNameFilter
{
    private static readonly HashSet<string> AllowedExactNames = new(StringComparer.Ordinal)
    {
        // Root activities — one per ClientType, written by TelemetryService.StartRootActivity
        // as $"{ClientType}.Activity.Root".
        "CLI.Activity.Root",
        "PowerShell.Activity.Root",
        "TUI.Activity.Root",

        // Recovery span, parented to the command root after that root ends, so command
        // duration excludes recovery.
        "CLI.Activity.AiRecovery",

        // Disabled recovery is observable without inflating the execution count above.
        "CLI.Activity.AiRecoverySkipped",

        // Startup phases, replayed by the CLI's PerformanceTimeline once telemetry
        // is available. Encloses the AzureCLI.Startup.* phase spans below.
        "AzureCLI.Startup",
        "AzureCLI.ParseGlobalArgs",
        "AzureCLI.HandleGlobalArgs",
        "AzureCLI.ParseCommandInvocation",
        "AzureCLI.ProcessCommand",
        "AzureCLI.DispatchVersion",
        "AzureCLI.DispatchHelp",
        // PowerShell module import phases, replayed by ImportTelemetry on a background
        // thread once the telemetry stack is built. Encloses PowerShell.Startup.*.
        "PowerShell.Startup",

        // Engine event-driven activities. Engine.Subscriptions wraps these
        // Events.* ids directly (i.e. evt.Id without any class/method suffix).
        // The corresponding *End/*After events Dispose the existing activity,
        // they do not create a new one — that's why only the Start side appears.
        "SessionCreated",
        "AuthBeforeTokenAcquisition",
        "ProcessorProcessAsyncStart",

        // HttpClient auto-instrumentation. URL/path tags are removed independently by the
        // property allowlist before this span reaches either exporter.
        "System.Net.Http.HttpRequestOut",
    };

    private static readonly string[] AllowedNamePrefixes =
    {
        // Service classes that use $"{nameof(Service)}.{nameof(Method)}" naming.
        // The activity *name* never carries secrets — tokens / account IDs land
        // in tags, which the property filter scrubs independently.
        "MsalAuthService.",
        "ManagedIdentityAuthenticator.",
        "ProfileService.",

        // Extension processors (Add/Remove/Update/Show/List).
        "AddExtensionProcessor.",
        "RemoveExtensionProcessor.",
        "UpdateExtensionProcessor.",
        "ShowExtensionProcessor.",
        "ListExtensionsProcessor.",

        //Auth
        "LoginProcessor.",
        "LogoutProcessor.",
        "ProfileListProcessor.",
        "ProfileShowProcessor.",
        "ProfileSetProcessor.",
        "ProfileRemoveProcessor.",

        // Config commands (Set/Get/List/Delete/Info).
        "SetConfigCommand.",
        "GetConfigCommand.",
        "ListConfigCommand.",
        "DeleteConfigCommand.",
        "InfoConfigCommand.",

        // Engine.Subscriptions writes $"{ActionStart}.{ClassName}.{FunctionName}"
        // for the ActionStart event, so we need the prefix not the exact match.
        "ActionStart.",

        // Individual startup phases: AzureCLI.Startup.Container, AzureCLI.Startup.Services.
        "AzureCLI.Startup.",

        // Individual import phases: PowerShell.Startup.Container.
        "PowerShell.Startup.",
    };

    /// <summary>Immutable snapshot of the verbatim allowed names.</summary>
    public static IReadOnlyCollection<string> AllowlistExact => Array.AsReadOnly(AllowedExactNames.ToArray());

    /// <summary>Immutable snapshot of the allowed name prefixes.</summary>
    public static IReadOnlyCollection<string> AllowlistPrefixes => Array.AsReadOnly(AllowedNamePrefixes.ToArray());

    public bool IsAllowed(string? activityName)
    {
        if (string.IsNullOrEmpty(activityName)) return false;
        if (AllowedExactNames.Contains(activityName)) return true;
        foreach (var prefix in AllowedNamePrefixes)
        {
            if (activityName.StartsWith(prefix, StringComparison.Ordinal)) return true;
        }
        return false;
    }
}
