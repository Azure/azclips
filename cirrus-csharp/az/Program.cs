namespace AzTool
{
    using System;
    using System.Linq;
    using System.Reflection;
    using System.Threading.Tasks;

    // Main program class for the Az tool
    public class Program
    {
        public static Invocation CreateInvocation(string[] args)
        {
            var helpFlags = new[] { "--help", "-h", "help", "?", "-?" };
            if (args.Length == 0 || helpFlags.Contains(args[0], StringComparer.OrdinalIgnoreCase))
            {
                return new AppHelpInvocation();
            }
            var (module, remainingArgs) = ModuleProxy.LoadFromArgs(args);
            return module.CreateInvocation(remainingArgs);
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
                        await cmdInvocation.Processor.Process();
                        return 0; // Return success code
                    default:
                        Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Unknown invocation type: {Invocation.GetType().Name}");
                        return 1; // Return error code
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Fatal error: {ex.Message}");
                return 1; // Return error code
            }
        }
    }
}