using Core.Cli.Parser;

namespace Core.Cli.Exceptions;

/// <summary>
/// Thrown by <see cref="ExtensionDispatcher"/> when the user requested help for a
/// verb-scoped group or command (e.g. <c>azv2 vm --help</c>, <c>azv2 help vm create</c>).
/// Carries the verb (not the package name) since help is a per-verb surface.
/// </summary>
public sealed class RuntimeExtensionHelpException(
    string verb,
    IReadOnlyList<string> path,
    bool isCommandHelp)
    : ParserException("Runtime extension help requested.")
{
    /// <summary>CLI verb the help target lives under (e.g. <c>"vm"</c>, <c>"login"</c>).</summary>
    public string Verb { get; } = verb;

    /// <summary>Full path including the verb, e.g. <c>["vm", "create"]</c>.</summary>
    public IReadOnlyList<string> Path { get; } = path;

    public bool IsCommandHelp { get; } = isCommandHelp;
}