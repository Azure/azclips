// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;

namespace Microsoft.Azure.Azclips.Extensions.Network.Processors;

/// <summary>
/// Parses CLI tags in <c>KEY[=VALUE]</c> form. A key without "=" gets an empty value.
/// </summary>
internal static class NetworkTagsParser
{
    internal static Dictionary<string, string>? Parse(string[]? tags)
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

            parsed[key] = separator < 0 ? string.Empty : entry[(separator + 1)..];
        }

        return parsed;
    }
}
