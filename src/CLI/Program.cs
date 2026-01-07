using System;
using System.Diagnostics;
using Core.Abstractions;
using Core.Cli.Exceptions;
using Core.Cli.Parser;
using Core.Engine;
using Core.Telemetry;
using Microsoft.Extensions.DependencyInjection;
using SharedContracts;

namespace Core.Cli;

public class Program
{
    private static ITelemetryService? _telemetryService;

    public static int Main(string[] args)
    {
        // Initialize telemetry at the beginning
        _telemetryService = new TelemetryService();

        try
        {
            using var activity = _telemetryService.StartActivity("AzureCLI.Main", ActivityKind.Internal);
            activity?.SetTag("args.count", args.Length);
            activity?.SetTag("args.command", args.Length > 0 ? args[0] : "none");

            var serviceProvider = Bootstrapper.ServiceProviderAsync.GetAwaiter().GetResult();
            var dispatcher = new ExtensionDispatcher();
        
            try
            {
                //var command = dispatcher.Parse(args);
                ICommandInvocation invocation = dispatcher.Parse(args);
            
                using var commandActivity = _telemetryService.StartActivity("AzureCLI.ProcessCommand", ActivityKind.Internal);
                commandActivity?.SetTag("command.type", invocation.GetType().Name);
            
                string result = Engine.Engine.GetCommand(invocation).Process(true);

                serviceProvider.GetRequiredService<ILoggerService>().Log(result);
                activity?.SetStatus(ActivityStatusCode.Ok);
                return 0;
            }
            catch (DispatchHelpException)
            {
                serviceProvider.GetRequiredService<ILoggerService>().Log("Command Line Interface Help");
                activity?.SetStatus(ActivityStatusCode.Ok, "Help requested");
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
                activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
                activity?.AddException(ex);
                return 1;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Unhandled exception: {ex}");
                activity?.SetStatus(ActivityStatusCode.Error, "Unhandled exception");
                activity?.AddException(ex);
                return 1;
            }
        }
        finally
        {
            // Ensure all traces are flushed before exit
            _telemetryService?.Dispose();
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
