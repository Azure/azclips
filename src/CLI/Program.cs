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
    private static CommandActivator? _activator;
    private static CLIEngine? _engine;
    private static IServiceProvider? _serviceProvider;

    public static async Task<int> Main(string[] args)
    {
        var builder = new CirrusHostBuilder();
        builder.Services.AddCirrusCore();
        builder.Services.AddCirrusCli();
        _serviceProvider = await builder.BuildAsync();

        _telemetryService = _serviceProvider.GetRequiredService<ITelemetryService>();
        _logger = _serviceProvider.GetRequiredService<ILoggingService>();

        // Create extension loaders and command resolver
        var extensionsDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".cirrus", "extensions");
#if DEBUG
        var builtinExtensionsDir = Path.GetFullPath(
            Path.Combine(AppContext.BaseDirectory, ".."));
#else
        var builtinExtensionsDir = Path.Combine(AppContext.BaseDirectory, "Extensions");
#endif
        IExtensionLoader[] extensionLoaders =
        [
            new BuiltinExtensionLoader(builtinExtensionsDir, _logger),
            new MultiVersionExtensionLoader(extensionsDir, _logger)
        ];
        _activator = new CommandActivator(extensionLoaders, _logger);
        _engine = new CLIEngine(_serviceProvider);

        try
        {
            using var activity = _telemetryService.StartCLIActivity(args);

            try
            {
                ActivationResult activation = _activator.Activate(args);

                using var commandActivity = _telemetryService.ActivitySource.StartActivity("AzureCLI.ProcessCommand", ActivityKind.Internal);
                commandActivity?.SetTag("command.type", activation.CommandType.Name);

                _engine.Execute(activation.CommandType, activation.Parameters, (value, ct) => { Console.WriteLine(value); return Task.CompletedTask; });

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
                    ActivationResult activation = _activator!.Activate(retryArgs);
                    _engine!.Execute(activation.CommandType, activation.Parameters, (value, ct) => { Console.WriteLine(value); return Task.CompletedTask; });
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
