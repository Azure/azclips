using System;
using Core.Cli.Exceptions;
using Core.Cli.Help.Templates;
using Core.Cli.Parser;

namespace Core.Cli;

public class Program
{
    public static async Task<int> Main(string[] args)
    {
        try
        {
            var dispatcher = new ExtensionDispatcher();
            var command = await dispatcher.ParseAsync<object>(args);
            var result = await command.ExecuteAsync();
            Console.WriteLine(result);
            return 0;
        }
        catch (DispatchHelpException)
        {
            Console.WriteLine("Command Line Interface Help");
            return 0;
        }
        catch (CommandGroupHelpException ex)
        {
            var executableName = Path.GetFileNameWithoutExtension(Environment.ProcessPath);
            var template = new CommandGroupHelpTemplate();
            foreach (var line in template.Render(ex.ExtensionParser, executableName, ex.Prefix, TerminalWidth()))
            {
                Console.WriteLine(line);
            }
            return 0;
        }
        catch (CommandHelpException)
        {
            Console.WriteLine("Command Help");
            return 0;
        }
        catch (ParserException ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
            return 1;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Unhandled exception: {ex}");
            return 1;
        }
    }

    static uint? TerminalWidth()
    {
        try
        {
            var width = (uint)Console.WindowWidth;
            return width > 0 ? width : null;
        }
        catch
        {
            return null;
        }
    }
}
