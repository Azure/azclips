// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;

namespace Microsoft.Azure.Azclips.Extensions.Network.Processors;

/// <summary>
/// Parses CLI <c>--tags</c> values of the form <c>key[=value]</c> into a tags dictionary.
/// A key without <c>=value</c> gets an empty string, unless <c>requireValue</c> is true (then it is an error).
/// When a key repeats, the last value wins.
/// </summary>
internal static class NetworkTagsParser
{
    internal static Dictionary<string, string>? ParseTags(string[]? tags, bool requireValue = false)
    {
        if (tags is null)
        {
            return null;
        }

        var parsed = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (string entry in tags)
        {
            if (string.IsNullOrWhiteSpace(entry))
            {
                continue;
            }

            int separator = entry.IndexOf('=');
            string key = (separator < 0 ? entry : entry[..separator]).Trim();
            if (key.Length == 0)
            {
                throw AzclipsErrorException.InvalidInput($"Invalid tag '{entry}'. Tag keys cannot be empty.");
            }

            if (requireValue && separator < 0)
            {
                throw AzclipsErrorException.InvalidInput($"Invalid tag '{entry}'. Use the format KEY=VALUE.");
            }

            parsed[key] = separator < 0 ? string.Empty : entry[(separator + 1)..];
        }

        return parsed;
    }
}
