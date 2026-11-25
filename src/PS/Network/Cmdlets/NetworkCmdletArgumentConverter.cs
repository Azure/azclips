// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections;
using System.Management.Automation;

namespace Microsoft.Azure.Azclips.Cmdlets.Network;

internal static class NetworkCmdletArgumentConverter
{
    public static Dictionary<string, string> ToStringDictionary(Hashtable table)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (DictionaryEntry entry in table)
        {
            result[KeyToString(entry.Key, nameof(table))] = ValueToString(entry.Value);
        }

        return result;
    }

    /// <summary>
    /// Keeps the hashtable's enumeration order, so an [ordered] hashtable keeps the order the user typed.
    /// </summary>
    public static List<KeyValuePair<string, string>> ToPairs(Hashtable table)
    {
        var result = new List<KeyValuePair<string, string>>(table.Count);
        foreach (DictionaryEntry entry in table)
        {
            result.Add(new KeyValuePair<string, string>(KeyToString(entry.Key, nameof(table)), ValueToString(entry.Value)));
        }

        return result;
    }

    public static List<List<string>> ToNestedList(string[][] values) =>
        values.Select(inner => (inner ?? []).ToList()).ToList();

    private static string KeyToString(object? key, string paramName) =>
        key?.ToString() ?? throw new ArgumentException("Key cannot be null.", paramName);

    // Array values become a comma-separated list, as the legacy cmdlets did.
    private static string ValueToString(object? value)
    {
        var unwrapped = value is PSObject psObject ? psObject.BaseObject : value;
        return unwrapped switch
        {
            null => string.Empty,
            string text => text,
            IEnumerable items => string.Join(",", items.Cast<object?>().Select(item => ValueToString(item))),
            _ => unwrapped.ToString() ?? string.Empty,
        };
    }
}
