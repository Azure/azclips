using Ace.Generator.Model.Ir;

namespace Ace.Generator.Tests;

/// <summary>
/// Targets for hand-built documents.
/// </summary>
/// <remarks>
/// A document from the pipeline carries the target the <c>seed-ir</c> stage resolved, and the
/// generator has no fallback without one. A test that builds its document directly sets the
/// target itself, here with seed-ir's default: the project named after the namespace.
/// </remarks>
internal static class Targets
{
    /// <summary>A target rooted at <paramref name="rootNamespace"/> (dotted).</summary>
    public static IRTarget For(string rootNamespace) => new()
    {
        RootNamespace = rootNamespace.Split('.'),
        ProjectName = rootNamespace,
    };
}
