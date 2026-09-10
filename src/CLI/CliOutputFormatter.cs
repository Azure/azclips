// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Encodings.Web;
using System.Text.Json;

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

    /// <summary>
    /// Converts a command output value to a display string suitable for the console.
    /// </summary>
    /// <param name="value">The value to render.</param>
    /// <param name="format">
    /// Desired output shape. <see cref="Parser.OutputFormat.Json"/>/<see cref="Parser.OutputFormat.Yaml"/>
    /// never colorize regardless of <paramref name="colorPolicyAllowsColor"/> (deterministic for
    /// scripting); <see cref="Parser.OutputFormat.Jsonc"/> colorizes via <see cref="JsonColorizer"/>
    /// when <paramref name="colorPolicyAllowsColor"/> is <c>true</c>.
    /// <see cref="Parser.OutputFormat.Yamlc"/> coloring is tracked separately (azclips#836) and
    /// currently renders identically to <see cref="Parser.OutputFormat.Yaml"/>.
    /// Strings and primitives are unaffected by <paramref name="format"/> -- a scalar renders the
    /// same in every format.
    /// </param>
    /// <param name="colorPolicyAllowsColor">
    /// Whether <c>Core.Runtime.ColorPolicy.ShouldEmitColor</c> currently allows color. This method
    /// does not consult the policy itself -- callers gate it.
    /// </param>
    internal static string FormatValue(object? value, Parser.OutputFormat format = Parser.OutputFormat.Json, bool colorPolicyAllowsColor = false)
    {
        if (value is null)
            return string.Empty;

        if (value is string s)
            return s;

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
        var json = JsonSerializer.Serialize(value, value.GetType(), s_jsonOptions);

        return format switch
        {
            Parser.OutputFormat.Table => TableFormatter.Format(json),
            Parser.OutputFormat.Tsv => TsvFormatter.Format(json),
            Parser.OutputFormat.Yaml or Parser.OutputFormat.Yamlc => YamlFormatter.Format(json),
            Parser.OutputFormat.Jsonc => colorPolicyAllowsColor ? JsonColorizer.Colorize(json) : json,
            _ => json, // Json: never colorized, regardless of colorPolicyAllowsColor.
        };
    }
}
