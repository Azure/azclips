using System;
using System.Linq;
using System.Threading.Tasks;
using Core.Abstractions;
using Core.Cli.Exceptions;
using Core.Cli.Parser;
using Core.Engine;
using Core.Implementation;
using Microsoft.Extensions.DependencyInjection;
using SharedContracts;

namespace Core.Cli;

public class Program
{
    public static async Task<int> Main(string[] args)
    {
        var serviceProvider = Bootstrapper.ServiceProviderAsync.GetAwaiter().GetResult();
        var dispatcher = new ExtensionDispatcher();
        try
        {
            //var command = dispatcher.Parse(args);
            ICommandInvocation invocation = dispatcher.Parse(args);
            string result = Engine.Engine.GetCommand(invocation).Process(true);

            serviceProvider.GetRequiredService<ILoggerService>().Log(result);
            return 0;
        }
        catch (DispatchHelpException)
        {
            serviceProvider.GetRequiredService<ILoggerService>().Log("Command Line Interface Help");
            return 0;
        }
        catch (Exception)
        {
            // Extension execution failed - try AI agent fallback
            return await TryAgentFallbackAsync(args);
        }
    }

    private static async Task<int> TryAgentFallbackAsync(string[] args)
    {
        const int maxRetries = 5;
        int attempt = 0;
        string[] currentArgs = args;

        try
        {
            var output = new ConsoleOutputAdapter();
            var flavor = Environment.GetEnvironmentVariable("AZURECLI_FLAVOR") ?? "azure cli";
            var agentEngine = new AgentEngine(output, flavor);

            while (attempt < maxRetries)
            {
                attempt++;

                await agentEngine.ExecuteCommandAsync(currentArgs, default);

                var extractedCommand = agentEngine.ExtractedCommand;
                if (string.IsNullOrWhiteSpace(extractedCommand))
                {
                    // AI provided info/help but no executable command
                    return 0;
                }

                // AI generated a command - retry through extension loader
                var retryArgs = extractedCommand.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                // Skip the first token if it's "az"
                if (retryArgs.Length > 0 && retryArgs[0].Equals("az", StringComparison.OrdinalIgnoreCase))
                {
                    retryArgs = retryArgs.Skip(1).ToArray();
                }

                await output.WriteLineAsync($"\nRetrying with command: {extractedCommand}\n");

                try
                {
                    var dispatcher = new ExtensionDispatcher();
                    ICommandInvocation invocation = dispatcher.Parse(retryArgs);
                    string result = Engine.Engine.GetCommand(invocation).Process(true);
                    Console.WriteLine(result);
                    return 0;
                }
                catch (Exception ex)
                {
                    if (attempt >= maxRetries)
                    {
                        await output.WriteErrorAsync($"Failed to execute command after {maxRetries} attempts: {ex.Message}");
                        return 1;
                    }
                    // Update args for next iteration
                    currentArgs = retryArgs;
                }
            }

            return 1;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"AI agent error: {ex.Message}");
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
