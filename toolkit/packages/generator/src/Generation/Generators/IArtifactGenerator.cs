using Ace.Generator;
using Ace.Generator.Model;

namespace Ace.Generator.Generators;

/// <summary>
/// One artifact-producing stage of the C# client generator. Implementations
/// traverse the IR (or emit fixed scaffolding) and <em>lazily yield</em> the files
/// they produce (reporting diagnostics through the shared
/// <see cref="GenerationContext"/>). Yielding — rather than pushing to a sink —
/// lets the orchestrator drive writing either synchronously or asynchronously
/// without the stages knowing which, and keeps output streaming (one file built
/// and written at a time, never buffered). Stages must be pure: no disk writes,
/// no console output, no process side effects.
/// </summary>
internal interface IArtifactGenerator
{
    /// <summary>Short, human-readable stage name (used in diagnostics).</summary>
    string Name { get; }

    /// <summary>Lazily produce this stage's files, reading state from <paramref name="context"/>.</summary>
    IEnumerable<GeneratedFile> Produce(GenerationContext context);

    /// <summary>
    /// Names the output scopes this stage <em>owns</em> and wants wiped before
    /// producing, so every regeneration starts clean (full-refresh codegen
    /// semantics). Each is a relative path — a directory (e.g. <c>Models</c>) or a
    /// file — removed by the host; the stage stays filesystem-agnostic and never
    /// deletes anything itself. Default: nothing. The orchestrator runs a cleanup
    /// pass (all stages' <see cref="Clean"/>) before any <see cref="Produce"/>, so
    /// a stage sharing a directory with a peer need not clean it if the peer owns
    /// that scope.
    /// </summary>
    IEnumerable<string> Clean(GenerationContext context) => [];
}
