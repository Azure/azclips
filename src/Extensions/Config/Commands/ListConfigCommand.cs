// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Diagnostics;
using Core.Abstractions;

namespace Microsoft.Azure.Azclips.Extensions.Config.Commands;

public sealed class ListConfigCommand : ConfigCommandBase
{
    [ArgumentInfo(Required = false, Description = @"Include default values.", SerializedName = @"All")]
    public bool All { get; set; }

    public override async Task ProcessAsync()
    {
        using var _ = StartActivity($"{nameof(ListConfigCommand)}.{nameof(ProcessAsync)}", ActivityKind.Internal);

        var entries = (await GetResolvedEntriesAsync(includeDefaults: All).ConfigureAwait(false))
            .Values
            .OrderBy(entry => entry.Key, StringComparer.OrdinalIgnoreCase)
            .ToList();

        await WriteOutput(entries, CancellationToken).ConfigureAwait(false);
    }
}