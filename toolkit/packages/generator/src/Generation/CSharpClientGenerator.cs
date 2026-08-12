using Ace.Generator.Model;
using Ace.Generator.Model.Ir;
using Ace.Generator.Generators;

namespace Ace.Generator;

/// <summary>
/// The C# client generator. Orchestrates the artifact generators — models,
/// processors, the processor base, and the project stub — and merges their output
/// into one <see cref="GenerationResult"/>.
/// </summary>
/// <remarks>
/// Pure: no disk writes, console output, or process side effects. Each stage
/// contributes files and diagnostics through a shared <see cref="GenerationContext"/>;
/// the stages themselves are currently scaffolds (see each <see cref="IArtifactGenerator"/>).
/// </remarks>
public sealed class CSharpClientGenerator : IClientGenerator
{
    private readonly IReadOnlyList<IArtifactGenerator> _stages =
    [
        new ModelGenerator(),
        new ProcessorGenerator(),
        new ProcessorBaseGenerator(),
        new ProjectStubGenerator(),
    ];

    /// <inheritdoc />
    public string Name => "csharp";

    /// <inheritdoc />
    public GenerationResult Generate(IRDocument ir, GeneratorOptions options, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(ir);
        ArgumentNullException.ThrowIfNull(options);

        var files = new Dictionary<string, string>(StringComparer.Ordinal);
        var diagnostics = new List<GeneratorDiagnostic>();
        var context = new GenerationContext(ir, options, files, diagnostics, cancellationToken);

        foreach (IArtifactGenerator stage in _stages)
        {
            cancellationToken.ThrowIfCancellationRequested();
            stage.Generate(context);
        }

        return new GenerationResult(files, diagnostics);
    }
}
