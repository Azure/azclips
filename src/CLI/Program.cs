using System.Diagnostics;
using System.Reflection;
using Core.Abstractions;
using Core.Cli.Exceptions;
using Core.Cli.Parser;
using Core.Cli.Telemetry;
using Core.Telemetry;
using Core.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;

namespace Core.Cli;

public class Program
{
    private static readonly ClientType _clientType = ClientType.CLI;
    private static readonly string AssemblyName = Assembly.GetExecutingAssembly().GetName().Name!;
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
            var originalColor = Console.ForegroundColor;
            Console.ForegroundColor = ConsoleColor.Red;
            Console.Error.WriteLine($"[ERROR] {value}");
            Console.ForegroundColor = originalColor;
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
            services.AddSingleton<ITelemetryProvider, CLITelemetryProvider>();
        });
        CirrusServiceProvider.Initialize(_clientType);
        _logger = CirrusServiceProvider.GetRequiredService<ILoggingService>();

        var telemetryService = CirrusServiceProvider.GetRequiredService<ITelemetryService>();

        try
        {
            using var activity = telemetryService.StartRootActivity();
            var dispatcher = new ExtensionDispatcher();
            args = WireDebugStreams(args);
            try
            {
                CommandInvocation invocation = dispatcher.Parse(args);
                telemetryService.TagInvocation(invocation);

                using var commandActivity =
                    telemetryService.StartActivity("AzureCLI.ProcessCommand", ActivityKind.Internal);

                var engine = CirrusServiceProvider.GetRequiredService<IEngine>() ??
                             throw new InvalidOperationException("Engine service not found.");
                using ISession session =
                    engine.OpenExecutionSession(invocation, streams, _cancellationTokenSource.Token);
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
            catch (HttpRequestException ex)
            {
                activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
                activity?.AddException(ex);

                return await TryAgentFallbackAsync(args, ex.Message);
            }
            catch (Exception ex)
            {
                _logger?.Error(ex.Message);

                activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
                activity?.AddException(ex);

                return await TryAgentFallbackAsync(args, ex.Message);
            }
        }
        finally
        {
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
                var originalColor = Console.ForegroundColor;
                Console.ForegroundColor = ConsoleColor.Red;
                Console.Error.WriteLine($"[ERROR] {value}");
                Console.ForegroundColor = originalColor;
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

    /// <summary>
    /// Hands a failed classic-CLI invocation off to the SDK-backed agent. Spawns
    /// <c>Core.Tui.exe --agent-mode</c> as a subprocess so the agent runs in the
    /// TUI process — keeping <see cref="Core.Agent"/> physically isolated from
    /// the CLI process and free of TUI / Hex1b dependencies.
    ///
    /// The subprocess inherits stdio so streaming tokens, the approval prompt,
    /// and any interactive sub-tool (device-code login) surface live in the
    /// user's terminal without in-process plumbing.
    /// </summary>
    private static async Task<int> TryAgentFallbackAsync(string[] args, string? errorText)
    {
        // Global disable switch. Set CIRRUS_AGENT_DISABLED=1 to keep the
        // original classic error and skip the agent fallback entirely.
        if (Environment.GetEnvironmentVariable("CIRRUS_AGENT_DISABLED") == "1")
        {
            return 1;
        }

        // Recursion guard: a failed azv2 invoked by run_command from inside an
        // already-running agent-mode TUI must NOT spawn another agent-mode TUI.
        if (Environment.GetEnvironmentVariable("CIRRUS_IN_AGENT_MODE") == "1")
        {
            return 1;
        }

        var tuiExe = FindTuiExe();
        if (tuiExe is null)
        {
            _logger?.Error("agent mode unavailable: Core.Tui.exe not found alongside azv2");
            return 1;
        }

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = tuiExe,
                // Inherit stdio so streaming tokens, y/n prompt, device-code links
                // all show live in the user's terminal.
                UseShellExecute = false,
                RedirectStandardOutput = false,
                RedirectStandardError = false,
                RedirectStandardInput = false,
            };
            psi.Environment["CIRRUS_IN_AGENT_MODE"] = "1";
            psi.ArgumentList.Add("--agent-mode");
            psi.ArgumentList.Add("--failed-command");
            psi.ArgumentList.Add(JoinShellQuoted(args));
            if (!string.IsNullOrWhiteSpace(errorText))
            {
                psi.ArgumentList.Add("--error");
                psi.ArgumentList.Add(errorText);
            }

            using var proc = Process.Start(psi)
                ?? throw new InvalidOperationException("Process.Start returned null");
            await proc.WaitForExitAsync();
            return proc.ExitCode;
        }
        catch (Exception ex)
        {
            _logger?.Error($"agent mode subprocess failed: {ex.Message}");
            return 1;
        }
    }

    private static string JoinShellQuoted(string[] args)
    {
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < args.Length; i++)
        {
            if (i > 0) sb.Append(' ');
            var a = args[i];
            if (a.Length == 0 || a.Contains(' ') || a.Contains('\t') || a.Contains('"'))
                sb.Append('"').Append(a.Replace("\"", "\\\"")).Append('"');
            else
                sb.Append(a);
        }
        return sb.ToString();
    }

    private static string? FindTuiExe()
    {
        var baseDir = AppContext.BaseDirectory;
        foreach (var candidate in new[]
        {
            Path.Combine(baseDir, "Core.Tui.exe"),
            Path.Combine(baseDir, "Core.Tui"),
            Path.Combine(baseDir, "..", "Core.Tui", "Core.Tui.exe"),
            Path.Combine(baseDir, "..", "Core.Tui", "Core.Tui"),
        })
        {
            if (File.Exists(candidate)) return Path.GetFullPath(candidate);
        }
        return null;
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
