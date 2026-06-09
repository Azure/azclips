using Core.Cli.Parser;

namespace Core.Cli.Exceptions;

public sealed class RuntimeExtensionHelpException(
    string extensionName,
    IReadOnlyList<string> path,
    bool isCommandHelp)
    : ParserException("Runtime extension help requested.")
{
    public string ExtensionName { get; } = extensionName;

    public IReadOnlyList<string> Path { get; } = path;

    public bool IsCommandHelp { get; } = isCommandHelp;
}