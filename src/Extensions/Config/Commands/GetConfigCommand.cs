// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Diagnostics;
using Core.Abstractions;
using Microsoft.Azure.Azclips.Extensions.Config.Models;

namespace Microsoft.Azure.Azclips.Extensions.Config.Commands;

public sealed class GetConfigCommand : ConfigCommandBase
{
    [ArgumentInfo(Required = false, Description = @"Configuration key in dot notation.", SerializedName = @"Key")]
    public string? Key { get; set; }

    [ArgumentInfo(Required = false, Description = @"Show only values configured in the configuration file or environment.", SerializedName = @"ConfiguredOnly")]
    public bool ConfiguredOnly { get; set; }

    public override async Task ProcessAsync()
    {
        using var _ = StartActivity($"{nameof(GetConfigCommand)}.{nameof(ProcessAsync)}", ActivityKind.Internal);

        if (Key is null)
        {
            await WriteEntriesAsync(includeDefaults: !ConfiguredOnly).ConfigureAwait(false);
            return;
        }

        if (ConfiguredOnly)
        {
            throw AzclipsErrorException.UsageError("Configured-only filtering cannot be combined with a configuration key or section.");
        }

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
            throw AzclipsErrorException.InvalidInput($"Configuration key '{Key}' was not found.");
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