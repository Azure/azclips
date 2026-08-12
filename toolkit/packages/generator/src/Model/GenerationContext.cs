using Ace.Generator.Model.Ir;

namespace Ace.Generator.Model;

/// <summary>
/// Read-only run state shared with the artifact generators: the IR document, the
/// resolved options, cancellation, and the diagnostic sink. Stages read from it
/// and report diagnostics through it, but <em>produce files by yielding</em>
/// (see <c>IArtifactGenerator</c>) — the orchestrator, not the context, streams
/// those files to the caller's handler, so the context never touches file output.
/// </summary>
internal sealed class GenerationContext
{
    private readonly List<GeneratorDiagnostic> _diagnostics;

    internal GenerationContext(
        IRDocument ir,
        GeneratorOptions options,
        List<GeneratorDiagnostic> diagnostics,
        CancellationToken cancellationToken)
    {
        Ir = ir;
        Options = options;
        _diagnostics = diagnostics;
        CancellationToken = cancellationToken;
    }

    /// <summary>The IR document being generated from.</summary>
    public IRDocument Ir { get; }

    /// <summary>Host-resolved generation options.</summary>
    public GeneratorOptions Options { get; }

    /// <summary>Cooperative cancellation for the run.</summary>
    public CancellationToken CancellationToken { get; }

    /// <summary>Record a diagnostic.</summary>
    public void Report(GeneratorDiagnostic diagnostic) => _diagnostics.Add(diagnostic);

    /// <summary>Record a diagnostic from its parts.</summary>
    public void Report(DiagnosticSeverity severity, string code, string message) =>
        _diagnostics.Add(new GeneratorDiagnostic(severity, code, message));
}
