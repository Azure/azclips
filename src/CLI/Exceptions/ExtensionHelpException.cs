using Core.Cli.Parser;

namespace Core.Cli.Exceptions;

public sealed class ExtensionHelpException(ExtensionParser extensionParser) : ParserException("Extension help requested.")
{
    public ExtensionParser ExtensionParser { get; } = extensionParser;
}