// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Extensions;
using DevLab.JmesPath;

namespace Core.Cli.Output;

/// <summary>
/// Applies a JMESPath expression (<c>--query</c>) to an already-serialized JSON result before
/// format-specific rendering. Thin wrapper around <see cref="JmesPath.Net"/> -- its
/// <see cref="DevLab.JmesPath.JmesPath.Transform(string, string)"/> is JSON-string-in/JSON-string-out,
/// so the underlying Newtonsoft.Json dependency never leaks past this class; callers keep working
/// with <c>System.Text.Json</c> as before.
/// </summary>
internal static class JmesPathEvaluator
{
    // JmesPath.Net has no dedicated exception type for invalid syntax (just System.Exception), so
    // a broad catch is the only option -- see JmesPathEvaluatorTests for confirmed exception shape.
    private static readonly DevLab.JmesPath.JmesPath s_jmesPath = new();

    /// <summary>
    /// Transforms <paramref name="json"/> per <paramref name="expression"/>, returning the
    /// resulting JSON text (a JSON value of any kind -- object, array, string, number, bool, or
    /// the literal <c>null</c> when nothing matches).
    /// </summary>
    /// <exception cref="AzclipsErrorException">
    /// Raised as a usage error (exit code 2) when <paramref name="expression"/> is not valid
    /// JMESPath syntax.
    /// </exception>
    internal static string Transform(string json, string expression)
    {
        try
        {
            return s_jmesPath.Transform(json, expression);
        }
        catch (Exception ex)
        {
            throw AzclipsErrorException.UsageError(
                $"--query expression '{expression}' could not be applied: {ex.Message}",
                new Dictionary<string, object?>
                {
                    ["flag"] = "--query",
                    ["expression"] = expression,
                });
        }
    }

    internal static void Validate(string expression)
    {
        try
        {
            _ = s_jmesPath.Parse(expression);
        }
        catch (Exception ex)
        {
            throw AzclipsErrorException.UsageError(
                $"--query expression '{expression}' is not valid JMESPath: {ex.Message}",
                new Dictionary<string, object?>
                {
                    ["flag"] = "--query",
                    ["expression"] = expression,
                });
        }
    }
}
