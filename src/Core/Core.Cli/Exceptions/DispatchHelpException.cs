using System;
using Core.Cli.Parser;

namespace Core.Cli.Exceptions;

public class DispatchHelpException(ExtensionDispatcher extensionDispatcher): ParserException("Dispatch help requested.")
{
    public ExtensionDispatcher ExtensionDispatcher { get; } = extensionDispatcher;
}
