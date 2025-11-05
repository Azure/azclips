using Core.Cli.Parser;

namespace Core.Cli.Help.Templates;

public class CommandHelpTemplate
{

    public uint Indent { get; set; } = 4;

    public string[] Render(
        CommandParser commandParser,
        uint? terminalWidth = null
    )
    {
        throw new NotImplementedException();
    }

}
