namespace Ace.Generator.Model;

/// <summary>
/// Result of a generation run. Mirrors the backbone's <c>GeneratorOutput</c>
/// (a map of generated files) and additionally surfaces structured diagnostics.
/// Files are returned, never written to disk by the generator.
/// </summary>
public sealed class GenerationResult
{
    /// <summary>
    /// Generated files keyed by path relative to the output root.
    /// </summary>
    public IReadOnlyDictionary<string, string> Files { get; }

    /// <summary>
    /// Diagnostics collected during the run.
    /// </summary>
    public IReadOnlyList<GeneratorDiagnostic> Diagnostics { get; }

    /// <summary>
    /// True when no error-severity diagnostics were reported.
    /// </summary>
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

    /// <summary>Create a result from a file map and diagnostics.</summary>
    public GenerationResult(
        IReadOnlyDictionary<string, string> files,
        IReadOnlyList<GeneratorDiagnostic> diagnostics)
    {
        Files = files;
        Diagnostics = diagnostics;
    }
}
