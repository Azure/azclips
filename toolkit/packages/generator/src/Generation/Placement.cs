using Ace.Generator.Model;
using Ace.Generator.Model.Ir;

namespace Ace.Generator.Generators;

/// <summary>
/// Where generated code lands and what it is called.
/// </summary>
/// <remarks>
/// The namespace and the project name come from <see cref="TransformedIR.Target"/>, which the
/// <c>seed-ir</c> stage resolved from the document facts. They are *decisions*, and decisions
/// belong in the document where a captured IR can show them — the generator only spells them.
/// The folders inside the project are not decisions: they are this generator's own convention,
/// so they are fixed here.
/// <para>
/// There is no fallback: <see cref="CSharpClientGenerator"/> rejects a document without
/// a target (<c>ace-generator/missing-target</c>) before anything is placed.
/// </para>
/// </remarks>
internal sealed class Placement
{
    public Placement(TransformedIR ir)
    {
        IRTarget? target = ir.Target;
        if (target is not { RootNamespace.Count: > 0 })
        {
            throw new InvalidOperationException(
                "The document has no target. The seed-ir stage resolves it from the document facts.");
        }

        RootNamespace = string.Join(".", target.RootNamespace);

        ProjectName = !string.IsNullOrWhiteSpace(target.ProjectName) ? target.ProjectName : RootNamespace;

        var occupiedNames = ir.Models
            .Where(model => !string.IsNullOrWhiteSpace(model.Name))
            .Select(model => model.Name.Identifier())
            .Concat(ir.Commands.Select(command => command.Name + "Processor"))
            .ToHashSet(StringComparer.Ordinal);
        PaginationHelperTypeName = UnusedName("Pagination", occupiedNames);

        var parameterNames = ir.Commands
            .SelectMany(command => command.Parameters)
            .Select(parameter => parameter.Name)
            .Concat(ServerSupport.Inputs(ir))
            .ToHashSet(StringComparer.Ordinal);
        ExecuteMethodName = UnusedName("ExecuteAsync", parameterNames);
    }

    /// <summary>Root namespace of the generated code, dotted.</summary>
    public string RootNamespace { get; }

    /// <summary>Assembly and package name. Free to differ from the namespace.</summary>
    public string ProjectName { get; }

    /// <summary>Relative directory for model types.</summary>
    public string ModelsDirectory => "Models";

    /// <summary>Relative directory for processors and their shared base.</summary>
    public string ProcessorsDirectory => "Processors";

    /// <summary>
    /// Namespace for model types. The folder name extends the root namespace, the usual C#
    /// convention.
    /// </summary>
    public string ModelsNamespace => $"{RootNamespace}.{ModelsDirectory}";

    /// <summary>Namespace for processors, derived the same way.</summary>
    public string ProcessorsNamespace => $"{RootNamespace}.{ProcessorsDirectory}";

    /// <summary>Helper name that does not shadow a service model or duplicate a class-map key.</summary>
    public string PaginationHelperTypeName { get; }

    /// <summary>Fully qualified helper reference, unaffected by processor parameter names.</summary>
    public string PaginationHelperTypeReference => $"global::{ProcessorsNamespace}.{PaginationHelperTypeName}";

    /// <summary>Shared execution-hook name that preserves command and endpoint parameter names.</summary>
    public string ExecuteMethodName { get; }

    /// <summary>Output path for one model file.</summary>
    public string ModelFile(string typeName) => $"{ModelsDirectory}/{typeName}.cs";

    /// <summary>Output path for one processor file.</summary>
    public string ProcessorFile(string typeName) => $"{ProcessorsDirectory}/{typeName}.cs";

    private static string UnusedName(string name, ISet<string> occupiedNames)
    {
        string candidate = name;
        for (int suffix = 2; occupiedNames.Contains(candidate); suffix++)
        {
            candidate = $"{name}{suffix}";
        }
        return candidate;
    }
}
