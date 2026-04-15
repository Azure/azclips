using System.Diagnostics;
using Core.Abstractions;

namespace Microsoft.Azure.Cirrus.Extensions.Config.Commands;

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
        if (!entries.TryGetValue(Key, out var entry))
        {
            throw new KeyNotFoundException($"Configuration key '{Key}' was not found.");
        }

        var output = Json
            ? Serialize(entry)
            : $"{entry.Key} = {FormatValue(entry.Value)} [{entry.Source}]";

        await WriteOutput(output).ConfigureAwait(false);
    }
}