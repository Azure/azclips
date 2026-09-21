// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Diagnostics;
using Core.Abstractions;

namespace Microsoft.Azure.Azclips.Extensions.Config.Commands;

public sealed class GetConfigCommand : ConfigCommandBase
{
    [ArgumentInfo(Required = true, Description = @"Configuration key in dot notation.", SerializedName = @"Key")]
    public string Key { get; set; } = string.Empty;

    [ArgumentInfo(Required = false, Description = @"Output as JSON.", SerializedName = @"Json")]
    public bool Json { get; set; }

    public override async Task ProcessAsync()
    {
        using var _ = StartActivity($"{nameof(GetConfigCommand)}.{nameof(ProcessAsync)}", ActivityKind.Internal);

        var entries = await GetResolvedEntriesAsync(includeDefaults: true).ConfigureAwait(false);
        var normalizedKey = NormalizeKey(Key);

        // Check for exact match first (leaf value)
        if (entries.TryGetValue(normalizedKey, out var entry))
        {
            object output = Json
                ? SerializeAsJsonValue(entry)
                : $"{entry.Key} = {FormatValue(entry.Value)} [{entry.Source}]";

            await WriteOutput(output, CancellationToken).ConfigureAwait(false);
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

        // Reconstruct nested object from matching entries
        var nested = ReconstructNestedObject(normalizedKey, matchingEntries);
        object sectionOutput = Json
            ? SerializeAsJsonValue(nested)
            : FormatNestedAsTable(nested);

        await WriteOutput(sectionOutput, CancellationToken).ConfigureAwait(false);
    }
}