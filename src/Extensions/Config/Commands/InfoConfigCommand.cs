// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Diagnostics;
using Core.Abstractions;
using Core.Abstractions.Runtime;

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
            await WriteFormattableOutput(keys).ConfigureAwait(false);
            return;
        }

        var metadata = schema.GetAllKeys()
            .OrderBy(key => key.Key, StringComparer.OrdinalIgnoreCase)
            .Select(key => new
            {
                key.Key,
                key.Description,
                key.DefaultValue,
                ValueType = key.ValueType?.Name,
                AllowedValues = key.AllowedValues,
                key.Minimum,
                key.Maximum,
                key.ValidationPattern,
                key.IsUri
            })
            .ToList();
        await WriteFormattableOutput(metadata).ConfigureAwait(false);
    }

    private Task WriteFormattableOutput(object value)
    {
        var json = SerializeAsJsonValue(value);
        return WriteOutput(new FormattableOutput(value, json.Value), CancellationToken);
    }
}