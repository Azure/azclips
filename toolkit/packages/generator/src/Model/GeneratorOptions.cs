using System.Text.Json;

namespace Ace.Generator.Model;

/// <summary>
/// App-resolved generation settings. Mirrors the role of the backbone's
/// <c>CompileConfig</c> on the TypeScript side: the host resolves configuration
/// and hands it to the generator, which reads its own named fields.
/// </summary>
/// <remarks>
/// Placement (root namespace, project name, folders) is not an option. It is a
/// decision resolved into the document's <c>TransformedIR.Target</c>.
/// </remarks>
public sealed record GeneratorOptions
{
    /// <summary>
    /// Raw, host-resolved configuration object (the backbone's <c>CompileConfig</c>),
    /// if any. Opaque for now; concrete named options are read from it as the
    /// generator grows.
    /// </summary>
    public JsonElement? Config { get; init; }
}
