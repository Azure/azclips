using Ace.Generator;
using Ace.Generator.Model;
using Ace.Generator.Model.Ir;

namespace Ace.Generator.Generators;

/// <summary>
/// Emits C# model types from <see cref="IRDocument.Models"/>. Faithful
/// ("garbage in, garbage out") for now: one class per model with every property
/// the IR carries, mapped literally to C# — no inheritance, flattening,
/// polymorphism, or enum emission yet.
/// </summary>
/// <remarks>
/// TODO — richer emission: enums (<see cref="IRDocument.Enums"/>), inheritance +
/// overrides (deduplicate inherited properties), property flattening, polymorphism
/// (<c>[JsonPolymorphic]</c>), and visibility/access shaping.
/// </remarks>
internal sealed class ModelGenerator : IArtifactGenerator
{
    public string Name => "model";

    public IEnumerable<GeneratedFile> Produce(GenerationContext context)
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
            yield return new GeneratedFile($"Models/{typeName}.g.cs", ModelEmitter.Emit(ns, typeName, model), typeName);
        }
    }
}
