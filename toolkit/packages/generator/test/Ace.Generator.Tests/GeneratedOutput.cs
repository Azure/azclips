using Ace.Generator.Model;

namespace Ace.Generator.Tests;

/// <summary>
/// Test helpers that centralize the generator's <em>incidental</em> output
/// conventions — folder layout and file suffixes — in ONE place. Downstream tests
/// assert the stable contract (class coverage, parseability, content) through
/// these helpers, so a path/suffix change touches only this file rather than
/// churning assertions across the suite.
///
/// The exact path scheme itself is pinned in exactly one focused test:
/// <c>CSharpClientGeneratorTests.Output_paths_follow_the_convention</c>.
/// </summary>
internal static class GeneratedOutput
{
    /// <summary>Every generated C# source file (by suffix — the one place that knows it).</summary>
    public static IEnumerable<KeyValuePair<string, string>> CSharpSources(
        this IReadOnlyDictionary<string, string> files) =>
        files.Where(f => f.Key.EndsWith(".cs", StringComparison.Ordinal));

    /// <summary>Generated model source files (by layout — the one place that knows it).</summary>
    public static IEnumerable<KeyValuePair<string, string>> ModelSources(
        this IReadOnlyDictionary<string, string> files) =>
        files.Where(f => f.Key.StartsWith("Models/", StringComparison.Ordinal));

    /// <summary>Generated processor source files, including the shared base.</summary>
    public static IEnumerable<KeyValuePair<string, string>> ProcessorSources(
        this IReadOnlyDictionary<string, string> files) =>
        files.Where(f => f.Key.StartsWith("Processors/", StringComparison.Ordinal));

    /// <summary>The generated project file's contents (a <c>.csproj</c> is a platform requirement).</summary>
    public static string ProjectFile(this IReadOnlyDictionary<string, string> files) =>
        files.Single(f => f.Key.EndsWith(".csproj", StringComparison.Ordinal)).Value;

    /// <summary>
    /// The written source for a generated class, looked up <em>by class name</em> —
    /// the path comes from the run's own class→path map, never hardcoded.
    /// </summary>
    public static string Source(
        this IReadOnlyDictionary<string, string> files,
        GenerationResult result,
        string className) => files[result.GeneratedClasses[className]];
}
