using System.Text.Json;

namespace Ace.Generator.Model;

/// <summary>
/// App-resolved generation settings: what the compiler hands the generator besides
/// the document.
/// </summary>
/// <remarks>
/// Placement (root namespace, project name, folders) is not an option. It is a
/// decision resolved into the document's <c>TransformedIR.Target</c>.
/// </remarks>
public sealed record GeneratorOptions
{
    /// <summary>
    /// The <c>csharp</c> stage's own options (<c>stages.csharp.options</c>), if any. Opaque
    /// for now; concrete named options are read from it as the generator grows.
    /// </summary>
    public JsonElement? Config { get; init; }
}
