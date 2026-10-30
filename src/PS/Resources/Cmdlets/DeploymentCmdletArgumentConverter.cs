// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections;

namespace Microsoft.Azure.Azclips.Cmdlets.Resources;

internal static class DeploymentCmdletArgumentConverter
{
    public static Dictionary<string, string> ToTagDictionary(Hashtable tags)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (DictionaryEntry entry in tags)
        {
            result[entry.Key.ToString()!] = entry.Value?.ToString() ?? string.Empty;
        }

        return result;
    }
}
