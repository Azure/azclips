// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Diagnostics;
using System.Reflection;
using Core.Abstractions;
using Core.Abstractions.Runtime.Handler;
using Core.Cli.Agent;
using Core.Cli.Completions;
using Core.Cli.Exceptions;
using Core.Cli.Help;
using Core.Cli.Parser;
using Core.Cli.Telemetry;
using Core.Cli.Version;
using Core.Configuration.Options;
using Core.Engine;
using Core.Extensions;
using Core.Runtime;
using Core.Telemetry;
using Core.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;

namespace Core.Cli;

public class Program
{
    private const string AgentModeEnvVar = "AZCLIPS_IN_AGENT_MODE";
    // Forwards the user's real cwd to the agent (whose working dir we override to
    // the install dir for probing). Read by EnvironmentProber on the agent side by
    // the same literal name — rename both together.
    private const string CallerCwdEnvVar = "AZCLIPS_CALLER_CWD";
    private static readonly ClientType _clientType = ClientType.CLI;
    private static readonly string AssemblyName = Assembly.GetExecutingAssembly().GetName().Name!;
    private static ILoggingService? _logger;
    private static readonly CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();

    private static OutputStreams streams = BuildStreams(CliLogLevel.Info);

    public static async Task<int> Main(string[] args)
    {
        bool isBareInvocation = args.Length == 0;

        // Background telemetry upload mode: send pending traces and exit.
        if (args.Length > 0 && args[0] == "--upload-telemetry")
        {
            await FileTelemetrySender.SendPendingStatic();
            return 0;
        }

        // Hidden UI-print probe. Renders a sample block for triaging terminal
        // wrap / encoding / alignment. Runs before DI so it stays independent.
        int? printProbeExit = PrintProbe.TryHandle(args, Console.Out);
        if (printProbeExit.HasValue)
        {
            return printProbeExit.Value;
        }

        AzclipsServiceProvider.PrependRegistration(services =>
        {
            services.AddSingleton<IExtensionLoader, BuiltinExtensionLoader>();
            services.AddSingleton<IExtensionLoader, MultiVersionExtensionLoader>();
            services.AddSingleton<PropertyParser>();
            services.AddCliExtensionDispatch();
        });
        AzclipsServiceProvider.OverrideRegistration(services =>
        {
            services.AddSingleton<IAssemblyLoadingProvider, CLIAssemblyLoadingProvider>();
            services.AddSingleton<IArgumentParsingProvider, CLIArgumentParsingProvider>();
            services.AddSingleton<ITelemetryProvider, CLITelemetryProvider>();
            services.AddSingleton<IUserAgentHandler, CLIUserAgentHandler>();
        });
        AzclipsServiceProvider.Initialize(_clientType);

        // Shell tab-completion metadata (after DI so generator can resolve IEngine directly)
        int? completionExit = await CompletionGenerator.TryHandleAsync(args, _clientType);
        if (completionExit.HasValue)
            return completionExit.Value;

        int? candidatesExit = await CompletionGenerator.TryHandleCandidatesAsync(args, _clientType);
        if (candidatesExit.HasValue)
            return candidatesExit.Value;

        _logger = AzclipsServiceProvider.GetRequiredService<ILoggingService>();
        var configurationService = AzclipsServiceProvider.GetRequiredService<IConfigurationService>();

        var telemetryService = AzclipsServiceProvider.GetRequiredService<ITelemetryService>();

        try
        {
            using var activity = telemetryService.StartRootActivity();
            var dispatcher = AzclipsServiceProvider.GetRequiredService<ExtensionDispatcher>();
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

                var engine = AzclipsServiceProvider.GetRequiredService<IEngine>() ??
                             throw new InvalidOperationException("Engine service not found.");
                using ISession session =
                    engine.OpenExecutionSession(invocation, streams, _cancellationTokenSource.Token);
                await session.RunAsync();

                activity?.SetStatus(ActivityStatusCode.Ok);
                return 0;
            }
            catch (DispatchHelpException)
            {
                var engine = AzclipsServiceProvider.GetRequiredService<IEngine>();
                // Banner only for bare `az`, not for top-level flags like `az --help` —
                // keeps scripted help-output grepping unaffected.
                if (isBareInvocation)
                {
                    WelcomeBanner.Write(Console.Out);
                }
                var exitCode = new RuntimeHelpDispatcher(engine, streams).RenderRoot();

                activity?.SetStatus(ActivityStatusCode.Ok, "Help requested");
                return exitCode;
            }
            catch (RuntimeExtensionHelpException helpException)
            {
                var engine = AzclipsServiceProvider.GetRequiredService<IEngine>();
                try
                {
                    var exitCode = new RuntimeHelpDispatcher(engine, streams).RenderExtensionHelp(helpException);

                    activity?.SetStatus(ActivityStatusCode.Ok, "Extension help requested");
                    return exitCode;
                }
                catch (AzclipsErrorException structured)
                {
                    // RenderExtensionHelp surfaces ExtensionNotInstalledException when the user asks
                    // for help on a verb owned by an uninstalled package (smart-suggestion path).
                    // Funnel through the same renderer the execute path uses so the UX matches.
                    EmitStructuredError(structured, outputJson);
                    activity?.SetStatus(ActivityStatusCode.Error, structured.Message);
                    return structured.ExitCode;
                }
            }
            catch (DispatchVersionException versionException)
            {
                // Version is a host cold-path command (like help): gather locally and render without
                // an engine session or extension load. JSON reuses the same formatter the old
                // `az version` command used; text serves `az --version`.
                var exitCode = new RuntimeVersionDispatcher(configurationService, Console.Out)
                    .Render(versionException.Format);

                activity?.SetStatus(ActivityStatusCode.Ok, "Version requested");
                return exitCode;
            }
            catch (HttpRequestException ex)
            {
                activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
                activity?.AddException(ex);

                return await TryAgentFallbackAsync(args, ex, "HttpRequestException");
            }
            catch (AzclipsErrorException ex)
            {
                // Structured AzClips error: render per --output mode and return the symbolic exit code.
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

                return await TryAgentFallbackAsync(args, ex, "Exception");
            }
        }
        finally
        {
            AzclipsServiceProvider.Dispose();
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

    // Strips `--no-cache` and propagates the bypass via AZCLIPS_NO_EXTENSION_CACHE env var,
    // which AssemblyLoadingOptions.BypassExtensionCache reads through ConfigurationService.
    // Test hook for benchmarking.
    private static string[] ParseNoCacheFlag(string[] args)
    {
        if (!args.Contains("--no-cache", StringComparer.OrdinalIgnoreCase))
        {
            return args;
        }
        Environment.SetEnvironmentVariable("AZCLIPS_NO_EXTENSION_CACHE", "true");
        return args.Where(a => !a.Equals("--no-cache", StringComparison.OrdinalIgnoreCase)).ToArray();
    }

    private static void EmitStructuredError(AzclipsErrorException ex, bool outputJson)
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

    private static async Task<int> TryAgentFallbackAsync(string[] args, Exception exception, string source)
    {
        var errorText = exception.Message;

        // Global enable switch: config `agent.enabled` (off by default for PP).
        // The user opts in with `az config set --key agent.enabled --value true`.
        // Fail-safe: if config can't be read, treat as disabled and surface the
        // original error rather than spawning the agent on an unknown state.
        AgentOptions agentOptions;
        try
        {
            var config = AzclipsServiceProvider.GetRequiredService<IConfigurationService>();
            agentOptions = config.GetConfiguration<AgentOptions>();
            if (!agentOptions.Enabled)
            {
                return 1;
            }
        }
        catch (Exception ex)
        {
            _logger?.Debug($"agent fallback skipped: agent.enabled unavailable ({ex.Message})");
            return 1;
        }

        // Skeleton for a future data-driven decision. The exclusion set is empty
        // today, so this is always false and every exception still reaches the
        // agent — behavior is unchanged. The trace below records the exception
        // (including whether it was excluded) so we can later decide which types
        // are well-understood enough to skip the agent.
        var excluded = AgentFallbackExclusions.IsExcluded(exception);

        // Whether the subprocess will actually run: not excluded, and none of the
        // guards below (interactive+redirected, recursion) trip, and the agent exe
        // exists. Computed up front so the trace records the real outcome.
        var interactiveBlocked = agentOptions.IsInteractive &&
            (Console.IsInputRedirected || Console.IsOutputRedirected);
        var recursion = Environment.GetEnvironmentVariable(AgentModeEnvVar) == "1";
        var agentExe = FindAgentExe();
        var willSpawn = !excluded && !interactiveBlocked && !recursion && agentExe is not null;

        // Record the triggering exception (parent side, real type) as the first
        // line of the session trace; hand the path to the child so it appends its
        // session to the same file. Null when tracing is off — then no --trace-file.
        var tracePath = AgentFallbackTrace.BeginSession(
            agentOptions.Trace, exception, source, excluded, willSpawn);

        // Excluded → surface the original error without spawning (no exclusions
        // today, so unreachable; kept so the skeleton is wired end to end).
        if (excluded)
        {
            return 1;
        }

        // Non-interactive guard — only for interactive sessions. `agent.sessionStyle`
        // = interactive keeps a chat loop that reads y/n approvals and prompts from
        // the terminal, so a non-TTY (CI, a pipe, an unattended script) would hang or
        // spew; surface the original error instead. oneshot proposes a plan and exits
        // without any approval turn, so it's safe (and useful) even when stdio is
        // redirected — don't gate it. Inlined (not the agent's ConsoleEnvironment
        // helper) so Core.Cli stays decoupled from Core.Agent — same TTY semantics.
        if (interactiveBlocked)
        {
            return 1;
        }

        // Recursion guard: a failed az invoked by powershell from inside an
        // already-running agent process must NOT spawn another agent.
        if (recursion)
        {
            return 1;
        }

        if (agentExe is null)
        {
            _logger?.Error("agent mode unavailable: azclips-agent.exe not found alongside az");
            return 1;
        }

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = agentExe,
                // Set the working directory to the agent exe's own directory so
                // relative additionalProbingPaths in azclips-agent.runtimeconfig.json
                // resolve correctly. .NET resolves those paths at CLR startup
                // (before any managed code runs), relative to the working
                // directory, not the exe location — so this must be set here and
                // can't be corrected later from inside the agent. The user's real
                // cwd is forwarded via AZCLIPS_CALLER_CWD below so environment
                // probing still reports the directory `az` was invoked from.
                WorkingDirectory = Path.GetDirectoryName(agentExe)!,
                // Inherit stdio so streaming tokens, y/n prompt, device-code links
                // all show live in the user's terminal.
                UseShellExecute = false,
                RedirectStandardOutput = false,
                RedirectStandardError = false,
                RedirectStandardInput = false,
            };
            psi.Environment[AgentModeEnvVar] = "1";
            // Forward the directory the user actually ran `az` from. WorkingDirectory
            // above is the agent install dir (needed for probing paths), which would
            // otherwise make the agent's environment snapshot report the wrong cwd.
            // Best-effort: GetCurrentDirectory can throw (e.g. the cwd was deleted),
            // and forwarding it is only a hint — skip it rather than fail the launch.
            try { psi.Environment[CallerCwdEnvVar] = Directory.GetCurrentDirectory(); }
            catch { /* leave CallerCwdEnvVar unset; the agent falls back to its own cwd */ }
            psi.ArgumentList.Add("--agent-mode");
            psi.ArgumentList.Add("--failed-command");
            psi.ArgumentList.Add(JoinShellQuoted(args));
            if (!string.IsNullOrWhiteSpace(errorText))
            {
                psi.ArgumentList.Add("--error");
                psi.ArgumentList.Add(errorText);
            }
            // Hand the parent-owned trace file to the child so it appends its
            // session trace to the same file (one timeline: parent exception →
            // child session). Null when tracing is off — then no --trace-file and
            // the child writes nothing.
            if (!string.IsNullOrWhiteSpace(tracePath))
            {
                psi.ArgumentList.Add("--trace-file");
                psi.ArgumentList.Add(tracePath);
            }

            // The child agent takes over Ctrl+C (it cancels the session). The parent
            // must also intercept it — otherwise Ctrl+C kills the parent first, the
            // shell redraws its prompt, and the child's remaining output prints onto
            // that prompt ("terminal hijack"). Intercept-only here; the child owns the
            // actual cancellation and we just wait for it to exit cleanly.
            ConsoleCancelEventHandler cancelHandler = (_, e) => e.Cancel = true;
            Console.CancelKeyPress += cancelHandler;
            try
            {
                using var proc = Process.Start(psi)
                    ?? throw new InvalidOperationException("Process.Start returned null");
                await proc.WaitForExitAsync();

                // Exit code semantics:
                //   one-shot — the agent only PROPOSED a fix; it never ran anything,
                //     so the user's original command still failed. Report failure
                //     (never let the agent's own 0 mask it), so `&&`, CI, and $?
                //     see the command as failed. The agent normally exits 0 here,
                //     so force a non-zero code.
                //   interactive — the agent may have actually run commands and
                //     resolved the task, so its real exit code is meaningful; pass
                //     it through unchanged.
                if (agentOptions.IsInteractive)
                {
                    return proc.ExitCode;
                }
                return proc.ExitCode != 0 ? proc.ExitCode : 1;
            }
            finally
            {
                Console.CancelKeyPress -= cancelHandler;
            }
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

    private static string? FindAgentExe()
    {
        var baseDir = AppContext.BaseDirectory;
        foreach (var candidate in new[]
        {
            // Packaged layout: azclips-agent.exe lives next to az.exe (hybrid
            // layout — see stage_agent). The two share the runtime pack + all
            // shared NuGet dlls; agent-only deps + prompts + copilot native
            // live under Extensions/Microsoft.Azure.Azclips.Extensions.Agent/.
            Path.Combine(baseDir, "azclips-agent.exe"),
            Path.Combine(baseDir, "azclips-agent"),
            // Local dev layout: `dotnet build` publishes each project to its
            // own artifacts/Debug/<ExtName>/ directory.
            Path.Combine(baseDir, "..", "Core.Agent", "azclips-agent.exe"),
            Path.Combine(baseDir, "..", "Core.Agent", "azclips-agent"),
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
            if (!exePath.EndsWith("az", StringComparison.OrdinalIgnoreCase) &&
                !exePath.EndsWith("az.exe", StringComparison.OrdinalIgnoreCase))
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
