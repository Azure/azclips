// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Encodings.Web;
using System.Text.Json;
using Core.Cli.Parser;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Core.Cli;

/// <summary>
/// Formats command output values for CLI console display.
/// Strings render as-is; primitives, decimals, complex objects, and collections are
/// serialized as JSON so top-level output is always valid JSON (except bare strings,
/// which stay unquoted for terminal readability).
/// </summary>
internal static class CliOutputFormatter
{
    // TODO: CamelCase is a temporary convention until proper property name mapping
    // is implemented (e.g., via [JsonPropertyName] on all model properties or a
    // dedicated output schema). Once models carry explicit serialization attributes,
    // this policy should be removed so the declared names take effect.
    private static readonly JsonSerializerOptions s_jsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        // Use relaxed escaping so console output stays human-readable: characters like the double
        // quotes inside a raw JSON body (e.g. the `rest` command's `content` string) render as `"`
        // instead of `\u0022`, and non-ASCII text is emitted verbatim rather than as `\uXXXX`. This
        // targets terminal display only (not HTML embedding), matching az-style CLI output.
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private static readonly ISerializer s_yamlSerializer = new SerializerBuilder()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .Build();

    /// <summary>
    /// Converts a command output value to a display string suitable for the console.
    /// </summary>
    internal static string FormatValue(object? value)
    {
        if (value is null)
            return string.Empty;

        if (value is string s)
            return s;

        // Primitives and decimals go through JsonSerializer so bool renders as lowercase
        // "true"/"false" (valid JSON literals consumable by scripts and tools like jq)
        // rather than Boolean.ToString()'s "True"/"False". JSON also guarantees invariant
        // formatting for numerics (period as decimal separator), so int/double/decimal
        // render locale-independently (e.g., 3.14 not 3,14).
        var type = value.GetType();
        if (type.IsPrimitive || type == typeof(decimal))
            return JsonSerializer.Serialize(value, type, s_jsonOptions);

        // TODO: Reconsider error handling before GA. Rethrowing surfaces serialization
        // bugs early. A fallback to ToString() is not useful here because it would most
        // likely return a meaningless type name. Once models stabilise, catching and
        // falling back gracefully may be preferable for production resilience.
        return JsonSerializer.Serialize(value, value.GetType(), s_jsonOptions);
    }

    internal static string FormatValue(object? value, OutputFormat format)
    {
        return format switch
        {
            OutputFormat.Yaml or OutputFormat.Yamlc => s_yamlSerializer.Serialize(value).TrimEnd(),
            _ => FormatValue(value),
        };
    }
}
