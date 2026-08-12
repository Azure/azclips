using Ace.Generator.Model;
using Ace.Generator.Model.Ir;

namespace Ace.Generator.Generators;

/// <summary>
/// Emits C# model types from <see cref="IRDocument.Models"/> (and, later,
/// <see cref="IRDocument.Enums"/>). Scaffold: one empty <c>partial class</c> per
/// model under <c>Models/</c>.
/// </summary>
/// <remarks>
/// TODO — real emission: properties (name vs wireName, optional ⊕ nullable,
/// visibility), enums, inheritance + overrides, property flattening, collections
/// and dictionaries, polymorphism, docs/summary.
/// </remarks>
internal sealed class ModelGenerator : IArtifactGenerator
{
    public string Name => "model";

    public void Generate(GenerationContext context)
    {
        string ns = context.Options.RootNamespace + ".Models";

        foreach (IRModel model in context.Ir.Models)
        {
            context.CancellationToken.ThrowIfCancellationRequested();

            if (string.IsNullOrWhiteSpace(model.Name))
            {
                context.Report(
                    DiagnosticSeverity.Warning,
                    "ace-generator/empty-model-name",
                    "Skipped a model with an empty name.");
                continue;
            }

            string typeName = Naming.Identifier(model.Name);
            context.AddFile($"Models/{typeName}.g.cs", CSharpStub.Class(ns, typeName));
        }
    }
}
