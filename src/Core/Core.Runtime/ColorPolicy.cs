// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Core.Runtime;

/// <summary>
/// Persistent color-preference mode for the CLI, sourced from <c>core.output.color</c> config.
/// </summary>
public enum ColorMode
{
    /// <summary>Respect env vars and redirection detection (ecosystem default).</summary>
    Auto,
    /// <summary>Force color on unless <c>NO_COLOR</c> env overrides.</summary>
    Always,
    /// <summary>Force color off; still overridden by <c>FORCE_COLOR</c> env.</summary>
    Never,
}

/// <summary>
/// Determines whether ANSI/console color output should be emitted for a given stream.
/// </summary>
/// <remarks>
/// Precedence, top to bottom (first match wins):
/// <list type="number">
///   <item><description>Console initialization failed → no color, including forced color.</description></item>
///   <item><description><c>NO_COLOR</c> env (any non-empty value) → no color. See https://no-color.org.</description></item>
///   <item><description><c>FORCE_COLOR</c> env (any non-empty value) → force color.</description></item>
///   <item><description>Configured <see cref="ColorMode.Always"/> → force color.</description></item>
///   <item><description>Configured <see cref="ColorMode.Never"/> → no color.</description></item>
///   <item><description>Configured <see cref="ColorMode.Auto"/> (default) → fall through.</description></item>
///   <item><description><c>TERM=dumb</c> → no color.</description></item>
///   <item><description>Target stream is redirected → no color.</description></item>
///   <item><description>Otherwise → color.</description></item>
/// </list>
/// Env vars sit above config because they're per-invocation overrides; config is persistent policy.
/// Matches the convention adopted by git, npm, cargo, terraform.
/// </remarks>
public static class ColorPolicy
{
    // Cached mode set once at process startup via Initialize. Default Auto keeps the pre-config
    // behavior for tests and hosts that never call Initialize.
    private static ColorMode _mode = ColorMode.Auto;
    private static bool _supportsColor = true;

    /// <summary>Current cached mode. Exposed internal for test scoping.</summary>
    internal static ColorMode CurrentMode => _mode;

    /// <summary>
    /// Sets the persistent color mode. Called once at process startup after config is resolved.
    /// </summary>
    public static void Initialize(ColorMode mode) => Initialize(mode, supportsColor: true);

    /// <summary>
    /// Sets the color preference and whether console initialization permits styling.
    /// An unsupported console suppresses styling even when color is forced.
    /// </summary>
    public static void Initialize(ColorMode mode, bool supportsColor)
    {
        _mode = mode;
        _supportsColor = supportsColor;
    }

    /// <summary>
    /// Parses a <c>core.output.color</c> config value into a <see cref="ColorMode"/>.
    /// Invalid or empty values fall back to <see cref="ColorMode.Auto"/>.
    /// </summary>
    public static ColorMode Parse(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return ColorMode.Auto;
        return value.Trim().ToLowerInvariant() switch
        {
            "auto" => ColorMode.Auto,
            "always" => ColorMode.Always,
            "never" => ColorMode.Never,
            _ => ColorMode.Auto,
        };
    }

    /// <summary>
    /// Returns <c>true</c> when color output is allowed on the given stream.
    /// </summary>
    /// <param name="targetIsRedirected">
    /// Pass <see cref="Console.IsErrorRedirected"/> for writes to <c>stderr</c>,
    /// or <see cref="Console.IsOutputRedirected"/> for writes to <c>stdout</c>.
    /// </param>
    public static bool ShouldEmitColor(bool targetIsRedirected)
    {
        if (!_supportsColor) return false;

        if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("NO_COLOR")))
        {
            return false;
        }
        if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("FORCE_COLOR")))
        {
            return true;
        }
        if (_mode == ColorMode.Always) return true;
        if (_mode == ColorMode.Never) return false;
        if (string.Equals(Environment.GetEnvironmentVariable("TERM"), "dumb", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }
        return !targetIsRedirected;
    }
}
