using Ace.Generator.Model;

namespace Ace.Generator.Generators;

/// <summary>
/// One artifact-producing stage of the C# client generator. Implementations
/// traverse the IR (or emit fixed scaffolding) and contribute files + diagnostics
/// through the shared <see cref="GenerationContext"/>. They must be pure: no disk
/// writes, no console output, no process side effects.
/// </summary>
internal interface IArtifactGenerator
{
    /// <summary>Short, human-readable stage name (used in diagnostics).</summary>
    string Name { get; }

    /// <summary>Produce this stage's artifacts into <paramref name="context"/>.</summary>
    void Generate(GenerationContext context);
}
