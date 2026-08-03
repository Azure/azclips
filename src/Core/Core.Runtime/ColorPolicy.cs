// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Core.Runtime;

/// <summary>
/// Determines whether ANSI/console color output should be emitted for a given stream.
/// </summary>
/// <remarks>
/// Follows the ecosystem precedence adopted by git, npm, cargo, terraform, etc.:
/// <list type="number">
///   <item><description><c>NO_COLOR</c> (any non-empty value) → no color. See https://no-color.org.</description></item>
///   <item><description><c>FORCE_COLOR</c> (any non-empty value) → force color.</description></item>
///   <item><description><c>TERM=dumb</c> → no color.</description></item>
///   <item><description>Target stream is redirected → no color.</description></item>
///   <item><description>Otherwise → color.</description></item>
/// </list>
/// </remarks>
public static class ColorPolicy
{
    /// <summary>
    /// Returns <c>true</c> when color output is allowed on the given stream.
    /// </summary>
    /// <param name="targetIsRedirected">
    /// Pass <see cref="Console.IsErrorRedirected"/> for writes to <c>stderr</c>,
    /// or <see cref="Console.IsOutputRedirected"/> for writes to <c>stdout</c>.
    /// </param>
    internal static bool ShouldEmitColor(bool targetIsRedirected)
    {
        if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("NO_COLOR")))
        {
            return false;
        }
        if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("FORCE_COLOR")))
        {
            return true;
        }
        if (string.Equals(Environment.GetEnvironmentVariable("TERM"), "dumb", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }
        return !targetIsRedirected;
    }
}
