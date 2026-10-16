using System.Text.Json;

using Ace.Generator.Model;
using Ace.Generator.Model.Ir;

namespace Ace.Generator;

/// <summary>
/// Receives one generated file the moment it is produced, returning a task the
/// generator awaits before producing the next (so a slow sink applies natural
/// backpressure and nothing is buffered).
///
/// <paramref name="relativePath"/> is <em>relative</em> to the caller's output
/// base directory (using <c>/</c> separators, never absolute, never escaping via
/// <c>..</c>); the generator is filesystem-agnostic and never knows the base
/// directory — the handler owns it (e.g. a filesystem writer that joins
/// <c>baseDir + relativePath</c>). So the whole generated output is one
/// relocatable folder tree.
///
/// This is the C# counterpart to the TypeScript backbone's <c>OutputDelegate</c>:
/// the generator decides <em>when</em> each artifact is emitted; the handler
/// decides <em>where/how</em> it lands (an orchestrator writing to disk, or a test
/// collecting in memory). A synchronous sink can simply return a completed task.
/// </summary>
/// <param name="relativePath">Output path relative to the caller's base directory.</param>
/// <param name="contents">The generated file's full text.</param>
/// <param name="cancellationToken">Cancellation for the write.</param>
public delegate ValueTask GeneratedFileHandler(
    string relativePath,
    string contents,
    CancellationToken cancellationToken);

/// <summary>
/// Removes one output scope — a directory or file, relative to the caller's output
/// base — before generation writes fresh files. The "remove" counterpart to
/// <see cref="GeneratedFileHandler"/>: the generator names <em>what</em> to wipe;
/// the handler (host) owns the filesystem and decides <em>how</em>, so the
/// generator never touches disk. Same relative-path contract as the write handler
/// (using <c>/</c> separators, never absolute, never escaping via <c>..</c>).
/// </summary>
/// <param name="relativePath">Scope to remove, relative to the output base directory.</param>
/// <param name="cancellationToken">Cancellation for the removal.</param>
public delegate ValueTask RemoveHandler(
    string relativePath,
    CancellationToken cancellationToken);

/// <summary>
/// A C# client generator: consumes a validated IR document and <em>streams</em>
/// generated source files to a caller-supplied handler. Implementations must be
/// pure — no disk writes, no console output, no process side effects — so a CLI
/// and a web service can both host them; where/how files land is the handler's
/// concern.
/// </summary>
/// <remarks>
/// This is the C# counterpart to the backbone's TypeScript <c>Generator</c> seam.
/// The IR is expected to be already validated against its schema version; callers
/// should gate on <see cref="TransformedIR.SchemaVersion"/>.
/// </remarks>
public interface IClientGenerator
{
    /// <summary>Stable identifier for this generator (used in diagnostics/config).</summary>
    string Name { get; }

    /// <summary>
    /// Generate C# source files from <paramref name="ir"/>, streaming each file to
    /// <paramref name="writeFile"/> and awaiting it before producing the next (so a
    /// slow sink applies backpressure and nothing is buffered).
    /// </summary>
    /// <remarks>
    /// When <paramref name="removeScope"/> is supplied, a <em>cleanup pass</em> runs
    /// first: each stage names the output scopes it owns and they are wiped via the
    /// handler before any file is written, so every regeneration starts clean
    /// (full-refresh codegen semantics). With no handler, nothing is removed.
    /// </remarks>
    /// <param name="ir">The IR document to generate from.</param>
    /// <param name="config">The <c>csharp</c> stage's own stage config, as written, if any.</param>
    /// <param name="writeFile">Handler invoked once per generated file (relative path + contents).</param>
    /// <param name="removeScope">Optional handler invoked once per owned scope to wipe, before writing.</param>
    /// <param name="cancellationToken">Cooperative cancellation.</param>
    /// <returns>The run's diagnostics and emitted-file count (files themselves are streamed).</returns>
    Task<GenerationResult> GenerateAsync(
        TransformedIR ir,
        JsonElement? config,
        GeneratedFileHandler writeFile,
        RemoveHandler? removeScope = null,
        CancellationToken cancellationToken = default);
}
