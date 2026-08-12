using Ace.Generator.Model.Ir;

namespace Ace.Generator.Model;

/// <summary>
/// Shared state threaded through the artifact generators during one run. Owns the
/// output file map and diagnostic list so each stage contributes into the same
/// buffers; the orchestrator turns them into the final <see cref="GenerationResult"/>.
/// </summary>
internal sealed class GenerationContext
{
    private readonly Dictionary<string, string> _files;
    private readonly List<GeneratorDiagnostic> _diagnostics;

    internal GenerationContext(
        IRDocument ir,
        GeneratorOptions options,
        Dictionary<string, string> files,
        List<GeneratorDiagnostic> diagnostics,
        CancellationToken cancellationToken)
    {
        Ir = ir;
        Options = options;
        _files = files;
        _diagnostics = diagnostics;
        CancellationToken = cancellationToken;
    }

    /// <summary>The IR document being generated from.</summary>
    public IRDocument Ir { get; }

    /// <summary>Host-resolved generation options.</summary>
    public GeneratorOptions Options { get; }

    /// <summary>Cooperative cancellation for the run.</summary>
    public CancellationToken CancellationToken { get; }

    /// <summary>
    /// Add a generated file keyed by path relative to the output root. Reports a
    /// warning and keeps the first entry if two stages produce the same path.
    /// </summary>
    public void AddFile(string path, string contents)
    {
        if (!_files.TryAdd(path, contents))
        {
            Report(
                DiagnosticSeverity.Warning,
                "ace-generator/duplicate-file",
                $"Two generators produced the file '{path}'; keeping the first.");
        }
    }

    /// <summary>Record a diagnostic.</summary>
    public void Report(GeneratorDiagnostic diagnostic) => _diagnostics.Add(diagnostic);

    /// <summary>Record a diagnostic from its parts.</summary>
    public void Report(DiagnosticSeverity severity, string code, string message) =>
        _diagnostics.Add(new GeneratorDiagnostic(severity, code, message));
}
