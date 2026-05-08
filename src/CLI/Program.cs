using System.Diagnostics;
using System.Reflection;
using Core.Abstractions;
using Core.Cli.Exceptions;
using Core.Cli.Parser;
using Core.Engine;
using Core.Telemetry;
using Core.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;

namespace Core.Cli;

public class Program
{
    private static readonly ClientType _clientType = ClientType.CLI;
    private static readonly string AssemblyName = Assembly.GetExecutingAssembly().GetName().Name!;
    private static ITelemetryService? _telemetryService;
    private static ILoggingService? _logger;
    private static readonly CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();
    
    // CLI by default only enable output and error streams. Optional streams enabled by global parameters
    private static OutputStreams streams = new OutputStreams(
        WriteOutput: (value, ct) =>
        {
            Console.WriteLine(CliOutputFormatter.FormatValue(value));
            return Task.CompletedTask;
        },
        WriteError: (value, ct) =>
        {
            Console.Error.WriteLine(value);
            return Task.CompletedTask;
        },
        WriteVerbose: (value, ct) => Task.CompletedTask,
        WriteDebug: (value, ct) => Task.CompletedTask,
        WriteWarning: (value, ct) => Task.CompletedTask,
        WriteInformation: (value, ct) => Task.CompletedTask
    );

    public static async Task<int> Main(string[] args)
    {
        // Background telemetry upload mode: send pending traces and exit.
        if (args.Length > 0 && args[0] == "--upload-telemetry")
        {
            await FileTelemetrySender.SendPendingStatic();
            return 0;
        }

        CirrusServiceProvider.AddPrependRegistration(services =>
        {
            services.AddSingleton<IExtensionLoader, BuiltinExtensionLoader>();
            services.AddSingleton<IExtensionLoader, MultiVersionExtensionLoader>();
            services.AddSingleton<PropertyParser>();
        });
        CirrusServiceProvider.AddOverrideRegistration(services =>
        {
            services.AddSingleton<IAssemblyLoadingProvider, CLIAssemblyLoadingProvider>();
            services.AddSingleton<IArgumentParsingProvider, CLIArgumentParsingProvider>();
        });
        CirrusServiceProvider.Initialize(_clientType);
        _telemetryService = CirrusServiceProvider.GetRequiredService<ITelemetryService>();
        _logger = CirrusServiceProvider.GetRequiredService<ILoggingService>();

        try
        {
            using var activity = _telemetryService.StartCLIActivity(args);
            var dispatcher = new ExtensionDispatcher();

            try
            {
                CommandInvocation invocation = dispatcher.Parse(args);

                using var commandActivity = _telemetryService.ActivitySource.StartActivity("AzureCLI.ProcessCommand", ActivityKind.Internal);
                commandActivity?.SetTag("command.type", _clientType.ToString());

                var engine = CirrusServiceProvider.GetRequiredService<IEngine>() ?? throw new InvalidOperationException("Engine service not found.");
                using IInvocationSession session = engine.InvokeSession(invocation, streams, _cancellationTokenSource.Token);
                session.Process();

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
                _logger?.Error(ex.Message);

                activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
                activity?.AddException(ex);

                return await TryAgentFallbackAsync(args);
            }
        } // activity disposed here → traces exported to file
        finally
        {
            _telemetryService?.Dispose();
            CirrusServiceProvider.Dispose();
            SpawnTelemetryUploader();
        }
    }

    private static async Task<int> TryAgentFallbackAsync(string[] args)
    {
        const int maxRetries = 5;
        int attempt = 0;
        string[] currentArgs = args;
        var logger = CirrusServiceProvider.GetRequiredService<ILoggingService>();

        try
        {
            // Use the singleton AgentEngine from DI container
            var agentEngine = CirrusServiceProvider.GetRequiredService<AgentEngine>();
            var output = CirrusServiceProvider.GetRequiredService<IOutputAdapter>();

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
                // Skip the first token if it matches the assembly name
                if (retryArgs.Length > 0 && retryArgs[0].Equals(AssemblyName, StringComparison.OrdinalIgnoreCase))
                {
                    retryArgs = retryArgs.Skip(1).ToArray();
                }

                await output.WriteLineAsync($"\nRetrying with command: {extractedCommand}\n");

                try
                {
                    var dispatcher = new ExtensionDispatcher();
                    CommandInvocation invocation = dispatcher.Parse(retryArgs);
                    //string result = Engine.Engine.GetCommand(invocation).Process(true).ToString();
                    //Console.WriteLine(result);
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
            logger.Error($"AI agent error: {ex.Message}");
            return 1;
        }
    }

    /// <summary>
    /// Spawns a detached background process to upload buffered telemetry to Azure Monitor.
    /// Fire-and-forget: the CLI exits immediately without waiting for the upload.
    /// </summary>
    private static void SpawnTelemetryUploader()
    {
        try
        {
            var exePath = Environment.ProcessPath;
            if (exePath == null) return;

            // In-process benchmark mode: do not attempt to flush telemetry as in benchmark execution process
            if (!exePath.EndsWith("azv2", StringComparison.OrdinalIgnoreCase) &&
                !exePath.EndsWith("azv2.exe", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            var startInfo = new ProcessStartInfo
            {
                FileName = exePath,
                Arguments = "--upload-telemetry",
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            Process.Start(startInfo);
            // Don't wait — the background process runs independently.
        }
        catch
        {
            // Best effort. If spawn fails, traces remain on disk
            // and will be uploaded on the next CLI invocation.
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
