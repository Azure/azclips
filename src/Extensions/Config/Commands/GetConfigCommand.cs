// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Diagnostics;
using Core.Abstractions;
using Microsoft.Azure.Azclips.Extensions.Config.Models;

namespace Microsoft.Azure.Azclips.Extensions.Config.Commands;

public sealed class GetConfigCommand : ConfigCommandBase
{
    [ArgumentInfo(Required = true, Description = @"Configuration key in dot notation.", SerializedName = @"Key")]
    public string Key { get; set; } = string.Empty;

    public override async Task ProcessAsync()
    {
        using var _ = StartActivity($"{nameof(GetConfigCommand)}.{nameof(ProcessAsync)}", ActivityKind.Internal);

        var entries = await GetResolvedEntriesAsync(includeDefaults: true).ConfigureAwait(false);
        var normalizedKey = NormalizeKey(Key);

        // Check for exact match first (leaf value)
        if (entries.TryGetValue(normalizedKey, out var entry))
        {
            await WriteOutput(entry, CancellationToken).ConfigureAwait(false);
            return;
        }

        // Check for section/subsection matches (prefix match)
        var matchingEntries = entries
            .Where(e => e.Key.StartsWith(normalizedKey + ".", StringComparison.OrdinalIgnoreCase))
            .OrderBy(e => e.Key, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (matchingEntries.Count == 0)
        {
            throw new KeyNotFoundException($"Configuration key '{Key}' was not found.");
        }

        var sectionEntries = matchingEntries
            .Select(item => new ConfigEntry(
                item.Key[(normalizedKey.Length + 1)..],
                item.Value.Value,
                item.Value.Source))
            .ToList();

        await WriteOutput(sectionEntries, CancellationToken).ConfigureAwait(false);
    }
}