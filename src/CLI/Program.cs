// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Diagnostics;
using Core.Abstractions;
using Core.Abstractions.Recovery;
using Core.Abstractions.Runtime.Handler;
using Core.Cli.Completions;
using Core.Cli.Exceptions;
using Core.Cli.Help;
using Core.Cli.Output;
using Core.Cli.Parser;
using Core.Cli.Recovery;
using Core.Cli.Telemetry;
using Core.Cli.Version;
using Core.Configuration.Options;
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
    /// <summary>
    /// W3C traceparent of the recovery span, read by the agent so its spans join that
    /// trace. Namespaced rather than the bare TRACEPARENT, which a surrounding CI system
    /// may set. The agent reads the same literal name — rename both together.
    /// </summary>
    internal const string RecoveryTraceparentEnvVar = "AZCLIPS_RECOVERY_TRACEPARENT";

    private static readonly ClientType _clientType = ClientType.CLI;
    private static ILoggingService? _logger;
    private static readonly CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();
    private static ITelemetryService? _telemetryService = null;
    private static IConfigurationService? _configurationService = null;
    private static string? _lastStreamError;

    private static OutputStreams streams = BuildStreams(CliLogLevel.Info, OutputFormat.Json, OutputFormat.Tsv);

    public static async Task<int> Main(string[] args)
    {
        _lastStreamError = null;
        var performance = new PerformanceTimeline("AzureCLI.Startup");
        bool isBareInvocation = args.Length == 0;

        if (args.Length > 0 && args[0] == "--upload-telemetry")
        {
            TelemetryUpload.RunPending();
            return 0;
        }

        // Hidden UI-print probe. Renders a sample block for triaging terminal
        // wrap / encoding / alignment. Runs before DI so it stays independent.
        int? printProbeExit = PrintProbe.TryHandle(args, Console.Out);
        if (printProbeExit.HasValue)
        {
            return printProbeExit.Value;
        }

        using (performance.Measure("AzureCLI.Startup.Container"))
        {
            AzclipsServiceProvider.PrependRegistration(services =>
            {
                services.AddSingleton<IExtensionLoader, BundledExtensionLoader>();
                services.AddSingleton<IExtensionLoader, InstalledExtensionLoader>();
                services.AddSingleton<PropertyParser>();
                services.AddCliExtensionDispatch();
            });
            AzclipsServiceProvider.OverrideRegistration(services =>
            {
                services.AddSingleton<IAssemblyLoadingProvider, CLIAssemblyLoadingProvider>();
                services.AddSingleton<IArgumentParsingProvider, CLIArgumentParsingProvider>();
                services.AddSingleton<ITelemetrySender, FileTelemetrySender>();
                services.AddSingleton<ITelemetryProvider, CLITelemetryProvider>();
                services.AddSingleton<IUserAgentHandler, CLIUserAgentHandler>();
            });
            AzclipsServiceProvider.Initialize(_clientType);
        }

        // Post-DI utility modes: completion generation needs the container to resolve
        // IEngine, but still returns before the root activity starts — no telemetry.
        int? valuesExit = await CompletionGenerator.TryHandleValuesAsync(args, _clientType);
        if (valuesExit.HasValue)
            return valuesExit.Value;

        int? completionExit = await CompletionGenerator.TryHandleAsync(args, _clientType);
        if (completionExit.HasValue)
            return completionExit.Value;

        int? candidatesExit = await CompletionGenerator.TryHandleCandidatesAsync(args, _clientType);
        if (candidatesExit.HasValue)
            return candidatesExit.Value;

        if (GlobalArgParser.IsCommandInvocation(args, IntrinsicVerbs.Troubleshoot))
            return await TroubleshootCommand.RunAsync(args, _cancellationTokenSource.Token);

        using (performance.Measure("AzureCLI.Startup.Services"))
        {
            _logger = AzclipsServiceProvider.GetRequiredService<ILoggingService>();
            _configurationService = AzclipsServiceProvider.GetRequiredService<IConfigurationService>();
            _telemetryService = AzclipsServiceProvider.GetRequiredService<ITelemetryService>();
        }

        Activity? activity = null;

        // Safe defaults in case GlobalArgParser.Parse throws (invalid --output).
        var errorFormat = OutputFormat.Table;
        var errorColorAllowed = false;
        var errorHelpAllowed = false;
        var positional = args;
        CommandInvocation? invocation = null;

        try
        {
            // Disposed explicitly in the finally below — it must stop before the container
            // is disposed, since that takes the TracerProvider (and the exporter) with it.

            using (performance.Measure("AzureCLI.Startup.Telemetry"))
            {
                activity = _telemetryService.StartRootActivity(performance.StartedAt);

                // Tags the root activity directly (SetTag mutates a shared heap object) rather
                // than writing to AsyncLocal-backed ambient state, so the tag is visible to
                // whichever code later disposes/exports this activity in Main's own finally
                // block, regardless of which nested awaited call resolved it. This is a
                // best-effort early resolution (from raw args, before command parsing).
                // MUST run before performance.Replay below: Replay creates and immediately
                // disposes the startup-phase activities, and the CLI's FileTelemetrySender
                // exports synchronously on Dispose — so if those activities were replayed
                // before the root carries a DataBoundary tag, they'd export untagged, get
                // normalized to Global by TraceUploader, and leak an EU profile's startup
                // telemetry to the Global endpoint. ExecutionSession.RunAsync below
                // re-resolves and re-tags from the bound --profile once it's known, which is
                // authoritative for the command's own spans.
                var (routedTenantId, routedBoundary) = await TelemetryRoutingResolver.ResolveAsync(
                    AzclipsServiceProvider.GetRequiredService<IProfileService>(),
                    AzclipsServiceProvider.GetRequiredService<IAuthService>(),
                    TryGetProfileOverride(args),
                    _cancellationTokenSource.Token);
                if (routedTenantId is not null) activity?.SetTag(TelemetryTags.TenantId, routedTenantId);
                if (routedBoundary is not null) activity?.SetTag(TelemetryTags.DataBoundary, routedBoundary);

                performance.Replay(_telemetryService);
            }

            GlobalArgs globalArgs;
            using (var _ = _telemetryService.StartActivity("AzureCLI.ParseGlobalArgs"))
            {
                globalArgs = GlobalArgParser.Parse(args);
            }

            using (var _ = _telemetryService.StartActivity("AzureCLI.HandleGlobalArgs"))
            {
                if (globalArgs.NoCache)
                {
                    // AssemblyLoadingOptions.BypassExtensionCache reads this via IConfigurationService.
                    Environment.SetEnvironmentVariable("AZCLIPS_NO_EXTENSION_CACHE", "true");
                }

                var coreOptions = _configurationService.GetConfiguration<CoreOptions>();
                var clientsOptions = _configurationService.GetConfiguration<ClientsOptions>();
                var settings = SessionPolicyResolver.Resolve(coreOptions, clientsOptions, globalArgs);
                ColorPolicy.Initialize(settings.Color);
                streams = BuildStreams(settings.LogLevel, settings.Format, settings.SimpleFormat, globalArgs.Query);
                errorFormat = ErrorHelpPolicy.ResolveErrorFormat(
                    errorStreamIsHumanReadable: ErrorHelpPolicy.IsHumanReadableErrorStream(Console.IsErrorRedirected, globalArgs.Output),
                    configuredFormat: settings.Format,
                    explicitFormat: globalArgs.Output);
                errorColorAllowed = ColorPolicy.ShouldEmitColor(Console.IsErrorRedirected);
                errorHelpAllowed = ErrorHelpPolicy.IsHumanReadableErrorStream(Console.IsErrorRedirected, globalArgs.Output);
                positional = globalArgs.Positional as string[] ?? globalArgs.Positional.ToArray();

                if (globalArgs.Query is not null)
                {
                    JmesPathEvaluator.Validate(globalArgs.Query);
                }

                if (globalArgs.Version is not null)
                {
                    return DispatchVersion(globalArgs.Version, globalArgs.Query, activity);
                }

                if (globalArgs.Help is not null)
                {
                    return DispatchHelp(globalArgs.Help, isBareInvocation, streams, activity);
                }
            }


            using (var _ = _telemetryService.StartActivity("AzureCLI.ParseCommandInvocation"))
            {
                var dispatcher = AzclipsServiceProvider.GetRequiredService<ExtensionDispatcher>();
                invocation = dispatcher.Parse(positional);
                _telemetryService.TagInvocation(invocation);
            }

            using (var _ = _telemetryService.StartActivity("AzureCLI.ProcessCommand"))
            {
                var engine = AzclipsServiceProvider.GetRequiredService<IEngine>() ??
                             throw new InvalidOperationException("Engine service not found.");
                using ISession session =
                    engine.OpenExecutionSession(invocation, streams, _cancellationTokenSource.Token);
                await session.RunAsync();

                activity?.SetStatus(ActivityStatusCode.Ok);
                return 0;
            }
        }
        catch (HttpRequestException ex)
        {
            if (!string.Equals(_lastStreamError, ex.Message, StringComparison.Ordinal))
                _logger?.Error(ex.Message);
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.AddException(ex);
            var failure = await CaptureFailureAsync(args, positional, ex, 1, invocation, activity);
            EmitRecoveryDiagnostic(failure, errorFormat, errorColorAllowed);

            return await StopRootThenFallbackAsync(
                activity,
                (traceId, spanId, commandName) =>
                    TryAgentFallbackAsync(positional, ex.Message, traceId, spanId, commandName));
        }
        // Filtered rather than checked in the body so other shapes still reach the structured-error catch below.
        catch (CommandNotFoundException ex) when (ErrorHelpPolicy.ShouldShowGroupHelp(ex, positional))
        {
            try
            {
                activity?.SetStatus(ActivityStatusCode.Ok, "Help requested (empty command group)");
                return DispatchHelp(new HelpRequest([ex.Verb]), isBareInvocation: false, streams, activity);
            }
            catch (Exception helpEx)
            {
                // Throwing from a catch body skips the catches below, so degrade to the original error here.
                _logger?.Debug($"group help unavailable: {helpEx.Message}");
                var failure = await CaptureFailureAsync(args, positional, ex, ex.ExitCode, invocation, activity);
                EmitStructuredError(ex, errorFormat, errorColorAllowed, invocation, failure);
                EmitRecoveryDiagnostic(failure, errorFormat, errorColorAllowed, structuredErrorAlreadyWritten: true);
                activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
                return ex.ExitCode;
            }
        }
        catch (AzclipsErrorException ex)
        {
            var failure = await CaptureFailureAsync(args, positional, ex, ex.ExitCode, invocation, activity);
            if (ErrorHelpPolicy.ShouldShowCommandHelp(ex, invocation, errorHelpAllowed))
            {
                EmitStructuredError(ex, errorFormat, errorColorAllowed, invocation, failure);
                TryRenderHelpAfterError(ErrorHelpPolicy.CommandHelpRequest(invocation), streams, activity);
                EmitRecoveryDiagnostic(failure, errorFormat, errorColorAllowed, structuredErrorAlreadyWritten: true);
                activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
                return ex.ExitCode;
            }

            // Structured AzClips error: render per --output mode and return the symbolic exit code.
            // No AI fallback — the failure is well-known and the user's intent is unambiguous.
            EmitStructuredError(ex, errorFormat, errorColorAllowed, invocation, failure);
            EmitRecoveryDiagnostic(failure, errorFormat, errorColorAllowed, structuredErrorAlreadyWritten: true);
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            return ex.ExitCode;
        }
        catch (Exception ex)
        {
            _logger?.Error(ex.Message);

            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.AddException(ex);
            var failure = await CaptureFailureAsync(args, positional, ex, 1, invocation, activity);
            EmitRecoveryDiagnostic(failure, errorFormat, errorColorAllowed);

            return await StopRootThenFallbackAsync(
                activity,
                (traceId, spanId, commandName) =>
                    TryAgentFallbackAsync(positional, ex.Message, traceId, spanId, commandName));
        }
        finally
        {
            // Stop the root span while the exporter is still alive — AzclipsServiceProvider.Dispose()
            // below disposes the TracerProvider, after which nothing can be exported.
            activity?.Dispose();

            // Route through streams.WriteDebug (level-gated) so the aggregate table stays
            // silent under warn/error and only surfaces under --debug. Routing through
            // _logger would fall back to ConsoleLoggingService here (session's UseScope
            // was already disposed) and leak the table regardless of level.
            ExtensionLoadTracker.EmitSummary(msg => streams.WriteDebug(msg, default).GetAwaiter().GetResult());
            AzclipsServiceProvider.Dispose();
            SpawnTelemetryUploader();
        }
    }

    private static OutputStreams BuildStreams(CliLogLevel level, OutputFormat format, OutputFormat simpleFormat, string? query = null)
    {
        var colorPolicyAllowsColor = ColorPolicy.ShouldEmitColor(Console.IsOutputRedirected);
        return new OutputStreams(
            WriteOutput: (value, ct) =>
            {
                var terminalWidth = format == OutputFormat.Table || simpleFormat == OutputFormat.Table
                    ? TerminalWidthProvider.Get()
                    : null;
                Console.WriteLine(CliOutputFormatter.FormatValue(value, format, simpleFormat, colorPolicyAllowsColor, query, terminalWidth));
                return Task.CompletedTask;
            },
            WriteError: (value, ct) =>
            {
                _lastStreamError = value?.ToString() ?? string.Empty;
                ConsoleSink.Error(_lastStreamError);
                return Task.CompletedTask;
            },
            WriteVerbose: (value, ct) =>
            {
                if (CliLoggingPolicy.ShouldEmit(level, CliLogSeverity.Verbose))
                {
                    ConsoleSink.Verbose(value?.ToString() ?? string.Empty);
                }
                return Task.CompletedTask;
            },
            WriteDebug: (value, ct) =>
            {
                if (CliLoggingPolicy.ShouldEmit(level, CliLogSeverity.Debug))
                {
                    ConsoleSink.Debug(value?.ToString() ?? string.Empty);
                }
                return Task.CompletedTask;
            },
            WriteWarning: (value, ct) =>
            {
                if (CliLoggingPolicy.ShouldEmit(level, CliLogSeverity.Warn))
                {
                    ConsoleSink.Warn(value?.ToString() ?? string.Empty);
                }
                return Task.CompletedTask;
            },
            WriteInformation: (value, ct) =>
            {
                if (CliLoggingPolicy.ShouldEmit(level, CliLogSeverity.Info))
                {
                    ConsoleSink.Info(value?.ToString() ?? string.Empty);
                }
                return Task.CompletedTask;
            }
        );
    }

    internal static string? TryGetProfileOverride(string[] args)
    {
        // Normalized the same way ExtensionParser does (TrimStart('-')): the real parser
        // treats any leading-dash form as an option marker, so `-profile`/`--profile`/
        // `---profile` are all equivalent. Matching only "--profile" here would miss a
        // valid `-profile eu-profile` invocation, causing this early best-effort pass to
        // resolve the active profile instead of the explicit override — routing startup
        // telemetry (and anything replayed before ExecutionSession's later, authoritative
        // resolution) to the wrong tenant/boundary.
        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            if (!arg.StartsWith("-", StringComparison.Ordinal))
                continue;

            var option = arg.TrimStart('-');
            if (option.StartsWith("profile=", StringComparison.OrdinalIgnoreCase))
                return option["profile=".Length..];

            if (string.Equals(option, "profile", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                return args[i + 1];
        }

        return null;
    }

    // Cold-path host command: rendered from local data, no engine session or extension load.
    private static int DispatchVersion(VersionRequest version, string? query, Activity? activity)
    {
        using (var _ = _telemetryService?.StartActivity("AzureCLI.DispatchVersion"))
        {
            var exit = new RuntimeVersionDispatcher(_configurationService!, Console.Out).Render(version.Format, query);
            activity?.SetStatus(ActivityStatusCode.Ok, "Version requested");
            return exit;
        }
    }

    /// <summary>
    /// Renders help beneath an error that was already reported. Routed to stderr so stdout stays
    /// reserved for command results, and best-effort so a help failure cannot replace the error.
    /// </summary>
    private static void TryRenderHelpAfterError(HelpRequest help, OutputStreams streams, Activity? activity)
    {
        try
        {
            DispatchHelp(help, isBareInvocation: false, streams, activity, Console.Error);
        }
        catch (Exception ex)
        {
            _logger?.Debug($"command help unavailable: {ex.Message}");
        }
    }

    private static int DispatchHelp(HelpRequest help, bool isBareInvocation, OutputStreams streams, Activity? activity, TextWriter? output = null)
    {
        using (var _ = _telemetryService?.StartActivity("AzureCLI.DispatchHelp"))
        {
            var engine = AzclipsServiceProvider.GetRequiredService<IEngine>();
            var registryStore = AzclipsServiceProvider.GetRequiredService<IExtensionRegistryStore>();
            var renderer = new RuntimeHelpDispatcher(engine, streams, registryStore, output);
            if (help.Path.Count == 0)
            {
                // Banner only for truly bare `az`; user-typed help forms stay grep-friendly.
                if (isBareInvocation)
                {
                    WelcomeBanner.Write(Console.Out);
                }
                var rootExit = renderer.RenderRoot();
                activity?.SetStatus(ActivityStatusCode.Ok, "Help requested");
                return rootExit;
            }
            // Render extension help; structured errors are handled by Main.
            var extExit = renderer.RenderExtensionHelp(help.Path);
            activity?.SetStatus(ActivityStatusCode.Ok, "Extension help requested");
            return extExit;
        }
    }

    internal static void EmitStructuredError(
        AzclipsErrorException ex, OutputFormat format, bool colorPolicyAllowsColor,
        CommandInvocation? invocation, FailureCaptureResult? failure = null)
    {
        var message = invocation is not null && ex is UsageErrorException
            ? $"{ex.Message} (az {invocation.CommandName} --help)"
            : ex.Message;

        if (format is OutputFormat.Json or OutputFormat.Jsonc)
        {
            var payload = new Dictionary<string, object?>
            {
                ["error"] = new
                {
                    code = ex.Code,
                    message,
                    details = ex.Details,
                },
            };
            if (failure?.Recovery is { } recovery)
                payload["recovery"] = recovery;
            if (failure?.Warning is { } warning)
                payload["recoveryWarning"] = warning;
            var json = System.Text.Json.JsonSerializer.Serialize(payload, new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            });
            Console.Error.WriteLine(format == OutputFormat.Jsonc && colorPolicyAllowsColor ? JsonColorizer.Colorize(json) : json);
        }
        else
        {
            ConsoleSink.Error($"[{ex.Code}] {message}");
        }
    }

    private static Task<FailureCaptureResult> CaptureFailureAsync(
        string[] args, string[] positional, Exception error, int exitCode,
        CommandInvocation? invocation, Activity? activity) =>
        FailureCapture.SaveAsync(
            args, JoinShellQuoted(positional), error, exitCode, invocation?.CommandName, activity,
            () => _configurationService!.GetConfiguration<AgentOptions>(),
            () => AzclipsServiceProvider.GetRequiredService<IFailureHistoryStore>(),
            Environment.GetEnvironmentVariable(AgentModeEnvVar) == "1",
            _cancellationTokenSource.Token);

    internal static void EmitRecoveryDiagnostic(
        FailureCaptureResult result, OutputFormat format, bool color,
        bool structuredErrorAlreadyWritten = false)
    {
        if (result.Recovery is null && result.Warning is null)
            return;
        if (format is OutputFormat.Json or OutputFormat.Jsonc)
        {
            if (structuredErrorAlreadyWritten)
                return;
            var payload = new Dictionary<string, object?>();
            if (result.Recovery is { } recovery)
                payload["recovery"] = recovery;
            if (result.Warning is { } warning)
                payload["recoveryWarning"] = warning;
            var json = System.Text.Json.JsonSerializer.Serialize(payload);
            Console.Error.WriteLine(format == OutputFormat.Jsonc && color ? JsonColorizer.Colorize(json) : json);
            return;
        }

        if (result.Warning is not null)
            ConsoleSink.Warn(result.Warning);
        if (result.ShowHint && result.Entry is not null)
            Console.Error.WriteLine($"To troubleshoot this error with AI, run: az troubleshoot --id {result.Entry.Id}");
    }

    /// <summary>
    /// Stops the command root span, then runs <paramref name="fallback"/> with the stopped
    /// root's identifiers and resolved command name.
    /// </summary>
    /// <remarks>
    /// The ordering is load-bearing: AI error recovery blocks on azclips-agent.exe for as
    /// long as the model takes, and anything still inside the root span counts as command
    /// execution time.
    /// </remarks>
    internal static async Task<int> StopRootThenFallbackAsync(
        Activity? root,
        Func<string?, string?, string?, Task<int>> fallback)
    {
        // Read before disposing, so this does not depend on Activity's post-stop behaviour.
        var traceId = root?.TraceId.ToString();
        var spanId = root?.SpanId.ToString();
        // TagInvocation stamps the dispatcher-resolved command path here once parsing
        // succeeds. Reusing it keeps positional parameter values out of telemetry: raw argv
        // cannot distinguish a command-path token from a value bound to the positional slot.
        // Null when the failure happened during parsing, before a command was resolved.
        var commandName = root?.GetTagItem(TelemetryTags.Command) as string;

        root?.Dispose();

        return await fallback(traceId, spanId, commandName);
    }

    /// <summary>
    /// Builds the spawn shape for azclips-agent.exe. Separate from the launch itself so
    /// tests can assert the environment block without starting a process.
    /// </summary>
    internal static ProcessStartInfo BuildAgentStartInfo(
        string agentExe,
        string[] args,
        string? errorText,
        string? recoveryTraceparent)
    {
        var psi = new ProcessStartInfo
        {
            FileName = agentExe,
            // Set the working directory to the agent exe's own directory so relative
            // additionalProbingPaths in azclips-agent.runtimeconfig.json resolve correctly.
            // .NET resolves those at CLR startup, before any managed code runs, relative to
            // the working directory rather than the exe location — so it must be set here
            // and cannot be corrected from inside the agent. The user's real cwd is
            // forwarded via AZCLIPS_CALLER_CWD below.
            WorkingDirectory = Path.GetDirectoryName(agentExe)!,
            // Inherit stdio so streaming tokens, the y/n prompt, and device-code links all
            // show live in the user's terminal.
            UseShellExecute = false,
            RedirectStandardOutput = false,
            RedirectStandardError = false,
            RedirectStandardInput = false,
        };

        psi.Environment[AgentModeEnvVar] = "1";
        // Best-effort: GetCurrentDirectory can throw (e.g. the cwd was deleted), and
        // forwarding it is only a hint — skip it rather than fail the launch.
        try { psi.Environment[CallerCwdEnvVar] = Directory.GetCurrentDirectory(); }
        catch { /* leave CallerCwdEnvVar unset; the agent falls back to its own cwd */ }

        // Null when telemetry is off and no recovery span exists. Leave the variable
        // absent in that case rather than setting it to an empty string.
        psi.Environment.Remove(RecoveryTraceparentEnvVar);
        if (!string.IsNullOrEmpty(recoveryTraceparent))
            psi.Environment[RecoveryTraceparentEnvVar] = recoveryTraceparent;

        psi.ArgumentList.Add("--agent-mode");
        psi.ArgumentList.Add("--failed-command");
        psi.ArgumentList.Add(JoinShellQuoted(args));
        if (!string.IsNullOrWhiteSpace(errorText))
        {
            psi.ArgumentList.Add("--error");
            psi.ArgumentList.Add(errorText);
        }

        return psi;
    }

    private static async Task<int> TryAgentFallbackAsync(
        string[] args, string? errorText, string? commandTraceId, string? commandSpanId, string? commandName)
    {
        // Global enable switch: config `agent.enabled` (off by default for PP).
        // The user opts in with `az config set --key agent.enabled --value true`.
        // Fail-safe: if config can't be read, treat as disabled and surface the
        // original error rather than spawning the agent on an unknown state.
        AgentOptions agentOptions;
        try
        {
            var config = AzclipsServiceProvider.GetRequiredService<IConfigurationService>();
            agentOptions = config.GetConfiguration<AgentOptions>();
            if (!AutomaticRecoveryPolicy.ShouldStart(
                agentOptions, Environment.GetEnvironmentVariable(AgentModeEnvVar) == "1",
                Console.IsInputRedirected, Console.IsOutputRedirected))
            {
                return 1;
            }
        }
        catch (Exception ex)
        {
            _logger?.Debug($"agent fallback skipped: agent.enabled unavailable ({ex.Message})");
            return 1;
        }

        var agentExe = FindAgentExe();
        if (agentExe is null)
        {
            _logger?.Error("agent mode unavailable: azclips-agent.exe not found alongside az");
            return 1;
        }

        // Opened only once a spawn is certain. The command root is already stopped, so
        // Activity.Current is null and this starts a second root with its own TraceId.
        using var recovery = _telemetryService?.StartRootActivity(name: "CLI.Activity.AiRecovery");
        recovery?.SetTag(TelemetryTags.CommandTraceId, commandTraceId);
        recovery?.SetTag(TelemetryTags.CommandSpanId, commandSpanId);
        var telemetryCommandName = commandName ?? GetCommandGroupFromArgs(args);
        if (!string.IsNullOrEmpty(telemetryCommandName))
        {
            recovery?.SetTag(TelemetryTags.Command, telemetryCommandName);
        }
        recovery?.SetTag(TelemetryTags.RecoverySessionStyle, agentOptions.SessionStyle);
        recovery?.SetTag(TelemetryTags.RecoveryTrigger, "automatic");
        recovery?.SetTag(TelemetryTags.RecoveryProvider, agentOptions.Provider);

        try
        {
            var psi = BuildAgentStartInfo(agentExe, args, errorText, recovery?.Id);

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

                recovery?.SetTag(TelemetryTags.RecoveryExitCode, proc.ExitCode);
                recovery?.SetStatus(
                    proc.ExitCode == 0 ? ActivityStatusCode.Ok : ActivityStatusCode.Error);

                // Exit code semantics:
                //   one-shot — the agent only PROPOSED a fix; it never ran anything, so the
                //     user's original command still failed. Report failure (never let the
                //     agent's own 0 mask it), so `&&`, CI, and $? see the command as failed.
                //   interactive — the agent may have actually run commands and resolved the
                //     task, so its real exit code is meaningful; pass it through unchanged.
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
            recovery?.SetStatus(ActivityStatusCode.Error);
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

    // Fallback for when the failure happened during parsing, so no command was resolved.
    // Emits the command group only: positional values bind after the matched command path
    // (ExtensionDispatcher skips the verb plus matched tokens before collecting them), so
    // args[0] is always a command group and never a parameter value.
    internal static string GetCommandGroupFromArgs(string[] args) =>
        args.Length == 0 || args[0].StartsWith('-') ? string.Empty : args[0];

    internal static string? FindAgentExe()
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
    internal static void SpawnTelemetryUploader()
    {
        try
        {
            var startInfo = BuildTelemetryUploaderStartInfo(Environment.ProcessPath);
            if (startInfo is null) return;

            // Windows process guard, must be run before process creation.
            DetachedStdio.SuppressStandardHandleInheritance();

            using var uploader = Process.Start(startInfo);
            // Drop our ends of the three pipes immediately.
            CloseRedirectedStreams(uploader);
        }
        catch
        {
            // Best effort. If spawn fails, traces remain on disk
            // and will be uploaded on the next CLI invocation.
        }
    }

    /// <summary>
    /// Builds the start info for the detached <c>--upload-telemetry</c> process, or
    /// <see langword="null"/> when this process must not spawn one.
    /// </summary>
    internal static ProcessStartInfo? BuildTelemetryUploaderStartInfo(string? exePath)
    {
        if (string.IsNullOrEmpty(exePath)) return null;

        // In-process benchmark mode: do not attempt to flush telemetry as in benchmark execution process
        var exeName = Path.GetFileName(exePath);
        if (!exeName.Equals("az", StringComparison.OrdinalIgnoreCase) &&
            !exeName.Equals("az.exe", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return new ProcessStartInfo
        {
            FileName = exePath,
            Arguments = "--upload-telemetry",
            UseShellExecute = false,
            CreateNoWindow = true,
            // The uploader outlives us, so it must not inherit our stdio: an
            // inherited pipe keeps the caller's reader waiting on the upload
            // rather than on `az`.
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
    }

    /// <summary>Closes the parent-side ends of a redirected child's stdio pipes.</summary>
    private static void CloseRedirectedStreams(Process? process)
    {
        if (process is null) return;

        try { process.StandardInput.Close(); } catch { /* already closed */ }
        try { process.StandardOutput.Close(); } catch { /* already closed */ }
        try { process.StandardError.Close(); } catch { /* already closed */ }
    }
}
