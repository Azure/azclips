using System;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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
    private static ILoggerService? _logger;

    public static async Task<int> Main(string[] args)
    {
        var serviceProvider = await Bootstrapper.ServiceProviderAsync;
        _telemetryService = serviceProvider.GetRequiredService<ITelemetryService>();
        _logger = serviceProvider.GetRequiredService<ILoggerService>();

        try
        {
            using var activity = _telemetryService.StartCLIActivity(args);

            var dispatcher = new ExtensionDispatcher();

            try
            {
                ICommandInvocation invocation = dispatcher.Parse(args);

                using var commandActivity = _telemetryService.StartActivity("AzureCLI.ProcessCommand", ActivityKind.Internal);
                commandActivity?.SetTag("command.type", invocation.GetType().Name);

                string result = Engine.Engine.GetCommand(invocation).Process(true);

                _logger?.Log(result);
                activity?.SetStatus(ActivityStatusCode.Ok);
                return 0;
            }
            catch (DispatchHelpException)
            {
                _logger?.Log("Command Line Interface Help");
                activity?.SetStatus(ActivityStatusCode.Ok, "Help requested");
                return 0;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex.Message);
                
                activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
                activity?.AddException(ex);

                return await TryAgentFallbackAsync(args);
            }
        }
        finally
        {
            _telemetryService?.Dispose();
        }
    }

    private static async Task<int> TryAgentFallbackAsync(string[] args)
    {
        const int maxRetries = 5;
        int attempt = 0;
        string[] currentArgs = args;

        try
        {
            // Use the singleton AgentEngine from DI container
            var serviceProvider = await Bootstrapper.ServiceProviderAsync;
            var agentEngine = serviceProvider.GetRequiredService<AgentEngine>();
            var output = serviceProvider.GetRequiredService<IOutputAdapter>();

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
            _logger?.LogError($"AI agent error: {ex.Message}");
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
