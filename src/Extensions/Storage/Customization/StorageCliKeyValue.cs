// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Azure.Azclips.Extensions.Storage.Customization;

internal static class StorageCliKeyValue
{
    internal static IReadOnlyDictionary<string, string>? Parse(string[]? values, bool tags)
    {
        if (values is null) return null;
        if (values.Length == 0)
            throw new ArgumentException("Specify at least one metadata or tag entry.");
        var result = new Dictionary<string, string>(tags ? StringComparer.Ordinal : StringComparer.OrdinalIgnoreCase);
        foreach (string entry in values)
        {
            if (entry is null) throw new ArgumentException("Metadata and tag entries cannot be null.");
            int separator = entry.IndexOf('=');
            if (separator < 0 && !tags)
                throw new ArgumentException("Metadata entries require key=value.");
            string key = separator < 0 ? entry : entry[..separator];
            string value = separator < 0 ? "" : entry[(separator + 1)..];
            if (key.Length == 0 || !result.TryAdd(key, value))
                throw new ArgumentException("Metadata and tag entries require nonempty, unique keys.");
        }
        return result;
    }
}
