// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Encodings.Web;
using System.Text.Json;
using Core.Abstractions.Runtime;
using Core.Cli.Parser;

namespace Core.Cli.Output;

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

    /// <summary>
    /// Converts a command output value to a display string suitable for the console.
    /// </summary>
    /// <param name="value">The value to render.</param>
    /// <param name="format">
    /// Desired output shape. <see cref="OutputFormat.Json"/>/<see cref="OutputFormat.Yaml"/>
    /// never colorize regardless of <paramref name="colorPolicyAllowsColor"/> (deterministic for
    /// scripting); <see cref="OutputFormat.Jsonc"/> colorizes via <see cref="JsonColorizer"/> and
    /// <see cref="OutputFormat.Yamlc"/> via <see cref="YamlColorizer"/>, both only when
    /// <paramref name="colorPolicyAllowsColor"/> is <c>true</c>.
    /// Strings and primitives are unaffected by <paramref name="format"/> -- a scalar renders the
    /// same in every format.
    /// </param>
    /// <param name="colorPolicyAllowsColor">
    /// Whether <c>Core.Runtime.ColorPolicy.ShouldEmitColor</c> currently allows color. This method
    /// does not consult the policy itself -- callers gate it.
    /// </param>
    internal static string FormatValue(
        object? value,
        OutputFormat format = OutputFormat.Json,
        bool colorPolicyAllowsColor = false,
        string? query = null)
    {
        if (value is null)
        {
            if (query is null)
                return string.Empty;

            var nullJson = "null";
            return RenderFormattedJson(JmesPathEvaluator.Transform(nullJson, query), format, colorPolicyAllowsColor);
        }

        if (value is string s && query is null)
            return s;

        if (value is SerializedJson serializedJson && query is null)
            return serializedJson.Value;

        var type = value.GetType();
        if (query is null && (type.IsPrimitive || type == typeof(decimal)))
            return JsonSerializer.Serialize(value, type, s_jsonOptions);

        var json = value switch
        {
            SerializedJson rawJson => rawJson.Value,
            string text when query is null => text,
            string text => JsonSerializer.Serialize(text, typeof(string), s_jsonOptions),
            _ => SerializeToJson(value),
        };

        if (query is not null)
        {
            json = JmesPathEvaluator.Transform(json, query);
        }

        return RenderFormattedJson(json, format, colorPolicyAllowsColor);
    }

    private static string RenderFormattedJson(string json, OutputFormat format, bool colorPolicyAllowsColor)
    {
        if (format is OutputFormat.Jsonc && IsScalarJsonLiteral(json))
            return json;

        return format switch
        {
            OutputFormat.Table => TableFormatter.Format(json),
            OutputFormat.Tsv => TsvFormatter.Format(json),
            OutputFormat.Yaml => YamlFormatter.Format(json),
            OutputFormat.Yamlc => Colorize(YamlFormatter.Format(json), colorPolicyAllowsColor, YamlColorizer.Colorize),
            OutputFormat.Jsonc => Colorize(json, colorPolicyAllowsColor, JsonColorizer.Colorize),
            _ => json, // Json: never colorized, regardless of colorPolicyAllowsColor.
        };
    }

    private static string SerializeToJson(object value)
    {
        // Primitives and decimals go through JsonSerializer so bool renders as lowercase
        // "true"/"false" (valid JSON literals consumable by scripts and tools like jq)
        // rather than Boolean.ToString()'s "True"/"False". JSON also guarantees invariant
        // formatting for numerics (period as decimal separator), so int/double/decimal
        // render locale-independently (e.g., 3.14 not 3,14). A scalar's shape doesn't change
        // across formats, so this branch is format-agnostic.
        var type = value.GetType();
        if (type.IsPrimitive || type == typeof(decimal))
            return JsonSerializer.Serialize(value, type, s_jsonOptions);

        // TODO: Reconsider error handling before GA. Rethrowing surfaces serialization
        // bugs early. A fallback to ToString() is not useful here because it would most
        // likely return a meaningless type name. Once models stabilise, catching and
        // falling back gracefully may be preferable for production resilience.
        return JsonSerializer.Serialize(value, value.GetType(), s_jsonOptions);
    }

    private static bool IsScalarJsonLiteral(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.ValueKind is not (JsonValueKind.Object or JsonValueKind.Array);
    }

    private static string Colorize(string text, bool colorPolicyAllowsColor, Func<string, string> colorize) =>
        colorPolicyAllowsColor ? colorize(text) : text;
}
