// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text;

namespace Benchmark.Telemetry;

/// <summary>Resolves which benchmark method the current process is running.</summary>
/// <remarks>
/// BenchmarkDotNet runs one child process per benchmark case and passes
/// <c>--benchmarkName &lt;fully.qualified.Type.Method&gt;</c> to it. The name matches the summary
/// row, so a telemetry file correlates to a reported result.
/// </remarks>
internal static class BenchmarkName
{
    private const string ArgumentName = "--benchmarkName";
    private const string NamespacePrefix = "Benchmark.";

    internal static string FromCommandLine() => FromArguments(Environment.GetCommandLineArgs());

    internal static string FromArguments(IReadOnlyList<string> args)
    {
        // Stop one short of the end: the flag is useless without its value.
        for (var i = 0; i < args.Count - 1; i++)
        {
            if (string.Equals(args[i], ArgumentName, StringComparison.Ordinal))
            {
                return Sanitize(StripNamespace(args[i + 1]));
            }
        }

        throw new InvalidOperationException(
            $"'{ArgumentName}' was not on the command line, so this benchmark cannot name its " +
            "telemetry output. BenchmarkDotNet passes it to every benchmark child process, so this " +
            "means the benchmark was launched directly, or BenchmarkDotNet changed its " +
            "child-process arguments.");
    }

    private static string StripNamespace(string fullName) =>
        fullName.StartsWith(NamespacePrefix, StringComparison.Ordinal)
            ? fullName[NamespacePrefix.Length..]
            : fullName;

    private static string Sanitize(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var buffer = new StringBuilder(name.Length);
        foreach (var character in name)
        {
            buffer.Append(Array.IndexOf(invalid, character) >= 0 ? '_' : character);
        }

        return buffer.ToString();
    }
}
