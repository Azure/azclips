using System.Diagnostics;
using System.Reflection;
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
    private static CommandResolver? _resolver;
    private static IEngine? _engine;
    private static IServiceProvider? _serviceProvider;

    public static async Task<int> Main(string[] args)
    {
        var builder = new CirrusHostBuilder();
        builder.Services.AddCirrusCore();
        builder.Services.AddCirrusCli();
        builder.Services.AddCirrusCliParser();
        _serviceProvider = await builder.BuildAsync();

        _telemetryService = _serviceProvider.GetRequiredService<ITelemetryService>();
        _logger = _serviceProvider.GetRequiredService<ILoggingService>();
        _resolver = _serviceProvider.GetRequiredService<CommandResolver>();
        _engine = _serviceProvider.GetRequiredService<IEngine>();

        try
        {
            using var activity = _telemetryService.StartCLIActivity(args);

            try
            {
                ResolvedCommand resolved = _resolver.Resolve(args);

                using var commandActivity = _telemetryService.ActivitySource.StartActivity("AzureCLI.ProcessCommand", ActivityKind.Internal);
                commandActivity?.SetTag("command.type", resolved.CommandType.Name);

                _engine.Execute(resolved.CommandType, resolved.Parameters, (value, ct) => { Console.WriteLine(value); return Task.CompletedTask; });

                activity?.SetStatus(ActivityStatusCode.Ok);
                return 0;
            }
            catch (DispatchHelpException)
            {
                var template = new DispatchHelpTemplate();
                foreach (var line in template.Render(AssemblyName, TerminalWidth()))
                    Console.WriteLine(line);
                activity?.SetStatus(ActivityStatusCode.Ok, "Help requested");
                return 0;
            }
            catch (CommandGroupHelpException ex)
            {
                var template = new CommandGroupHelpTemplate();
                foreach (var line in template.Render(ex.GroupName, ex.ExtensionIndex, AssemblyName, TerminalWidth()))
                    Console.WriteLine(line);
                activity?.SetStatus(ActivityStatusCode.Ok, "Group help requested");
                return 0;
            }
            catch (CommandHelpException ex)
            {
                var template = new CommandHelpTemplate();
                foreach (var line in template.Render(ex.GroupName, ex.CommandMetadata, AssemblyName, TerminalWidth()))
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
        var logger = _serviceProvider!.GetRequiredService<ILoggingService>();

        try
        {
            var agentEngine = _serviceProvider!.GetRequiredService<AgentEngine>();
            var output = _serviceProvider!.GetRequiredService<IOutputAdapter>();

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
                    ResolvedCommand resolved = _resolver!.Resolve(retryArgs);
                    _engine!.Execute(resolved.CommandType, resolved.Parameters, (value, ct) => { Console.WriteLine(value); return Task.CompletedTask; });
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
