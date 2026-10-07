// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Diagnostics;
using Core.Abstractions;
using Microsoft.Azure.Azclips.Extensions.Config.Models;

namespace Microsoft.Azure.Azclips.Extensions.Config.Commands;

public sealed class InfoConfigCommand : ConfigCommandBase
{
    [ArgumentInfo(Required = false, Description = @"Return only valid keys.", SerializedName = @"KeysOnly")]
    public bool KeysOnly { get; set; }

    public override async Task ProcessAsync()
    {
        using var _ = StartActivity($"{nameof(InfoConfigCommand)}.{nameof(ProcessAsync)}", ActivityKind.Internal);

        var schema = GetConfigurationSchema();
        if (KeysOnly)
        {
            var keys = schema.GetAllKeys()
                .Select(key => key.Key)
                .OrderBy(key => key, StringComparer.OrdinalIgnoreCase)
                .ToList();
            await WriteOutput(keys, CancellationToken).ConfigureAwait(false);
            return;
        }

        var metadata = schema.GetAllKeys()
            .OrderBy(key => key.Key, StringComparer.OrdinalIgnoreCase)
            .Select(key => new ConfigKeyInfo(
                key.Key,
                key.Description,
                key.DefaultValue,
                key.ValueType?.Name,
                key.AllowedValues,
                key.Minimum,
                key.Maximum,
                key.ValidationPattern,
                key.IsUri))
            .ToList();
        await WriteOutput(metadata, CancellationToken).ConfigureAwait(false);
    }
}