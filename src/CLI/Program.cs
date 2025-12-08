using System;
using Core.Abstractions;
using Core.Cli.Exceptions;
using Core.Cli.Parser;
using SharedContracts;

namespace Core.Cli;

public class Program
{
    public static int Main(string[] args)
    {
        try
        {
            var dispatcher = new ExtensionDispatcher();
            //var command = dispatcher.Parse(args);
            ICommandInvocation invocation = dispatcher.Parse(args);
            string result = Engine.Engine.GetCommand(invocation).Process(true);
            Console.WriteLine(result);
            return 0;
        }
        catch (DispatchHelpException)
        {
            Console.WriteLine("Command Line Interface Help");
            return 0;
        }
        //catch (CommandGroupHelpException ex)
        //{
        //    var executableName = Path.GetFileNameWithoutExtension(Environment.ProcessPath);
        //    var template = new CommandGroupHelpTemplate();
        //    foreach (var line in template.Render(ex.ExtensionParser, executableName, ex.Prefix, TerminalWidth()))
        //    {
        //        Console.WriteLine(line);
        //    }
        //    return 0;
        //}
        //catch (CommandHelpException)
        //{
        //    Console.WriteLine("Command Help");
        //    return 0;
        //}
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
