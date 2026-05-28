using System.Diagnostics;
using System.Reflection;
using Core.Abstractions;
using Core.Cli.Exceptions;
using Core.Cli.Parser;
using Core.Engine;
using Core.Extensions;
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

        CirrusServiceProvider.PrependRegistration(services =>
        {
            services.AddSingleton<IExtensionLoader, BuiltinExtensionLoader>();
            services.AddSingleton<IExtensionLoader, MultiVersionExtensionLoader>();
            services.AddSingleton<PropertyParser>();
        });
        CirrusServiceProvider.OverrideRegistration(services =>
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
            args = WireDebugStreams(args);
            args = ParseOutputFlag(args, out var outputJson);
            args = ParseNoCacheFlag(args);
            try
            {
                CommandInvocation invocation = dispatcher.Parse(args);

                using var commandActivity = _telemetryService.ActivitySource.StartActivity("AzureCLI.ProcessCommand", ActivityKind.Internal);
                commandActivity?.SetTag("command.type", _clientType.ToString());

                var engine = CirrusServiceProvider.GetRequiredService<IEngine>() ?? throw new InvalidOperationException("Engine service not found.");
                using ISession session = engine.OpenExecutionSession(invocation, streams, _cancellationTokenSource.Token);
                await session.RunAsync();

                activity?.SetStatus(ActivityStatusCode.Ok);
                return 0;
            }
            catch (DispatchHelpException)
            {
                _logger?.Log("Command Line Interface Help");
                activity?.SetStatus(ActivityStatusCode.Ok, "Help requested");
                return 0;
            }
            catch (CirrusErrorException ex)
            {
                // Structured Cirrus error: render per --output mode and return the symbolic exit code.
                // No AI fallback — the failure is well-known and the user's intent is unambiguous.
                EmitStructuredError(ex, outputJson);
                activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
                return ex.ExitCode;
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

    // TODO(Yabo): Q&P squad to refactor global parameter handling
    // this method is temporary and is for ease of debugging
    private static string[] WireDebugStreams(string[] args)
    {
        if (!args.Contains("--debug"))
        {
            return args;
        }
        streams = new OutputStreams(
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
            WriteVerbose: (value, ct) => { Console.WriteLine($"VERBOSE: {value}"); return Task.CompletedTask; },
            WriteDebug: (value, ct) => { Console.WriteLine($"DEBUG: {value}"); return Task.CompletedTask; },
            WriteWarning: (value, ct) => { Console.WriteLine($"WARNING: {value}"); return Task.CompletedTask; },
            WriteInformation: (value, ct) =>
            {
                Console.WriteLine($"INFO: {value}");
                return Task.CompletedTask;
            }
        );
        return args.Where(a => a != "--debug").ToArray();
    }

    // Strips `--output json` / `--output plain` (and `--output=json`) from args and reports
    // whether JSON shape was requested. Per docs/spec/extension-management.md §4.4: when
    // `--output json` is active, errors render as { "error": { code, message, details } }.
    private static string[] ParseOutputFlag(string[] args, out bool outputJson)
    {
        outputJson = false;
        var result = new List<string>(args.Length);
        for (int i = 0; i < args.Length; i++)
        {
            var a = args[i];
            if (a.Equals("--output", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
            {
                outputJson = args[i + 1].Equals("json", StringComparison.OrdinalIgnoreCase);
                i++;
                continue;
            }
            if (a.StartsWith("--output=", StringComparison.OrdinalIgnoreCase))
            {
                outputJson = a.Substring("--output=".Length).Equals("json", StringComparison.OrdinalIgnoreCase);
                continue;
            }
            result.Add(a);
        }
        return result.ToArray();
    }

    // Strips `--no-cache` and flips the static ALC-cache bypass switch consumed by
    // MultiVersionExtensionLoader (Phase 5 #18 test hook).
    private static string[] ParseNoCacheFlag(string[] args)
    {
        if (!args.Contains("--no-cache", StringComparer.OrdinalIgnoreCase))
        {
            return args;
        }
        MultiVersionExtensionLoader.BypassCache = true;
        return args.Where(a => !a.Equals("--no-cache", StringComparison.OrdinalIgnoreCase)).ToArray();
    }

    private static void EmitStructuredError(CirrusErrorException ex, bool outputJson)
    {
        if (outputJson)
        {
            var payload = new
            {
                error = new
                {
                    code = ex.Code,
                    message = ex.Message,
                    details = ex.Details,
                },
            };
            Console.Error.WriteLine(System.Text.Json.JsonSerializer.Serialize(payload, new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase,
            }));
        }
        else
        {
            Console.Error.WriteLine($"error: [{ex.Code}] {ex.Message}");
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
