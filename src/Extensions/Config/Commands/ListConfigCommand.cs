// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Diagnostics;
using Core.Abstractions;

namespace Microsoft.Azure.Azclips.Extensions.Config.Commands;

public sealed class ListConfigCommand : ConfigCommandBase
{
    [ArgumentInfo(Required = false, Description = @"Show only values configured in the configuration file or environment.", SerializedName = @"ConfiguredOnly")]
    public bool ConfiguredOnly { get; set; }

    public override async Task ProcessAsync()
    {
        using var _ = StartActivity($"{nameof(ListConfigCommand)}.{nameof(ProcessAsync)}", ActivityKind.Internal);

        await WriteEntriesAsync(includeDefaults: !ConfiguredOnly).ConfigureAwait(false);
    }
}