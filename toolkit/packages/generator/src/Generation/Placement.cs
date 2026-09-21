using Ace.Generator.Model;
using Ace.Generator.Model.Ir;

namespace Ace.Generator.Generators;

/// <summary>
/// Where generated code lands and what it is called.
/// </summary>
/// <remarks>
/// Read from <see cref="IRDocument.Target"/>, which a transform resolved from
/// generation-time configuration. Placement is a *decision*, and decisions belong in
/// the document where a captured IR can show them — the generator only spells them.
/// <para>
/// Falls back to <see cref="GeneratorOptions.RootNamespace"/> and the conventional
/// folder names when a document carries no target. That keeps hand-built documents
/// (tests, embedders) working; a document from the real pipeline always has one,
/// because <c>parseTransformedIR</c> requires it.
/// </para>
/// </remarks>
internal sealed class Placement
{
    private const string DefaultModelsDirectory = "Models";
    private const string DefaultProcessorsDirectory = "Processors";

    public Placement(IRDocument ir, GeneratorOptions options)
    {
        IRTarget? target = ir.Target;

        RootNamespace = target is { RootNamespace.Count: > 0 }
            ? string.Join(".", target.RootNamespace)
            : options.RootNamespace;

        ProjectName = !string.IsNullOrWhiteSpace(target?.ProjectName) ? target.ProjectName : RootNamespace;

        ModelsDirectory = Directory(target?.Directories?.Models, DefaultModelsDirectory);
        ProcessorsDirectory = Directory(target?.Directories?.Processors, DefaultProcessorsDirectory);
    }

    /// <summary>Root namespace of the generated code, dotted.</summary>
    public string RootNamespace { get; }

    /// <summary>Assembly and package name. Free to differ from the namespace.</summary>
    public string ProjectName { get; }

    /// <summary>Relative directory for model types.</summary>
    public string ModelsDirectory { get; }

    /// <summary>Relative directory for processors and their shared base.</summary>
    public string ProcessorsDirectory { get; }

    /// <summary>
    /// Namespace for model types. The trailing segment follows the directory, so a
    /// configured folder and its namespace stay in step — the usual C# convention.
    /// </summary>
    public string ModelsNamespace => $"{RootNamespace}.{NamespaceSegment(ModelsDirectory)}";

    /// <summary>Namespace for processors, derived the same way.</summary>
    public string ProcessorsNamespace => $"{RootNamespace}.{NamespaceSegment(ProcessorsDirectory)}";

    /// <summary>Output path for one model file.</summary>
    public string ModelFile(string typeName) => $"{ModelsDirectory}/{typeName}.cs";

    /// <summary>Output path for one processor file.</summary>
    public string ProcessorFile(string typeName) => $"{ProcessorsDirectory}/{typeName}.cs";

    private static string Directory(string? configured, string fallback) =>
        string.IsNullOrWhiteSpace(configured) ? fallback : configured.Replace('\\', '/').Trim('/');

    /// <summary>
    /// The namespace segment a directory contributes: its last path component, made a
    /// legal identifier. A nested output folder (<c>src/Models</c>) still yields
    /// <c>Models</c> rather than an illegal segment.
    /// </summary>
    private static string NamespaceSegment(string directory)
    {
        string[] parts = directory.Split('/', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length > 0 ? parts[^1].Identifier() : directory.Identifier();
    }
}
