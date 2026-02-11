using System;
using Core.Cli.Parser;

namespace Core.Cli.Exceptions;

public class DispatchHelpException(CommandRouter router): ParserException("Dispatch help requested.")
{
    public CommandRouter Router { get; } = router;
}
