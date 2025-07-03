namespace AzTool
{
    using System;
    using System.Linq;
    using System.Reflection;
    using System.Threading.Tasks;
    using SharedContracts;

    // Main program class for the Az tool
    public class Program
    {
        public static Invocation CreateInvocation(string[] args)
        {
            var helpFlags = new[] { "--help", "-h", "help", "?", "-?" };
            if (args.Length == 0 || helpFlags.Contains(args[0], StringComparer.OrdinalIgnoreCase))
            {
                var invocationInfo = new CLIInvocationInfo
                {
                    RawCommand = args,
                    ShowHelp = true,
                };
                return new AppHelpInvocation(invocationInfo);
            }
            var invoker = new Invoker();
            return invoker.CreateInvocation(args);
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

        public static async Task<int> Main(string[] args)
        {
            try
            {
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Starting Az tool with arguments: {string.Join(" ", args)}");
                var Invocation = CreateInvocation(args);
                switch (Invocation)
                {
                    case AppHelpInvocation helpInvocation:
                        var helpTemplate = new AppHelpTemplate();
                        foreach (var line in helpTemplate.Render(Array.Empty<Assembly>(), TerminalWidth()))
                        {
                            Console.WriteLine(line);
                        }
                        return 0; // Return success code
                    case ModuleHelpInvocation moduleHelpInvocation:
                        var moduleHelpTemplate = new ModuleHelpTemplate();
                        foreach (var line in moduleHelpTemplate.Render(moduleHelpInvocation.Module.Assembly, TerminalWidth()))
                        {
                            Console.WriteLine(line);
                        }
                        return 0; // Return success code
                    case CommandHelpInvocation commandHelpInvocation:
                        var commandHelpTemplate = new CommandHelpTemplate();
                        foreach (var line in commandHelpTemplate.Render(commandHelpInvocation.Command.CommandType, TerminalWidth()))
                        {
                            Console.WriteLine(line);
                        }
                        return 0; // Return success code
                    case CommandInvocation cmdInvocation:
                        await cmdInvocation.Processor.Process(new CLIInvocationInfo { RawCommand = args });
                        return 0; // Return success code
                    default:
                        Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Unknown invocation type: {Invocation.GetType().Name}");
                        return 1; // Return error code
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Fatal error: {ex.Message}");
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Stack trace: {ex.StackTrace}");
                return 1; // Return error code
            }
        }
    }
}