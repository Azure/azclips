using System.Collections;

namespace Microsoft.Azure.Azclips.Cmdlets.Storage;

internal static class StorageCmdletArgumentConverter
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