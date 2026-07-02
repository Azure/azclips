using System.Text.RegularExpressions;

namespace Core.Abstractions;

/// <summary>
/// Validation rules for profile names. Names must be 1–100 characters consisting of
/// alphanumeric characters, hyphens (<c>-</c>), underscores (<c>_</c>), and periods (<c>.</c>).
/// The name <c>"temp"</c> is reserved for bare login and must not be accepted by
/// user-facing profile creation paths.
/// </summary>
public static partial class ProfileNameValidator
{
    /// <summary>The reserved profile name used exclusively by bare login.</summary>
    public const string ReservedTempName = "temp";

    [GeneratedRegex(@"^[a-zA-Z0-9._-]{1,100}$", RegexOptions.CultureInvariant)]
    private static partial Regex NamePattern();

    /// <summary>Returns true if <paramref name="name"/> satisfies the character and length rules.</summary>
    public static bool IsValid(string? name) => !string.IsNullOrEmpty(name) && NamePattern().IsMatch(name);

    /// <summary>Returns true if <paramref name="name"/> is the reserved "temp" name.</summary>
    public static bool IsReserved(string? name) =>
        string.Equals(name, ReservedTempName, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Validates the profile name's characters and length, throwing
    /// <see cref="ArgumentException"/> when invalid.
    /// </summary>
    public static void Validate(string? name)
    {
        if (!IsValid(name))
        {
            throw new ArgumentException(
                $"Invalid profile name '{name}'. Profile names must be 1-100 characters and contain only letters, digits, '-', '_', or '.'.",
                nameof(name));
        }
    }

    /// <summary>
    /// Validates a name supplied by a user-facing creation path: enforces the character and
    /// length rules and rejects the reserved "temp" name.
    /// </summary>
    public static void ValidateForCreation(string? name)
    {
        Validate(name);
        if (IsReserved(name))
        {
            throw new ArgumentException(
                $"The profile name '{ReservedTempName}' is reserved for bare login and cannot be created directly.",
                nameof(name));
        }
    }
}
