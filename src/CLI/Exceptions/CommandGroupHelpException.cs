using Core.Cli.Parser;

namespace Core.Cli.Exceptions;

public sealed class CommandGroupHelpException(ExtensionParser extensionParser, string prefix) : ParserException("Group help requested.")
{
    public ExtensionParser ExtensionParser { get; } = extensionParser;
    public string Prefix { get; } = prefix;
}
