using Ace.Generator;
using Ace.Generator.Model;
using Ace.Generator.Model.Ir;

namespace Ace.Generator.Generators;

/// <summary>
/// Emits the shared base class every processor derives from. This stage is
/// "template-like": mostly fixed code that wires the runtime dependencies (HTTP
/// pipeline, credential/auth, endpoint, api-version, (de)serialization) rather
/// than being IR-driven. Scaffold: an empty <c>abstract partial class</c>.
/// </summary>
/// <remarks>
/// TODO — real base: constructor injecting runtime dependencies; request send +
/// retry; response deserialization + error mapping; LRO / pageable helpers.
/// Prefer fixed string templates here over hand-built Roslyn trees, since the
/// content is largely static.
/// </remarks>
internal sealed class ProcessorBaseGenerator : IArtifactGenerator
{
    /// <summary>Name of the base type that generated processors derive from.</summary>
    public const string BaseTypeName = "ProcessorBase";

    public string Name => "base";

    public void Generate(GenerationContext context)
    {
        context.CancellationToken.ThrowIfCancellationRequested();

        string ns = context.Options.RootNamespace + ".Processors";
        context.AddFile(
            $"Processors/{BaseTypeName}.g.cs",
            CSharpStub.Class(ns, BaseTypeName, isAbstract: true));
    }
}
