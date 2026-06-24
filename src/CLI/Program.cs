using System.Diagnostics;
using System.Reflection;
using Core.Abstractions;
using Core.Cli.Exceptions;
using Core.Cli.Help;
using Core.Cli.Parser;
using Core.Cli.Telemetry;
using Core.Engine;
using Core.Extensions;
using Core.Runtime;
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

    private static OutputStreams streams = BuildStreams(CliLogLevel.Info);

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
            services.AddCliExtensionDispatch();
        });
        CirrusServiceProvider.OverrideRegistration(services =>
        {
            services.AddSingleton<IAssemblyLoadingProvider, CLIAssemblyLoadingProvider>();
            services.AddSingleton<IArgumentParsingProvider, CLIArgumentParsingProvider>();
            services.AddSingleton<ITelemetryProvider, CLITelemetryProvider>();
        });
        CirrusServiceProvider.Initialize(_clientType);
        _logger = CirrusServiceProvider.GetRequiredService<ILoggingService>();
        var configurationService = CirrusServiceProvider.GetRequiredService<IConfigurationService>();

        var telemetryService = CirrusServiceProvider.GetRequiredService<ITelemetryService>();

        try
        {
            using var activity = telemetryService.StartRootActivity();
            var dispatcher = CirrusServiceProvider.GetRequiredService<ExtensionDispatcher>();
            args = ParseDebugFlag(args, out var debugOverride);
            var effectiveLogLevel = CliLoggingPolicy.ResolveEffectiveLevel(configurationService, debugOverride);
            streams = BuildStreams(effectiveLogLevel);
            args = ParseOutputFlag(args, out var outputJson);
            args = ParseNoCacheFlag(args);
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
                var assemblyLoading = CirrusServiceProvider.GetRequiredService<IAssemblyLoadingService>();
                var engine = CirrusServiceProvider.GetRequiredService<IEngine>();
                var exitCode = new RuntimeHelpDispatcher(assemblyLoading, engine).RenderRoot();

                activity?.SetStatus(ActivityStatusCode.Ok, "Help requested");
                return exitCode;
            }
            catch (RuntimeExtensionHelpException helpException)
            {
                var assemblyLoading = CirrusServiceProvider.GetRequiredService<IAssemblyLoadingService>();
                var engine = CirrusServiceProvider.GetRequiredService<IEngine>();
                try
                {
                    var exitCode = new RuntimeHelpDispatcher(assemblyLoading, engine).RenderExtensionHelp(helpException);

                    activity?.SetStatus(ActivityStatusCode.Ok, "Extension help requested");
                    return exitCode;
                }
                catch (CirrusErrorException structured)
                {
                    // RenderExtensionHelp surfaces ExtensionNotInstalledException when the user asks
                    // for help on a verb owned by an uninstalled package (smart-suggestion path).
                    // Funnel through the same renderer the execute path uses so the UX matches.
                    EmitStructuredError(structured, outputJson);
                    activity?.SetStatus(ActivityStatusCode.Error, structured.Message);
                    return structured.ExitCode;
                }
            }
            catch (HttpRequestException ex)
            {
                activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
                activity?.AddException(ex);

                return await TryAgentFallbackAsync(args, ex.Message);
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
    private static string[] ParseDebugFlag(string[] args, out bool debugOverride)
    {
        debugOverride = args.Contains("--debug", StringComparer.OrdinalIgnoreCase);
        if (!debugOverride)
        {
            return args;
        }

        return args.Where(a => !a.Equals("--debug", StringComparison.OrdinalIgnoreCase)).ToArray();
    }

    private static OutputStreams BuildStreams(CliLogLevel level)
    {
        return new OutputStreams(
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
            WriteVerbose: (value, ct) =>
            {
                if (CliLoggingPolicy.ShouldEmit(level, CliLogSeverity.Verbose))
                {
                    Console.WriteLine($"VERBOSE: {value}");
                }
                return Task.CompletedTask;
            },
            WriteDebug: (value, ct) =>
            {
                if (CliLoggingPolicy.ShouldEmit(level, CliLogSeverity.Debug))
                {
                    Console.WriteLine($"DEBUG: {value}");
                }
                return Task.CompletedTask;
            },
            WriteWarning: (value, ct) =>
            {
                if (CliLoggingPolicy.ShouldEmit(level, CliLogSeverity.Warn))
                {
                    Console.WriteLine($"WARNING: {value}");
                }
                return Task.CompletedTask;
            },
            WriteInformation: (value, ct) =>
            {
                if (CliLoggingPolicy.ShouldEmit(level, CliLogSeverity.Info))
                {
                    Console.WriteLine($"INFO: {value}");
                }
                return Task.CompletedTask;
            }
        );
    }

    // Strips `--output json` / `--output plain` (and `--output=json`) from args and reports
    // whether JSON shape was requested. When `--output json` is active, errors render as
    // { "error": { code, message, details } }.
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

    // Strips `--no-cache` and propagates the bypass via the CIRRUS_NO_EXTENSION_CACHE env var,
    // which AssemblyLoadingOptions.BypassExtensionCache reads through ConfigurationService.
    // Test hook for benchmarking.
    private static string[] ParseNoCacheFlag(string[] args)
    {
        if (!args.Contains("--no-cache", StringComparer.OrdinalIgnoreCase))
        {
            return args;
        }
        Environment.SetEnvironmentVariable("CIRRUS_NO_EXTENSION_CACHE", "true");
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
}
