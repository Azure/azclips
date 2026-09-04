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
    /// operation's <see cref="IRId.Interface"/> (e.g. <c>ElasticSans</c>) — operation
    /// <see cref="IROperation.Name"/>s alone are not unique across groups. Namespace-level
    /// operations have no interface group and emit <c>{Verb}Processor</c>.
    /// </summary>
    public static string TypeName(IROperation operation)
    {
        string group = operation.Id?.Interface ?? string.Empty;
        return (operation.Name + group).Identifier() + "Processor";
    }
}
