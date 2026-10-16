// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Diagnostics;
using Core.Abstractions;

namespace Microsoft.Azure.Azclips.Extensions.Config.Commands;

public sealed class SetConfigCommand : ConfigCommandBase
{
    [ArgumentInfo(Required = true, Description = @"Configuration key in dot notation.", SerializedName = @"Key")]
    public string Key { get; set; } = string.Empty;

    [ArgumentInfo(Required = false, Description = @"Configuration value. For list keys, replaces the whole list; an empty value clears it.", SerializedName = @"Value")]
    public string? Value { get; set; }

    [ArgumentInfo(Required = false, Description = @"Entries to add to a list key (comma- or semicolon-separated).", SerializedName = @"Add")]
    public string? Add { get; set; }

    [ArgumentInfo(Required = false, Description = @"Entries to remove from a list key (comma- or semicolon-separated).", SerializedName = @"Remove")]
    public string? Remove { get; set; }

    [ArgumentInfo(Required = false, Description = @"Allow unknown keys.", SerializedName = @"Force")]
    public bool Force { get; set; }

    public override async Task ProcessAsync()
    {
        using var _ = StartActivity($"{nameof(SetConfigCommand)}.{nameof(ProcessAsync)}", ActivityKind.Internal);

        var provided = (Value is not null ? 1 : 0) + (Add is not null ? 1 : 0) + (Remove is not null ? 1 : 0);
        if (provided != 1)
        {
            throw AzclipsErrorException.InvalidInput(
                "Specify exactly one of --value, --add, or --remove.");
        }

        var isList = IsListKey(Key);

        if (Value is not null)
        {
            var value = isList ? (object?)Value : ParseValue(Value);
            await GetConfigurationService().SetConfigurationAsync(Key, value, Force, CancellationToken).ConfigureAwait(false);
            await WriteOutput($"Set {Key} = {FormatValue(value)}", CancellationToken).ConfigureAwait(false);
            return;
        }

        if (!isList)
        {
            throw AzclipsErrorException.InvalidInput(
                $"--add and --remove are only supported for list configuration keys. '{Key}' is not a list key.");
        }

        var result = string.Empty;
        if (Add is not null)
        {
            var additions = Add;
            await GetConfigurationService().UpdateListConfigurationAsync(
                Key,
                current => result = CommandExclusionList.Add(current, additions),
                CancellationToken).ConfigureAwait(false);
        }
        else
        {
            var removals = Remove!;
            await GetConfigurationService().UpdateListConfigurationAsync(
                Key,
                current => result = CommandExclusionList.Remove(current, removals),
                CancellationToken).ConfigureAwait(false);
        }

        await WriteOutput($"Set {Key} = {result}", CancellationToken).ConfigureAwait(false);
    }
}
