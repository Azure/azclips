using System.Diagnostics;
using Core.Abstractions;

namespace Microsoft.Azure.Cirrus.Extensions.Config.Commands;

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
            var keys = schema.GetAllKeys().Select(key => key.Key).OrderBy(key => key, StringComparer.OrdinalIgnoreCase);
            await WriteOutput(Serialize(keys)).ConfigureAwait(false);
            return;
        }

        var metadata = schema.GetAllKeys().OrderBy(key => key.Key, StringComparer.OrdinalIgnoreCase);
        await WriteOutput(Serialize(metadata)).ConfigureAwait(false);
    }
}