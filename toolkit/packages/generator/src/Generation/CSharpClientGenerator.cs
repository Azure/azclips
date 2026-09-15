using Ace.Generator.Model;
using Ace.Generator.Model.Ir;
using Ace.Generator.Generators;

namespace Ace.Generator;

/// <summary>
/// The C# client generator. Orchestrates the artifact generators — models,
/// processors, the processor base, and the project stub — streaming each file
/// they produce to the caller's <see cref="GeneratedFileHandler"/>.
/// </summary>
/// <remarks>
/// Pure: no disk writes, console output, or process side effects. Each stage
/// contributes files and diagnostics through a shared <see cref="GenerationContext"/>,
/// which streams files to the handler on the fly (never buffering contents); the
/// stages themselves are currently scaffolds (see each <see cref="IArtifactGenerator"/>).
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
    public async Task<GenerationResult> GenerateAsync(
        IRDocument ir,
        GeneratorOptions options,
        GeneratedFileHandler writeFile,
        RemoveHandler? removeScope = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(ir);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(writeFile);

        var diagnostics = new List<GeneratorDiagnostic>();
        var context = new GenerationContext(ir, options, diagnostics, cancellationToken);

        // Cleanup pass (before any produce): each stage names the output scopes it
        // owns; the host wipes them so every regeneration starts clean. Running
        // first guarantees a later produce never removes freshly written output.
        if (removeScope is not null)
        {
            foreach (IArtifactGenerator stage in _stages)
            {
                cancellationToken.ThrowIfCancellationRequested();
                foreach (string scope in stage.Clean(context))
                {
                    if (!scope.TryNormalize(out string normalized, out string error))
                    {
                        context.Report(
                            DiagnosticSeverity.Error,
                            "ace-generator/invalid-clean-scope",
                            $"A generator named an invalid clean scope: {error}.");
                        continue;
                    }

                    await removeScope(normalized, cancellationToken);
                }
            }
        }

        var emitted = new HashSet<string>(StringComparer.Ordinal);
        var generatedClasses = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (IArtifactGenerator stage in _stages)
        {
            cancellationToken.ThrowIfCancellationRequested();
            foreach (GeneratedFile file in stage.Produce(context))
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!TryAccept(context, emitted, file, out string path))
                {
                    continue;
                }

                await writeFile(path, file.Contents, cancellationToken);

                if (file.ClassName is { Length: > 0 } className && !generatedClasses.TryAdd(className, path))
                {
                    context.Report(
                        DiagnosticSeverity.Warning,
                        "ace-generator/duplicate-class",
                        $"Two generators produced the class '{className}'; keeping the first ('{generatedClasses[className]}').");
                }
            }
        }

        return new GenerationResult(diagnostics, generatedClasses);
    }

    /// <summary>
    /// Validate a produced file's path (must be relative and non-escaping) and
    /// enforce cross-stage uniqueness. Reports a diagnostic and returns false when
    /// the file should be dropped.
    /// </summary>
    private static bool TryAccept(
        GenerationContext context,
        HashSet<string> emitted,
        GeneratedFile file,
        out string path)
    {
        if (!file.Path.TryNormalize(out path, out string error))
        {
            context.Report(
                DiagnosticSeverity.Error,
                "ace-generator/invalid-output-path",
                $"A generator produced an invalid output path: {error}.");
            return false;
        }

        if (!emitted.Add(path))
        {
            context.Report(
                DiagnosticSeverity.Warning,
                "ace-generator/duplicate-file",
                $"Two generators produced the file '{path}'; keeping the first.");
            return false;
        }

        return true;
    }
}
