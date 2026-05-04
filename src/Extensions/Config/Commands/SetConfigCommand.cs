using System.Diagnostics;
using Core.Abstractions;

namespace Microsoft.Azure.Cirrus.Extensions.Config.Commands;

public sealed class SetConfigCommand : ConfigCommandBase
{
    [ArgumentInfo(Required = true, Description = @"Configuration key in dot notation.", SerializedName = @"Key")]
    public string Key { get; set; } = string.Empty;

    [ArgumentInfo(Required = true, Description = @"Configuration value.", SerializedName = @"Value")]
    public string Value { get; set; } = string.Empty;

    [ArgumentInfo(Required = false, Description = @"Allow unknown keys.", SerializedName = @"Force")]
    public bool Force { get; set; }

    public override async Task ProcessAsync()
    {
        using var _ = StartActivity($"{nameof(SetConfigCommand)}.{nameof(ProcessAsync)}", ActivityKind.Internal);

        var parsedValue = ParseValue(Value);
        await GetConfigurationService().SetConfigurationAsync(Key, parsedValue, Force, CancellationToken).ConfigureAwait(false);
        await WriteOutput($"Set {Key} = {FormatValue(parsedValue)}", CancellationToken).ConfigureAwait(false);
    }
}