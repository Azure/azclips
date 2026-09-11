// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json;
using YamlDotNet.Serialization;

namespace Core.Cli.Output;

/// <summary>
/// Renders already-serialized JSON as YAML via YamlDotNet (hand-rolling YAML's string-quoting/
/// multiline-escaping rules is a real correctness risk; a mature library removes that risk
/// entirely). Consumes JSON text -- like <see cref="TableFormatter"/>/<see cref="TsvFormatter"/>
/// -- rather than the raw .NET value, so property names/date formatting/etc. always match what
/// <c>json</c>/<c>table</c>/<c>tsv</c> show; serializing the raw value directly let YamlDotNet's
/// own reflection expand types like <see cref="DateTimeOffset"/> into all their properties
/// instead of the single ISO 8601 string <c>System.Text.Json</c> already produces for it.
/// Backs both <c>OutputFormat.Yaml</c> and <c>OutputFormat.Yamlc</c> -- <c>Yamlc</c> colorizes
/// this method's output via <see cref="YamlColorizer"/> (see <see cref="CliOutputFormatter"/>).
/// </summary>
internal static class YamlFormatter
{
    private static readonly ISerializer s_serializer = new SerializerBuilder().Build();

    internal static string Format(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var graph = ToPlainObject(doc.RootElement);
        return s_serializer.Serialize(graph).Replace("\r\n", "\n").TrimEnd('\n');
    }

    private static object? ToPlainObject(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.Object => element.EnumerateObject().ToDictionary(p => p.Name, p => ToPlainObject(p.Value)),
        JsonValueKind.Array => element.EnumerateArray().Select(ToPlainObject).ToList(),
        JsonValueKind.String => element.GetString(),
        JsonValueKind.Number => element.TryGetInt64(out var l) ? l : element.TryGetDecimal(out var d) ? d : element.GetDouble(),
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        _ => null, // Null
    };
}
