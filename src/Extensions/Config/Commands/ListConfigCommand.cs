// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Diagnostics;
using System.Text;
using Core.Abstractions;

namespace Microsoft.Azure.Azclips.Extensions.Config.Commands;

public sealed class ListConfigCommand : ConfigCommandBase
{
    [ArgumentInfo(Required = false, Description = @"Include default values.", SerializedName = @"All")]
    public bool All { get; set; }

    [ArgumentInfo(Required = false, Description = @"Output as JSON.", SerializedName = @"Json")]
    public bool Json { get; set; }

    public override async Task ProcessAsync()
    {
        using var _ = StartActivity($"{nameof(ListConfigCommand)}.{nameof(ProcessAsync)}", ActivityKind.Internal);

        var entries = (await GetResolvedEntriesAsync(includeDefaults: All).ConfigureAwait(false))
            .Values
            .OrderBy(entry => entry.Key, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (Json)
        {
            await WriteOutput(SerializeAsJsonValue(entries), CancellationToken).ConfigureAwait(false);
            return;
        }

        var builder = new StringBuilder();
        foreach (var entry in entries)
        {
            builder.AppendLine($"{entry.Key} = {FormatValue(entry.Value)} [{entry.Source}]");
        }

        await WriteOutput(builder.ToString().TrimEnd(), CancellationToken).ConfigureAwait(false);
    }
}