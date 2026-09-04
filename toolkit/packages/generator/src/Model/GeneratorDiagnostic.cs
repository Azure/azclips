namespace Ace.Generator.Model;

/// <summary>Severity of a <see cref="GeneratorDiagnostic"/>.</summary>
public enum DiagnosticSeverity
{
    /// <summary>Informational; does not affect success.</summary>
    Info,

    /// <summary>A problem that does not fail generation unless escalated by the host.</summary>
    Warning,

    /// <summary>A failure; generation is considered unsuccessful.</summary>
    Error,
}

/// <summary>
/// A structured diagnostic produced during generation. Mirrors the backbone's
/// <c>CompilerDiagnostic</c> so a host can surface generator problems uniformly.
/// Diagnostics are data, never printed by the generator itself.
/// </summary>
/// <param name="Severity">How serious the diagnostic is.</param>
/// <param name="Code">Stable machine-readable identifier (e.g. <c>ace-generator/empty-model-name</c>).</param>
/// <param name="Message">Human-readable description.</param>
public sealed record GeneratorDiagnostic(DiagnosticSeverity Severity, string Code, string Message);
