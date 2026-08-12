using Ace.Generator.Model;
using Ace.Generator.Model.Ir;

namespace Ace.Generator;

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
/// should gate on <see cref="IRDocument.SchemaVersion"/>.
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
    /// <param name="ir">The IR document to generate from.</param>
    /// <param name="options">Host-resolved generation options.</param>
    /// <param name="writeFile">Handler invoked once per generated file (relative path + contents).</param>
    /// <param name="cancellationToken">Cooperative cancellation.</param>
    /// <returns>The run's diagnostics and emitted-file count (files themselves are streamed).</returns>
    Task<GenerationResult> GenerateAsync(
        IRDocument ir,
        GeneratorOptions options,
        GeneratedFileHandler writeFile,
        CancellationToken cancellationToken = default);
}
