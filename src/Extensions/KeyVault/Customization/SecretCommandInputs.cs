using System.Globalization;
using System.Text.RegularExpressions;

namespace Microsoft.Azure.Azclips.Extensions.KeyVault.Customization;

internal static class SecretCommandInputs
{
    public static void ValidateName(string name)
    {
        if (!Regex.IsMatch(name, "^[a-zA-Z0-9-]{1,127}$", RegexOptions.CultureInvariant))
            throw new ArgumentException("Secret names must contain 1-127 letters, digits or hyphens.");
    }

    public static void ValidateVersion(string? version)
    {
        if (version is not null && !Regex.IsMatch(version, "^[a-zA-Z0-9]{1,64}$", RegexOptions.CultureInvariant))
            throw new ArgumentException("Secret version must be an alphanumeric version identifier.");
    }

    public static void ValidatePageSize(int? pageSize)
    {
        if (pageSize is < 1 or > 25)
            throw new ArgumentException("Page size must be between 1 and 25.");
    }

    public static bool? ParseEnabled(string? enabled)
    {
        if (enabled is null) return null;
        if (bool.TryParse(enabled, out bool result)) return result;
        throw new ArgumentException("Enabled must be true or false.");
    }

    public static int? ParseTime(string? text)
    {
        if (text is null) return null;
        if (!DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var date) ||
            date.ToUnixTimeSeconds() is < 0 or > int.MaxValue)
            throw new ArgumentException("Dates must be UTC timestamps between 1970 and January 2038 supported by the selected API.");
        return (int)date.ToUnixTimeSeconds();
    }

    public static IDictionary<string, string>? ParseTags(string[]? tags)
    {
        if (tags is null) return null;
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (string tag in tags)
        {
            int separator = tag.IndexOf('=');
            if (separator <= 0) throw new ArgumentException("Tags must use NAME=VALUE syntax.");
            result[tag[..separator]] = tag[(separator + 1)..];
        }
        return result;
    }
}