// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections;

namespace Microsoft.Azure.Azclips.Extensions.Compute.Processors;

/// <summary>
/// Converts the value bound to vm create's tags into the tags sent to ARM, the way each legacy
/// tool does: <c>az vm create --tags</c> KEY[=VALUE] tokens like azure-cli's
/// <c>validate_tags</c>, and the <c>New-AzVM -Tag</c> hashtable like azure-powershell's
/// <c>TagsConversionHelper.CreateTagDictionary</c>. Neither tool validates tags; ARM does.
/// </summary>
internal static class ResourceTags
{
    /// <summary>
    /// Returns null when no tags were given, so requests carry no tags property; otherwise the
    /// tags, possibly empty. Keys are case-sensitive and the last duplicate wins.
    /// </summary>
    internal static Dictionary<string, string>? Normalize(object? value) => value switch
    {
        null => null,
        IDictionary table => FromTable(table),
        IEnumerable<string> tokens => FromTokens(tokens),
        object other => throw new ArgumentException(
            $"Tags must be KEY[=VALUE] strings or a dictionary, not {other.GetType().FullName}.", nameof(value)),
    };

    // New-AzVM -Tag: keys and values through ToString(), and a $null value as "".
    private static Dictionary<string, string> FromTable(IDictionary table)
    {
        var tags = new Dictionary<string, string>();
        foreach (DictionaryEntry entry in table)
        {
            tags[entry.Key.ToString() ?? string.Empty] = entry.Value?.ToString() ?? string.Empty;
        }
        return tags;
    }

    // az vm create --tags: split on the first '=', a missing value is "", nothing is trimmed,
    // and an empty token, as from --tags "", is skipped.
    private static Dictionary<string, string> FromTokens(IEnumerable<string> tokens)
    {
        var tags = new Dictionary<string, string>();
        foreach (string token in tokens)
        {
            if (token.Length == 0)
            {
                continue;
            }

            int separator = token.IndexOf('=');
            if (separator < 0)
            {
                tags[token] = string.Empty;
            }
            else
            {
                tags[token[..separator]] = token[(separator + 1)..];
            }
        }
        return tags;
    }
}
