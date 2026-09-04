using Ace.Generator.Model.Ir;

namespace Ace.Generator;

/// <summary>
/// Naming shared between the processor files and the extension index that references them, so a
/// change to one cannot silently leave the other pointing at a type that no longer exists.
/// </summary>
internal static class ProcessorNaming
{
    /// <summary>
    /// Unique C# class name for a processor: <c>{Verb}{Group}Processor</c>, where the group is the
    /// last segment of the operation's <see cref="IRId.Namespace"/> (the operation interface, e.g.
    /// <c>ElasticSans</c>) — operation <see cref="IROperation.Name"/>s alone are not unique across
    /// groups. Falls back to <c>{Verb}Processor</c> when the operation carries no id.
    /// </summary>
    public static string TypeName(IROperation operation)
    {
        string group = operation.Id?.Namespace is { Count: > 0 } ns ? ns[^1] : string.Empty;
        return (operation.Name + group).Identifier() + "Processor";
    }
}
