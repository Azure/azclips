// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections;
using Core.Abstractions;

namespace Microsoft.Azure.Azclips.Cmdlets.Storage;

/// <summary>
/// Converts Az.Storage-shaped cmdlet values into the arguments the storage processors bind, which
/// follow the CLI. The PowerShell surface keeps the Az.Storage names and types so existing scripts
/// bind; these conversions are the only place the two shapes meet.
/// </summary>
public static class StorageCmdletArgumentConverter
{
    /// <summary>Az.Storage's spelling of the combined identity type, which ARM writes with a comma.</summary>
    public const string SystemAssignedUserAssigned = "SystemAssignedUserAssigned";

    /// <summary>Flattens <c>-Tag @{ k = 'v' }</c> into the <c>key=value</c> pairs the processor parses.</summary>
    public static string[] ToTagArguments(Hashtable tags)
    {
        var arguments = new List<string>(tags.Count);
        foreach (DictionaryEntry entry in tags)
        {
            var key = entry.Key.ToString() ?? string.Empty;

            // The processor splits each pair on its first '=', so a key containing one would be
            // silently cut short.
            if (key.Contains('='))
            {
                throw AzclipsErrorException.InvalidInput($"Tag name '{key}' cannot contain '='.");
            }

            arguments.Add($"{key}={entry.Value}");
        }

        return [.. arguments];
    }

    /// <summary>Formats a SAS expiration period as ARM's <c>DD.HH:MM:SS</c>, as Az.Storage does.</summary>
    public static string ToSasExpirationPeriod(TimeSpan period) => period.ToString(@"d\.hh\:mm\:ss");

    /// <summary>Maps Az.Storage's <c>SystemAssignedUserAssigned</c> onto ARM's <c>SystemAssigned,UserAssigned</c>.</summary>
    public static string ToIdentityType(string identityType) =>
        string.Equals(identityType, SystemAssignedUserAssigned, StringComparison.OrdinalIgnoreCase)
            ? "SystemAssigned,UserAssigned"
            : identityType;
}
