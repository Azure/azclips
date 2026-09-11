// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections;

namespace Microsoft.Azure.Azclips.Cmdlets.Identity;

internal static class IdentityCmdletArgumentConverter
{
    public static Dictionary<string, string> ToStringDictionary(Hashtable table)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (DictionaryEntry entry in table)
        {
            if (entry.Key is null)
            {
                throw new ArgumentException("Key cannot be null.", nameof(table));
            }

            result[entry.Key.ToString()!] = entry.Value?.ToString() ?? string.Empty;
        }

        return result;
    }
}

