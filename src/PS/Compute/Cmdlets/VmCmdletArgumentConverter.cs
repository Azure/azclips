// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections;
using System.Text.Json;

namespace Microsoft.Azure.Azclips.Cmdlets.Compute;

internal static class VmCmdletArgumentConverter
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

    /// <summary>
    /// Packs <paramref name="table"/> into the one-element array that vm create's TagsInput binds.
    /// It is a JSON object, so keys and values containing '=' or spaces survive parsing.
    /// </summary>
    public static string[] ToTagsInput(Hashtable table) => [JsonSerializer.Serialize(ToStringDictionary(table))];
}
