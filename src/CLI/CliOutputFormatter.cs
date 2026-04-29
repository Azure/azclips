using System.Globalization;
using System.Text.Json;

namespace Core.Cli;

/// <summary>
/// Formats command output values for CLI console display.
/// Strings and primitives are rendered via ToString(); complex objects
/// (including collections and dictionaries) are serialized as JSON.
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
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    /// <summary>
    /// Converts a command output value to a display string suitable for the console.
    /// </summary>
    internal static string FormatValue(object? value)
    {
        if (value is null)
            return string.Empty;

        if (value is string s)
            return s;

        // Use InvariantCulture for primitives/decimals so output is locale-independent
        // (e.g., 3.14 not 3,14).
        var type = value.GetType();
        if (type.IsPrimitive || type == typeof(decimal))
            return value is IFormattable fmt
                ? fmt.ToString(null, CultureInfo.InvariantCulture)
                : (value.ToString() ?? string.Empty);

        // TODO: Reconsider error handling before GA. Rethrowing surfaces serialization
        // bugs early. A fallback to ToString() is not useful here because it would most
        // likely return a meaningless type name. Once models stabilise, catching and
        // falling back gracefully may be preferable for production resilience.
        return JsonSerializer.Serialize(value, value.GetType(), s_jsonOptions);
    }
}
