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

        // CLI entrypoint span — wraps every CLI command invocation.
        "AzureCLI.ProcessCommand",

        // Engine event-driven activities. Engine.Subscriptions wraps these
        // Events.* ids directly (i.e. evt.Id without any class/method suffix).
        // The corresponding *End/*After events Dispose the existing activity,
        // they do not create a new one — that's why only the Start side appears.
        "SessionCreated",
        "AuthBeforeTokenAcquisition",
        "ProcessorProcessAsyncStart",
    };

    private static readonly string[] AllowedNamePrefixes =
    {
        // Service classes that use $"{nameof(Service)}.{nameof(Method)}" naming.
        // The activity *name* never carries secrets — tokens / account IDs land
        // in tags, which the property filter scrubs independently.
        // "MsalAuthService.",
        "AuthContextService.",

        // Extension processors (Add/Remove/Update/Show/List).
        "AddExtensionProcessor.",
        "RemoveExtensionProcessor.",
        "UpdateExtensionProcessor.",
        "ShowExtensionProcessor.",
        "ListExtensionsProcessor.",

        // Config commands (Set/Get/List/Delete/Info).
        "SetConfigCommand.",
        "GetConfigCommand.",
        "ListConfigCommand.",
        "DeleteConfigCommand.",
        "InfoConfigCommand.",

        // Engine.Subscriptions writes $"{ActionStart}.{ClassName}.{FunctionName}"
        // for the ActionStart event, so we need the prefix not the exact match.
        "ActionStart.",
    };

    /// <summary>Read-only view of the verbatim allowed names.</summary>
    public static IReadOnlyCollection<string> AllowlistExact => AllowedExactNames;

    /// <summary>Read-only view of the allowed name prefixes.</summary>
    public static IReadOnlyCollection<string> AllowlistPrefixes => AllowedNamePrefixes;

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
