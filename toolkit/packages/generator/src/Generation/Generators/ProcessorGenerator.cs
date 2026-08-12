using Ace.Generator;
using Ace.Generator.Model;
using Ace.Generator.Model.Ir;

namespace Ace.Generator.Generators;

/// <summary>
/// Emits one C# processor class per operation (the IProcessor pattern) from
/// <see cref="IRDocument.Operations"/>. Scaffold: one empty <c>partial class</c>
/// per operation under <c>Processors/</c>, derived from the shared processor base.
/// </summary>
/// <remarks>
/// TODO — real emission: HTTP framing (method, path template + path/query/header
/// parameters), request body, responses + headers, error mapping, api-version
/// stamping, server/endpoint binding, and LRO / pageable semantics.
/// </remarks>
internal sealed class ProcessorGenerator : IArtifactGenerator
{
    public string Name => "processor";

    public void Generate(GenerationContext context)
    {
        string ns = context.Options.RootNamespace + ".Processors";

        foreach (IROperation operation in context.Ir.Operations)
        {
            context.CancellationToken.ThrowIfCancellationRequested();

            if (string.IsNullOrWhiteSpace(operation.Name))
            {
                context.Report(
                    DiagnosticSeverity.Warning,
                    "ace-generator/empty-operation-name",
                    "Skipped an operation with an empty name.");
                continue;
            }

            string typeName = Naming.Identifier(operation.Name) + "Processor";
            context.AddFile(
                $"Processors/{typeName}.g.cs",
                CSharpStub.Class(ns, typeName, baseType: ProcessorBaseGenerator.BaseTypeName));
        }
    }
}
