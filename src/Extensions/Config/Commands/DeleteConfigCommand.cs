// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Diagnostics;
using Core.Abstractions;

namespace Microsoft.Azure.Azclips.Extensions.Config.Commands;

public sealed class DeleteConfigCommand : ConfigCommandBase
{
    [ArgumentInfo(Required = true, Description = @"Configuration key in dot notation.", SerializedName = @"Key")]
    public string Key { get; set; } = string.Empty;

    [ArgumentInfo(Required = false, Description = @"Allow unknown keys.", SerializedName = @"Force")]
    public bool Force { get; set; }

    public override async Task ProcessAsync()
    {
        using var _ = StartActivity($"{nameof(DeleteConfigCommand)}.{nameof(ProcessAsync)}", ActivityKind.Internal);

        await GetConfigurationService().DeleteConfigurationAsync(Key, Force, CancellationToken).ConfigureAwait(false);
        await NotifyChangeHandlersAsync(Key, null, deleted: true).ConfigureAwait(false);
        await WriteOutput($"Deleted {Key}", CancellationToken).ConfigureAwait(false);
    }
}