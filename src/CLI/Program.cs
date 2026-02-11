using System;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Core.Abstractions;
using Core.Cli.Exceptions;
using Core.Cli.Parser;
using Core.Engine;
using Core.Cli.Help.Templates;
using Core.Telemetry;
using Microsoft.Extensions.DependencyInjection;

namespace Core.Cli;

public class Program
{
    private static readonly string AssemblyName = Assembly.GetExecutingAssembly().GetName().Name!;
    private static ITelemetryService? _telemetryService;
    private static ILoggingService? _logger;
    private static CommandActivator? _activator;
    private static CLIEngine? _engine;

    public static async Task<int> Main(string[] args)
    {
        var serviceProvider = await Bootstrapper.ServiceProviderAsync;
        _telemetryService = serviceProvider.GetRequiredService<ITelemetryService>();
        _logger = serviceProvider.GetRequiredService<ILoggingService>();

        // Create extension loaders and command resolver
        var extensionsDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".cirrus", "extensions");
        IExtensionLoader[] extensionLoaders =
        [
            new BuiltinExtensionLoader(_logger),
            new MultiVersionExtensionLoader(extensionsDir, _logger)
        ];
        _activator = new CommandActivator(extensionLoaders, _logger);
        _engine = new CLIEngine(serviceProvider);

        try
        {
            using var activity = _telemetryService.StartCLIActivity(args);

            try
            {
                IProcessor command = _activator.Activate(args);

                using var commandActivity = _telemetryService.ActivitySource.StartActivity("AzureCLI.ProcessCommand", ActivityKind.Internal);
                commandActivity?.SetTag("command.type", command.GetType().Name);

                _engine.Execute(command, (value, ct) => { Console.WriteLine(value); return Task.CompletedTask; });

                activity?.SetStatus(ActivityStatusCode.Ok);
                return 0;
            }
            catch (DispatchHelpException)
            {
                var template = new DispatchHelpTemplate();
                foreach (var line in template.Render("azv2", TerminalWidth()))
                    Console.WriteLine(line);
                activity?.SetStatus(ActivityStatusCode.Ok, "Help requested");
                return 0;
            }
            catch (CommandGroupHelpException ex)
            {
                var template = new CommandGroupHelpTemplate();
                foreach (var line in template.Render(ex.GroupName, ex.ExtensionIndex, "azv2", TerminalWidth()))
                    Console.WriteLine(line);
                activity?.SetStatus(ActivityStatusCode.Ok, "Group help requested");
                return 0;
            }
            catch (CommandHelpException ex)
            {
                var template = new CommandHelpTemplate();
                foreach (var line in template.Render(ex.GroupName, ex.CommandMetadata, "azv2", TerminalWidth()))
                    Console.WriteLine(line);
                activity?.SetStatus(ActivityStatusCode.Ok, "Command help requested");
                return 0;
            }
            catch (Exception ex)
            {
                _logger?.Error(ex.Message);
                
                activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
                activity?.AddException(ex);

                return await TryAgentFallbackAsync(args);
            }
        }
        finally
        {
            _engine?.Dispose();
            _telemetryService?.Dispose();
        }
    }

    private static async Task<int> TryAgentFallbackAsync(string[] args)
    {
        const int maxRetries = 5;
        int attempt = 0;
        string[] currentArgs = args;
        var serviceProvider = await Bootstrapper.ServiceProviderAsync;
        var logger = serviceProvider.GetRequiredService<ILoggingService>();

        try
        {
            var agentEngine = serviceProvider.GetRequiredService<AgentEngine>();
            var output = serviceProvider.GetRequiredService<IOutputAdapter>();

            while (attempt < maxRetries)
            {
                attempt++;

                await agentEngine.ExecuteCommandAsync(currentArgs, default);

                var extractedCommand = agentEngine.ExtractedCommand;
                if (string.IsNullOrWhiteSpace(extractedCommand))
                {
                    return 0;
                }

                var retryArgs = extractedCommand.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (retryArgs.Length > 0 && retryArgs[0].Equals(AssemblyName, StringComparison.OrdinalIgnoreCase))
                {
                    retryArgs = retryArgs.Skip(1).ToArray();
                }

                await output.WriteLineAsync($"\nRetrying with command: {extractedCommand}\n");

                try
                {
                    IProcessor command = _activator!.Activate(retryArgs);
                    _engine!.Execute(command, (value, ct) => { Console.WriteLine(value); return Task.CompletedTask; });
                    return 0;
                }
                catch (Exception ex)
                {
                    if (attempt >= maxRetries)
                    {
                        await output.WriteErrorAsync($"Failed to execute command after {maxRetries} attempts: {ex.Message}");
                        return 1;
                    }
                    currentArgs = retryArgs;
                }
            }

            return 1;
        }
        catch (Exception ex)
        {
            logger.Error($"AI agent error: {ex.Message}");
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
