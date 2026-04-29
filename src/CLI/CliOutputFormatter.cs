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
    // Use camelCase to match the Azure REST API and traditional az CLI JSON output
    // convention. Alternatives considered:
    //   - null (PascalCase from C# properties) — inconsistent with API responses
    //   - JsonNamingPolicy.SnakeCaseLower — not used by Azure APIs
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

        try
        {
            return JsonSerializer.Serialize(value, value.GetType(), s_jsonOptions);
        }
        catch (JsonException)
        {
            return value.ToString() ?? string.Empty;
        }
        catch (NotSupportedException)
        {
            return value.ToString() ?? string.Empty;
        }
    }
}
