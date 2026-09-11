namespace Ace.Generator.Model;

/// <summary>
/// Result of a generation run. Generated files are <em>streamed</em> to the
/// caller's <c>GeneratedFileHandler</c> as they are produced, so their contents
/// are not carried here; this surfaces the small, aggregate outcome — structured
/// diagnostics, the map of generated classes to their output paths, and overall
/// success.
/// </summary>
public sealed class GenerationResult
{
    /// <summary>Diagnostics collected during the run.</summary>
    public IReadOnlyList<GeneratorDiagnostic> Diagnostics { get; }

    /// <summary>
    /// Generated classes, keyed by C# class name, mapped to the output file path
    /// (relative to the output base directory) each was written to. Non-type
    /// artifacts (e.g. the project file) are not included.
    /// </summary>
    public IReadOnlyDictionary<string, string> GeneratedClasses { get; }

    /// <summary>True when no error-severity diagnostics were reported.</summary>
    public bool Success
    {
        get
        {
            foreach (GeneratorDiagnostic diagnostic in Diagnostics)
            {
                if (diagnostic.Severity == DiagnosticSeverity.Error)
                {
                    return false;
                }
            }

            return true;
        }
    }

    /// <summary>Create a result from the run's diagnostics and generated-class map.</summary>
    public GenerationResult(
        IReadOnlyList<GeneratorDiagnostic> diagnostics,
        IReadOnlyDictionary<string, string> generatedClasses)
    {
        Diagnostics = diagnostics;
        GeneratedClasses = generatedClasses;
    }
}
