using Ace.Generator.Model;
using Ace.Generator.Model.Ir;

namespace Ace.Generator;

/// <summary>
/// A C# client generator: consumes a validated IR document and returns generated
/// source files. Implementations must be pure — no disk writes, no console output,
/// no process side effects — so a CLI and a web service can both host them.
/// </summary>
/// <remarks>
/// This is the C# counterpart to the backbone's TypeScript <c>Generator</c> seam
/// (<c>generate(ir, ctx) =&gt; { files }</c>). The IR is expected to be already
/// validated against its schema version; callers should gate on
/// <see cref="IRDocument.SchemaVersion"/>.
/// </remarks>
public interface IClientGenerator
{
    /// <summary>Stable identifier for this generator (used in diagnostics/config).</summary>
    string Name { get; }

    /// <summary>
    /// Generate C# source files from <paramref name="ir"/>.
    /// </summary>
    /// <param name="ir">The IR document to generate from.</param>
    /// <param name="options">Host-resolved generation options.</param>
    /// <param name="cancellationToken">Cooperative cancellation.</param>
    /// <returns>The generated files plus any diagnostics.</returns>
    GenerationResult Generate(IRDocument ir, GeneratorOptions options, CancellationToken cancellationToken = default);
}
